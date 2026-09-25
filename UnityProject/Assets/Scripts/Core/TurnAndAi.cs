using System;
using System.Collections.Generic;
using System.Linq;

namespace Hastings
{
    public sealed partial class GameEngine
    {
        public void Advance()
        {
            if(state.playerSide==Side.Saxon)
            {
                AdvanceSaxonPlayer();
                return;
            }
            switch(state.phase)
            {
                case Phase.NormanFire:
                    if(OptionsPending())return;
                    state.phase=Phase.NormanMove;Log("Norman movement segment");break;
                case Phase.NormanMove:
                    ResolveUnmovedRequiredMoves(Side.Norman);
                    AiReaction(Side.Saxon);ResetFire();AiFire(Side.Saxon);
                    foreach(var u in Living(Side.Saxon))u.reacted=false;
                    state.phase=Phase.NormanMelee;Log("Norman melee segment");break;
                case Phase.NormanMelee:
                    ResolveRemainingMandatory(Side.Norman);
                    Rally(Side.Saxon);ResetFire();AiFire(Side.Saxon);AiMove(Side.Saxon);
                    ResolveUnmovedRequiredMoves(Side.Saxon);
                    state.phase=Phase.NormanReaction;Log("Norman reaction segment");break;
                case Phase.NormanReaction:
                    ResetFire();state.phase=Phase.NormanDefenseFire;
                    Log("Norman defensive missile segment");break;
                case Phase.NormanDefenseFire:
                    AiMelee(Side.Saxon);EndTurn();break;
            }
        }
        private void AdvanceSaxonPlayer()
        {
            switch(state.phase)
            {
                case Phase.NormanFire:
                    if(OptionsPending())return;
                    AiFire(Side.Norman);
                    state.phase=Phase.NormanMove;Log("Norman movement segment");
                    AiMove(Side.Norman);ResolveUnmovedRequiredMoves(Side.Norman);
                    state.phase=Phase.SaxonReaction;Log("Saxon reaction segment");break;
                case Phase.SaxonReaction:
                    ResetFire();state.phase=Phase.SaxonDefenseFire;
                    Log("Saxon defensive missile segment");break;
                case Phase.SaxonDefenseFire:
                    AiMelee(Side.Norman);
                    Rally(Side.Saxon);ResetFire();state.phase=Phase.SaxonFire;
                    Log("Saxon missile fire segment");break;
                case Phase.SaxonFire:
                    state.phase=Phase.SaxonMove;EnterReinforcements();
                    Log("Saxon movement segment");break;
                case Phase.SaxonMove:
                    ResolveUnmovedRequiredMoves(Side.Saxon);
                    AiReaction(Side.Norman);ResetFire();
                    state.phase=Phase.NormanDefenseFire;AiFire(Side.Norman);
                    foreach(var u in Living(Side.Norman))u.reacted=false;
                    state.phase=Phase.SaxonMelee;Log("Saxon melee segment");break;
                case Phase.SaxonMelee:
                    ResolveRemainingMandatory(Side.Saxon);EndTurn();break;
            }
        }
        private void ResolveUnmovedRequiredMoves(Side side)
        {
            foreach(var unit in RequiredMovementUnits(side))
            {
                var options=LegalMoves(unit).Values.ToList();
                if(options.Count==0)continue;
                var best=options.OrderBy(o=>NearestEnemyDistance(unit.side,o.destination))
                    .ThenByDescending(o=>o.charge).ThenBy(o=>o.destination).First();
                MoveCore(unit,best,false);
            }
        }
        public List<UnitState> RequiredChargeMoves(Side side)
        {
            return Living(side).Where(unit=>UnitTypes.Get(unit).knight &&
                unit.status==Status.Ready && !unit.moved && OrderFor(unit)==Order.Charge &&
                LegalMoves(unit).Count>0).ToList();
        }
        public List<UnitState> RequiredAttackPursueMoves(Side side)
        {
            return Living(side).Where(unit=>!UnitTypes.Get(unit).leader &&
                unit.status==Status.Ready && !unit.moved &&
                OrderFor(unit)==Order.AttackPursue &&
                NearestEnemyDistance(side,unit.hex)>1 && LegalMoves(unit).Count>0).ToList();
        }
        public List<UnitState> RequiredMovementUnits(Side side)
        {
            return RequiredChargeMoves(side).Concat(RequiredAttackPursueMoves(side))
                .Concat(RequiredBowmenZocMoves(side)).GroupBy(unit=>unit.id)
                .Select(group=>group.First()).ToList();
        }
        public List<UnitState> RequiredBowmenZocMoves(Side side)
        {
            return Living(side).Where(unit=>UnitTypes.Get(unit).missile=="B" &&
                unit.status==Status.Ready && !unit.moved && InEnemyZoc(side,unit.hex) &&
                LegalMoves(unit).Count>0).ToList();
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
            while(state.phase!=Phase.GameOver)
            {
                var attacker=RequiredMeleeAttackers(side).FirstOrDefault();
                if(attacker==null)return;
                var targets=RequiredMeleeTargets(new[]{attacker});
                if(targets.Count==0)return;
                ResolveMelee(new List<UnitState>{attacker},targets);
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
                state.phase=Phase.Reform;
                AutoReformSaxons();
                if(state.playerSide==Side.Saxon)AutoReformNormans();
                Log("First assault ends. Reform the "+state.playerSide+" army.");
                return;
            }
            if(state.period==2 && state.turn>8)
            {
                state.result="Saxon strategic victory";state.phase=Phase.GameOver;Log(state.result);return;
            }
            state.phase=Phase.Orders;Log("New battle turn. Choose "+state.playerSide+" strategies.");
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
        private void AiReaction(Side side)
        {
            foreach(var unit in Living(side).Where(u=>u.status==Status.Ready &&
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
        private void AiFire(Side side)
        {
            foreach(var shooter in Living(side).Where(u=>UnitTypes.Get(u).missile!="" &&
                u.status==Status.Ready).ToList())
            {
                UnitState target=null;bool high=false;double best=-10000;
                foreach(var enemy in Living(Opposite(side)).Where(u=>!UnitTypes.Get(u).leader ||
                    UnitAt(u.hex,Opposite(side))==null))
                {
                    bool h=false;
                    if(!CanFire(shooter,enemy,false))
                    {h=true;if(!CanFire(shooter,enemy,true))continue;}
                    int strength=RuleTables.MissileStrength(UnitTypes.Get(shooter).missile,
                        board.Distance(shooter.hex,enemy.hex));
                    double score=(double)strength/Defense(enemy,false)*10+
                        (enemy.type=="WG"||enemy.type=="Harold"?4:0)+
                        (enemy.reduced?2:0)-board.Distance(shooter.hex,enemy.hex)*.1;
                    if(score>best){best=score;target=enemy;high=h;}
                }
                if(target!=null)Fire(new List<UnitState>{shooter},target,high);
            }
        }
        private void AiMove(Side side)
        {
            if(side==Side.Saxon)EnterReinforcements();
            var units=Living(side).Where(u=>u.status==Status.Ready && !UnitTypes.Get(u).leader)
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
            foreach(var leader in Living(side).Where(u=>UnitTypes.Get(u).leader && u.status==Status.Ready))
            {
                var friendly=Living(side).Where(u=>!UnitTypes.Get(u).leader && u.group==leader.group).ToList();
                if(friendly.Count==0)continue;
                var options=LegalMoves(leader).Values.Where(o=>UnitAt(o.destination,side)!=null).ToList();
                if(options.Count==0)continue;
                var best=options.OrderByDescending(o=>friendly.Count(u=>board.Distance(o.destination,u.hex)<=3))
                    .ThenBy(o=>NearestEnemyDistance(side,o.destination)).First();
                if(friendly.Count(u=>board.Distance(best.destination,u.hex)<=3)>
                   friendly.Count(u=>board.Distance(leader.hex,u.hex)<=3))MoveCore(leader,best,false);
            }
        }
        private double AiPositionScore(UnitState unit,string hex)
        {
            var h=board.Hex(hex);int enemy=NearestEnemyDistance(unit.side,hex);
            double score=h.level*2+(h.road?3:0)+(h.woods?1:0)+(h.marsh?-3:0);
            int row=int.Parse(hex.Substring(0,2));
            if(unit.side==Side.Saxon && row>9)score-=7;
            if(unit.side==Side.Norman)score-=row*.18;
            if(UnitTypes.Get(unit).missile=="B" || UnitTypes.Get(unit).missile=="S")
                score+=enemy>=2 && enemy<=3?2:enemy==1?-3:0;
            else score+=enemy==1?2:enemy==2?1:enemy>5?-1:0;
            if(h.level>=4 && row<=8)score+=unit.side==Side.Saxon?2:3;
            return score;
        }
        private void AiMelee(Side side)
        {
            var attackers=Living(side).Where(u=>!UnitTypes.Get(u).leader && u.status==Status.Ready)
                .OrderByDescending(u=>UnitTypes.Get(u).attack).ToList();
            foreach(var attacker in attackers)
            {
                if(state.phase==Phase.GameOver)return;
                if(attacker.engaged)continue;
                var targets=RequiredMeleeTargets(new[]{attacker});
                if(targets.Count==0)continue;
                var targetIds=new HashSet<string>(targets.Select(target=>target.id));
                var partners=attackers.Where(a=>a!=attacker && !a.engaged &&
                    targets.Any(target=>CanMeleeUnderObligation(a,target)) &&
                    RequiredMeleeTargets(new[]{a}).All(target=>targetIds.Contains(target.id)))
                    .OrderByDescending(a=>UnitTypes.Get(a).attack).Take(2).ToList();
                var group=new List<UnitState>{attacker};group.AddRange(partners);
                ResolveMelee(group,targets);
            }
            ResolveRemainingMandatory(side);
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
                UpdateReserveOrder(unit);
                arrived++;
                Log(unit.id+" reinforces at "+destination);
            }
        }
        private void AutoReformNormans()
        {
            var sites=board.data.hexes.Where(h=>
                int.Parse(h.id.Substring(2,2))>=6 && int.Parse(h.id.Substring(2,2))<=26 &&
                board.data.hexes.Where(x=>x.level>=4 && int.Parse(x.id.Substring(0,2))<=9)
                    .Min(x=>board.Distance(h.id,x.id))>=4)
                .OrderByDescending(h=>h.y).ThenBy(h=>h.id).ToList();
            foreach(var unit in Living(Side.Norman).OrderBy(u=>UnitTypes.Get(u).leader?1:0).ToList())
            {
                foreach(var site in sites)
                    if(ReformMoveCore(unit,site.id,false))break;
            }
        }
        private void AutoReformSaxons()
        {
            var combat=state.units.Where(unit=>unit.side==Side.Saxon &&
                unit.status!=Status.Eliminated && !UnitTypes.Get(unit).leader).ToList();
            foreach(var unit in combat)unit.hex="";
            var hillSlots=board.data.hexes.Where(hex=>ReformArea(Side.Saxon,hex.id))
                .OrderBy(hex=>Math.Abs(int.Parse(hex.id.Substring(2,2))-16))
                .ThenByDescending(hex=>hex.level).ThenBy(hex=>hex.id).ToList();
            foreach(var unit in combat)
            {
                var slot=hillSlots.FirstOrDefault(hex=>UnitAt(hex.id,Side.Saxon)==null &&
                    UnitAt(hex.id,Side.Norman)==null);
                if(slot==null)break;
                unit.hex=slot.id;unit.reservePeriod=0;unit.reserveTurn=0;
                unit.reserveOrder=false;unit.entrySpent=0;
            }
            var placedCombat=combat.Where(unit=>board.Has(unit.hex)).ToList();
            foreach(var leader in state.units.Where(unit=>unit.side==Side.Saxon &&
                unit.status!=Status.Eliminated && UnitTypes.Get(unit).leader))
            {
                var companion=placedCombat.FirstOrDefault(unit=>unit.group==leader.group)??
                    placedCombat.FirstOrDefault();
                if(companion!=null)leader.hex=companion.hex;
            }
        }
        public bool ReformMove(UnitState unit,string hex)
        {
            return ReformMoveCore(unit,hex,true);
        }
        private bool ReformMoveCore(UnitState unit,string hex,bool requirePlayer)
        {
            if(state.phase!=Phase.Reform || (requirePlayer && unit.side!=state.playerSide) || !board.Has(hex) ||
               !ReformArea(unit.side,hex) || UnitAt(hex,Opposite(unit.side))!=null)return false;
            var occupant=UnitAt(hex,unit.side);
            if(!UnitTypes.Get(unit).leader && occupant!=null && occupant!=unit)return false;
            unit.hex=hex;Log(unit.id+" reforms at "+hex);return true;
        }
        public bool FinishReform()
        {
            if(state.phase!=Phase.Reform)return false;
            foreach(var unit in state.units.Where(unit=>unit.status!=Status.Eliminated))
                if(!board.Has(unit.hex)||!ReformArea(unit.side,unit.hex))return false;
            if(state.units.Where(unit=>unit.status!=Status.Eliminated &&
                    !UnitTypes.Get(unit).leader).GroupBy(unit=>unit.hex).Any(group=>group.Count()>1))
                return false;
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
