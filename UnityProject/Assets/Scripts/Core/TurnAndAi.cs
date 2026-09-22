using System;
using System.Collections.Generic;
using System.Linq;

namespace Hastings
{
    public sealed partial class GameEngine
    {
        public void Advance()
        {
            switch(state.phase)
            {
                case Phase.NormanFire:
                    if(OptionsPending())return;
                    state.phase=Phase.NormanMove;Log("Norman movement segment");break;
                case Phase.NormanMove:
                    ResolveUnmovedCharges();
                    AiReaction();ResetFire();AiFire();
                    foreach(var u in Living(Side.Saxon))u.reacted=false;
                    state.phase=Phase.NormanMelee;Log("Norman melee segment");break;
                case Phase.NormanMelee:
                    ResolveRemainingMandatory(Side.Norman);
                    Rally(Side.Saxon);ResetFire();AiFire();AiMove();
                    state.phase=Phase.NormanReaction;Log("Norman reaction segment");break;
                case Phase.NormanReaction:
                    ResetFire();state.phase=Phase.NormanDefenseFire;
                    Log("Norman defensive missile segment");break;
                case Phase.NormanDefenseFire:
                    AiMelee();EndTurn();break;
            }
        }
        private void ResolveUnmovedCharges()
        {
            foreach(var unit in Living(Side.Norman).Where(u=>UnitTypes.Get(u).knight &&
                u.status==Status.Ready && !u.moved && OrderFor(u)==Order.Charge).ToList())
            {
                var options=LegalMoves(unit).Values.Where(o=>NearestEnemyDistance(unit.side,o.destination)<
                    NearestEnemyDistance(unit.side,unit.hex)).ToList();
                if(options.Count==0)continue;
                var best=options.OrderBy(o=>NearestEnemyDistance(unit.side,o.destination))
                    .ThenByDescending(o=>o.charge).ThenBy(o=>o.destination).First();
                MoveCore(unit,best,false);
            }
        }
        private void ResetFire()
        {
            foreach(var unit in state.units)unit.fired=false;
            foreach(var group in state.groups)group.firedThisSegment=false;
        }
        private void ResetTurn()
        {
            foreach(var unit in state.units)
            {unit.moved=false;unit.fired=false;unit.reacted=false;unit.charged=false;unit.engaged=false;
             unit.entrySpent=0;
             if(unit.leaderCondition==1 && state.turn>unit.shakenUntil)unit.leaderCondition=0;}
            foreach(var group in state.groups)group.firedThisSegment=false;
        }
        private void ResolveRemainingMandatory(Side side)
        {
            foreach(var attacker in Living(side).Where(u=>!UnitTypes.Get(u).leader && !u.engaged && u.status==Status.Ready).ToList())
            {
                var targets=Living(Opposite(side)).Where(d=>!UnitTypes.Get(d).leader && !d.engaged &&
                    CanMelee(attacker,d) && Controls(d,attacker.hex)).ToList();
                if(OrderFor(attacker)==Order.ShieldWall)continue;
                if(targets.Count==0)continue;
                ResolveMelee(new List<UnitState>{attacker},new List<UnitState>{targets[0]});
                if(state.phase==Phase.GameOver)return;
            }
        }
        private void EndTurn()
        {
            if(state.phase==Phase.GameOver)return;
            state.turn++;ResetTurn();CheckVictory();
            if(state.phase==Phase.GameOver)return;
            if(state.period==1 && state.turn==9)
            {
                int normans=Living(Side.Norman).Count(u=>!UnitTypes.Get(u).leader);
                int hill=Living(Side.Norman).Count(u=>!UnitTypes.Get(u).leader &&
                    board.Hex(u.hex).level>=4 && int.Parse(u.hex.Substring(0,2))<=9);
                if(IsEncircled())state.extendedTo=999;
                else if(normans>0 && hill*4>normans*3)state.extendedTo=11;
                else if(normans>0 && hill*2>normans)state.extendedTo=10;
                if(state.extendedTo>8)Log(state.extendedTo==999?
                    "First assault extended while the Saxons remain encircled":
                    "First assault extended to turn "+state.extendedTo);
            }
            if(state.period==1 && (state.turn>state.extendedTo ||
                (state.extendedTo==999 && !IsEncircled())))
            {
                PrepareReform();
                state.phase=Phase.Reform;Log("First assault ends. Reform Norman units south of Senlac Hill.");
                return;
            }
            if(state.period==2 && state.turn>8)
            {
                state.result="Saxon strategic victory";state.phase=Phase.GameOver;Log(state.result);return;
            }
            state.phase=Phase.Orders;Log("New battle turn. Choose Norman strategies.");
        }
        private bool IsEncircled()
        {
            var saxons=Living(Side.Saxon).Where(u=>!UnitTypes.Get(u).leader && u.status==Status.Ready).ToList();
            if(saxons.Count==0)return false;
            foreach(var unit in saxons)
            {
                var seen=new HashSet<string>{unit.hex};var open=new Queue<string>();open.Enqueue(unit.hex);
                while(open.Count>0)
                {
                    var current=open.Dequeue();
                    if(current.StartsWith("01"))return false;
                    foreach(var next in board.Adjacent(current))
                        if(seen.Add(next) && UnitAt(next,Side.Norman)==null && !InEnemyZoc(Side.Saxon,next))
                            open.Enqueue(next);
                }
            }
            return true;
        }
        private bool ReformArea(Side side,string hex)
        {
            if(side==Side.Saxon)return board.Hex(hex).level>=4 && int.Parse(hex.Substring(0,2))<=8;
            int column=int.Parse(hex.Substring(2,2));
            return column>=6 && column<=26 &&
                board.data.hexes.Where(h=>h.level>=4 && int.Parse(h.id.Substring(0,2))<=9)
                    .Min(h=>board.Distance(hex,h.id))>=4;
        }
        private bool CanTraceReform(UnitState unit)
        {
            var visited=new HashSet<string>{unit.hex};var open=new Queue<string>();open.Enqueue(unit.hex);
            while(open.Count>0)
            {
                string current=open.Dequeue();if(ReformArea(unit.side,current))return true;
                foreach(var next in board.Adjacent(current))
                {
                    if(!visited.Add(next) || UnitAt(next,Opposite(unit.side))!=null)continue;
                    if(InEnemyZoc(unit.side,next) && UnitAt(next,unit.side)==null)continue;
                    open.Enqueue(next);
                }
            }
            return false;
        }
        private void PrepareReform()
        {
            foreach(var side in new[]{Side.Norman,Side.Saxon})
            {
                bool hasLeader=Living(side).Any(u=>UnitTypes.Get(u).leader);
                foreach(var unit in Living(side).ToList())
                {
                    if(!UnitTypes.Get(unit).leader && unit.status!=Status.Ready)
                    {
                        if(hasLeader)unit.status=Status.Ready;
                        else{Eliminate(unit);continue;}
                    }
                    if(!CanTraceReform(unit))Eliminate(unit);
                }
            }
        }
        private void AiReaction()
        {
            foreach(var unit in Living(Side.Saxon).Where(u=>u.status==Status.Ready &&
                NearestEnemyDistance(u.side,u.hex)<=1).ToList())
            {
                var options=LegalMoves(unit,true).Values.ToList();
                if(options.Count==0)continue;
                var best=options.OrderByDescending(o=>board.Hex(o.destination).level)
                    .ThenByDescending(o=>board.Hex(o.destination).road)
                    .ThenBy(o=>o.destination).First();
                // Preserve the shield wall if retreat would surrender a valuable position.
                if(board.Hex(best.destination).level<board.Hex(unit.hex).level &&
                   board.Hex(unit.hex).road)continue;
                MoveCore(unit,best,true);
            }
        }
        private void AiFire()
        {
            foreach(var shooter in Living(Side.Saxon).Where(u=>UnitTypes.Get(u).missile!="" &&
                u.status==Status.Ready).ToList())
            {
                UnitState target=null;bool high=false;double best=-10000;
                foreach(var enemy in Living(Side.Norman).Where(u=>!UnitTypes.Get(u).leader ||
                    UnitAt(u.hex,Side.Norman)==null))
                {
                    bool h=false;
                    if(!CanFire(shooter,enemy,false))
                    {h=true;if(!CanFire(shooter,enemy,true))continue;}
                    int strength=RuleTables.MissileStrength(UnitTypes.Get(shooter).missile,
                        board.Distance(shooter.hex,enemy.hex));
                    double score=(double)strength/Defense(enemy,false)*10+
                        (enemy.type=="WG"?4:0)+(enemy.reduced?2:0)-board.Distance(shooter.hex,enemy.hex)*.1;
                    if(score>best){best=score;target=enemy;high=h;}
                }
                if(target!=null)Fire(new List<UnitState>{shooter},target,high);
            }
        }
        private void AiMove()
        {
            EnterReinforcements();
            var units=Living(Side.Saxon).Where(u=>u.status==Status.Ready && !UnitTypes.Get(u).leader)
                .OrderBy(u=>u.hex).ToList();
            foreach(var unit in units)
            {
                if(state.phase==Phase.GameOver)return;
                var options=LegalMoves(unit).Values.ToList();if(options.Count==0)continue;
                double current=AiPositionScore(unit,unit.hex);
                var best=options.OrderByDescending(o=>AiPositionScore(unit,o.destination)+
                    (OrderFor(unit)==Order.AttackPursue?1.5*(NearestEnemyDistance(unit.side,unit.hex)-
                        NearestEnemyDistance(unit.side,o.destination)):0))
                    .ThenBy(o=>o.destination).First();
                double score=AiPositionScore(unit,best.destination);
                if(score>current+.25 || (OrderFor(unit)==Order.AttackPursue &&
                    NearestEnemyDistance(unit.side,best.destination)<NearestEnemyDistance(unit.side,unit.hex)))
                    MoveCore(unit,best,false);
            }
            // Commanders remain near their wing and off exposed road approaches.
            foreach(var leader in Living(Side.Saxon).Where(u=>UnitTypes.Get(u).leader && u.status==Status.Ready))
            {
                var friendly=Living(Side.Saxon).Where(u=>!UnitTypes.Get(u).leader && u.group==leader.group).ToList();
                if(friendly.Count==0)continue;
                var options=LegalMoves(leader).Values.Where(o=>UnitAt(o.destination,Side.Saxon)!=null).ToList();
                if(options.Count==0)continue;
                var best=options.OrderByDescending(o=>friendly.Count(u=>board.Distance(o.destination,u.hex)<=3))
                    .ThenBy(o=>NearestEnemyDistance(Side.Saxon,o.destination)).First();
                if(friendly.Count(u=>board.Distance(best.destination,u.hex)<=3)>
                   friendly.Count(u=>board.Distance(leader.hex,u.hex)<=3))MoveCore(leader,best,false);
            }
        }
        private double AiPositionScore(UnitState unit,string hex)
        {
            var h=board.Hex(hex);int enemy=NearestEnemyDistance(Side.Saxon,hex);
            double score=h.level*2+(h.road?3:0)+(h.woods?1:0)+(h.marsh?-3:0);
            if(int.Parse(hex.Substring(0,2))>9)score-=7;
            if(UnitTypes.Get(unit).missile=="B" || UnitTypes.Get(unit).missile=="S")
                score+=enemy>=2 && enemy<=3?2:enemy==1?-3:0;
            else score+=enemy==1?2:enemy==2?1:enemy>5?-1:0;
            if(h.level>=4 && int.Parse(hex.Substring(0,2))<=8)score+=2;
            return score;
        }
        private void AiMelee()
        {
            var attackers=Living(Side.Saxon).Where(u=>!UnitTypes.Get(u).leader && u.status==Status.Ready)
                .OrderByDescending(u=>UnitTypes.Get(u).attack).ToList();
            foreach(var attacker in attackers)
            {
                if(state.phase==Phase.GameOver)return;
                if(attacker.engaged)continue;
                var targets=Living(Side.Norman).Where(d=>!UnitTypes.Get(d).leader && !d.engaged &&
                    CanMelee(attacker,d)).OrderBy(d=>Defense(d,true)).ToList();
                if(targets.Count==0)continue;
                var target=targets[0];
                var partners=attackers.Where(a=>a!=attacker && !a.engaged && CanMelee(a,target))
                    .OrderByDescending(a=>UnitTypes.Get(a).attack).Take(2).ToList();
                var group=new List<UnitState>{attacker};group.AddRange(partners);
                ResolveMelee(group,new List<UnitState>{target});
            }
            ResolveRemainingMandatory(Side.Saxon);
        }
        private void EnterReinforcements()
        {
            int arrived=0;
            foreach(var unit in state.units.Where(u=>u.side==Side.Saxon && u.hex=="" &&
                u.reservePeriod==state.period && u.reserveTurn==state.turn).ToList())
            {
                string destination=UnitAt("0116",Side.Saxon)==null && UnitAt("0116",Side.Norman)==null?
                    "0116":board.data.hexes.Where(h=>h.id.StartsWith("01") &&
                        UnitAt(h.id,Side.Saxon)==null && UnitAt(h.id,Side.Norman)==null)
                        .OrderBy(h=>board.Distance(h.id,"0116")).Select(h=>h.id).FirstOrDefault();
                if(destination==null)continue;
                unit.hex=destination;unit.group=board.Distance(destination,"0721")<board.Distance(destination,"0710")?
                    "Left":"Right";unit.reservePeriod=0;unit.moved=false;unit.reserveOrder=true;
                unit.entrySpent=1+(board.Hex(destination).woods||board.Hex(destination).marsh?1:0)+arrived;
                arrived++;
                Log(unit.id+" reinforces at "+destination);
            }
        }
        public bool ReformMove(UnitState unit,string hex)
        {
            if(state.phase!=Phase.Reform || unit.side!=Side.Norman || !board.Has(hex))return false;
            var h=board.Hex(hex);
            if(int.Parse(hex.Substring(2,2))<6 || int.Parse(hex.Substring(2,2))>26)return false;
            int hill=board.data.hexes.Where(x=>x.level>=4 && int.Parse(x.id.Substring(0,2))<=9)
                .Min(x=>board.Distance(hex,x.id));
            if(hill<4 || (UnitAt(hex,Side.Norman)!=null && !UnitTypes.Get(unit).leader))return false;
            unit.hex=hex;Log(unit.id+" reforms at "+hex);return true;
        }
        public bool FinishReform()
        {
            if(state.phase!=Phase.Reform)return false;
            foreach(var unit in Living(Side.Norman))
            {
                int hill=board.data.hexes.Where(x=>x.level>=4 && int.Parse(x.id.Substring(0,2))<=9)
                    .Min(x=>board.Distance(unit.hex,x.id));
                if(hill<4)return false;
            }
            foreach(var unit in Living(Side.Norman).ToList())
                if(unit.status!=Status.Ready)
                {
                    if(Living(Side.Norman).Any(u=>UnitTypes.Get(u).leader))unit.status=Status.Ready;
                    else Eliminate(unit);
                }
            var hillSlots=board.data.hexes.Where(h=>h.level>=4 && int.Parse(h.id.Substring(0,2))<=8)
                .OrderBy(h=>Math.Abs(int.Parse(h.id.Substring(2,2))-16)).ToList();
            foreach(var unit in Living(Side.Saxon).Where(u=>!UnitTypes.Get(u).leader).ToList())
            {
                var slot=hillSlots.FirstOrDefault(h=>UnitAt(h.id,Side.Saxon)==null && UnitAt(h.id,Side.Norman)==null);
                if(slot!=null)unit.hex=slot.id;
                if(unit.status!=Status.Ready)
                {
                    if(Living(Side.Saxon).Any(u=>UnitTypes.Get(u).leader))unit.status=Status.Ready;
                    else Eliminate(unit);
                }
            }
            foreach(var unit in state.units.Where(u=>u.hex=="" && u.reservePeriod==2))
            {
                var slot=hillSlots.FirstOrDefault(h=>UnitAt(h.id,Side.Saxon)==null && UnitAt(h.id,Side.Norman)==null);
                if(slot==null)break;unit.hex=slot.id;unit.reservePeriod=0;
            }
            foreach(var group in state.groups)
            {
                int baseSupply=group.id=="Left"||group.id=="Center"||group.id=="Right"?4:6;
                group.carry=Math.Max(0,baseSupply-group.fireSegments);
                group.fireSegments=0;group.effect=0;group.footDuration=0;group.knightDuration=0;
                group.footPendingEffect=0;group.knightPendingEffect=0;
                group.footOptional=false;group.knightOptional=false;
            }
            foreach(var leader in state.units.Where(u=>UnitTypes.Get(u).leader && u.status!=Status.Eliminated))
                leader.leaderCondition=0;
            state.period=2;state.turn=1;state.extendedTo=8;ResetTurn();
            state.phase=Phase.Orders;Log("Second assault begins. Strategy effects reset.");return true;
        }
    }
}
