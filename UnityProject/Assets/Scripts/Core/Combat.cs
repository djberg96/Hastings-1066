using System;
using System.Collections.Generic;
using System.Linq;

namespace Hastings
{
    public sealed partial class GameEngine
    {
        public bool CanFire(UnitState shooter, UnitState target, bool high=false)
        {
            var type=UnitTypes.Get(shooter);
            if(type.missile=="" || shooter.status!=Status.Ready || shooter.fired ||
               target.side==shooter.side || target.status==Status.Eliminated ||
               !board.Has(shooter.hex)||!board.Has(target.hex))return false;
            if(shooter.reacted && type.missile!="B")return false;
            if(shooter.side==Side.Norman && state.phase!=Phase.NormanFire && state.phase!=Phase.NormanDefenseFire)return false;
            if(shooter.side==Side.Saxon && state.playerSide==Side.Saxon &&
               state.phase!=Phase.SaxonFire && state.phase!=Phase.SaxonDefenseFire)return false;
            if(OptionsPending())return false;
            int range=board.Distance(shooter.hex,target.hex);
            if(RuleTables.MissileStrength(type.missile,range)==0)return false;
            int direction=board.Direction(shooter.hex,target.hex);
            if(direction!=shooter.facing && direction!=(shooter.facing+1)%6)return false;
            if(high && (type.missile!="B" || (shooter.side==Side.Norman && state.period==1)))return false;
            if(InEnemyZoc(shooter.side,shooter.hex) && !Living(target.side).Any(e=>e.id==target.id && Controls(e,shooter.hex)))return false;
            if(shooter.side==Side.Norman && type.missile=="B" && !SupplyAvailable(shooter.group,Side.Norman))return false;
            if(shooter.side==Side.Saxon && type.missile=="J" && !SupplyAvailable(shooter.group,Side.Saxon))return false;
            return LineOfSight(shooter.hex,target.hex,high);
        }
        private bool SupplyAvailable(string group,Side side)
        {
            var g=state.groups.First(x=>x.id==group);
            int baseSupply=side==Side.Norman?(state.period==1?6:3):(state.period==1?4:2);
            return g.firedThisSegment || g.fireSegments<Math.Min(6,baseSupply+g.carry);
        }
        private static double DistanceToLine(float px,float py,float ax,float ay,float bx,float by)
        {
            double dx=bx-ax,dy=by-ay;
            double t=Math.Max(0,Math.Min(1,((px-ax)*dx+(py-ay)*dy)/(dx*dx+dy*dy)));
            return Math.Sqrt(Math.Pow(px-ax-t*dx,2)+Math.Pow(py-ay-t*dy,2));
        }
        private bool LineOfSight(string from,string to,bool high)
        {
            var a=board.Hex(from);var b=board.Hex(to);
            foreach(var h in board.data.hexes)
            {
                if(h.id==from||h.id==to)continue;
                if(DistanceToLine(h.x,h.y,a.x,a.y,b.x,b.y)>44)continue;
                if(board.Distance(from,h.id)+board.Distance(h.id,to)>board.Distance(from,to))continue;
                if(h.woods||h.level>Math.Max(a.level,b.level))return false;
                if(!high && (UnitAt(h.id,Side.Norman)!=null || UnitAt(h.id,Side.Saxon)!=null))return false;
            }
            return true;
        }
        public bool Fire(List<UnitState> shooters,UnitState target,bool high=false)
        {
            if(shooters==null||shooters.Count==0 || shooters.Any(s=>!CanFire(s,target,high)))return false;
            int strength=0;
            foreach(var s in shooters)
            {
                strength+=RuleTables.MissileStrength(UnitTypes.Get(s).missile,board.Distance(s.hex,target.hex));
                strength+=FlankBonus(s,target,true);
            }
            int defense=Defense(target,false);
            var targetType=UnitTypes.Get(target);
            if(board.Hex(target.hex).woods && shooters.All(s=>UnitTypes.Get(s).missile=="B"))defense++;
            int column=RuleTables.MissileColumn(strength,defense);
            if(high)column--;
            if(shooters.Any(s=>StrategyEffects.PenalizesCombat(UnitTypes.Get(s).knight,Group(s).effect)))column--;
            if(StrategyEffects.PenalizesCombat(targetType.knight,Group(target).effect))column++;
            column=Math.Min(9,column);
            var fireResult=new MissileFireResult {
                shooterIds=shooters.Select(s=>s.id).ToArray(),
                shooterHexes=shooters.Select(s=>s.hex).ToArray(),
                targetId=target.id,targetHex=target.hex,strength=strength,defense=defense,
                high=high,targetStatusBefore=target.status,targetReducedBefore=target.reduced
            };
            foreach(var s in shooters)
            {
                s.fired=true; var t=UnitTypes.Get(s);
                if((s.side==Side.Norman && t.missile=="B") || (s.side==Side.Saxon && t.missile=="J"))
                {
                    var g=Group(s);if(!g.firedThisSegment)g.fireSegments++;
                    g.firedThisSegment=true;
                }
            }
            if(column<0)
            {
                fireResult.tableResult="-";fireResult.roll=0;
                fireResult.targetStatusAfter=target.status;
                fireResult.targetReducedAfter=target.reduced;
                lastFireResult=fireResult;
                Log("Fire on "+target.id+" at odds below 1:4: no effect");return true;
            }
            int die=Die();string result=RuleTables.Missile[die-1,column];
            Log(shooters.Count+" unit(s) fire on "+target.id+" ("+strength+":"+defense+") roll "+die+" → "+result);
            ApplyResult(target,result,false);
            fireResult.roll=die;fireResult.tableResult=result;
            fireResult.targetStatusAfter=target.status;
            fireResult.targetReducedAfter=target.reduced;
            lastFireResult=fireResult;
            CheckVictory();return true;
        }
        public bool CanMelee(UnitState attacker,UnitState defender)
        {
            return attacker.side!=defender.side && attacker.status==Status.Ready &&
                defender.status!=Status.Eliminated && !UnitTypes.Get(attacker).leader &&
                !UnitTypes.Get(defender).leader && Controls(attacker,defender.hex);
        }
        private List<UnitState> OpenMeleeTargets(UnitState attacker)
        {
            if(attacker==null || attacker.engaged || attacker.status!=Status.Ready ||
               UnitTypes.Get(attacker).leader)return new List<UnitState>();
            return Living(Opposite(attacker.side)).Where(defender=>!defender.engaged &&
                CanMelee(attacker,defender)).OrderBy(defender=>defender.hex).ToList();
        }
        public List<UnitState> MandatoryMeleeTargets(UnitState attacker)
        {
            if(attacker==null || OrderFor(attacker)==Order.ShieldWall)
                return new List<UnitState>();
            return OpenMeleeTargets(attacker).Where(defender=>Controls(defender,attacker.hex)).ToList();
        }
        public List<UnitState> RequiredMeleeAttackers(Side side)
        {
            return Living(side).Where(attacker=>MandatoryMeleeTargets(attacker).Count>0).ToList();
        }
        public List<UnitState> RequiredMeleeTargets(IEnumerable<UnitState> attackers)
        {
            var result=new Dictionary<string,UnitState>();
            foreach(var attacker in attackers)
            {
                var open=OpenMeleeTargets(attacker);
                var mandatory=MandatoryMeleeTargets(attacker);
                foreach(var defender in mandatory.Count>0?mandatory:open)
                    result[defender.id]=defender;
            }
            return result.Values.OrderBy(defender=>defender.hex).ToList();
        }
        private bool CanMeleeUnderObligation(UnitState attacker,UnitState defender)
        {
            if(!CanMelee(attacker,defender) || defender.engaged)return false;
            var mandatory=MandatoryMeleeTargets(attacker);
            return mandatory.Count==0 || mandatory.Any(target=>target.id==defender.id);
        }
        public bool Melee(List<UnitState> attackers,List<UnitState> defenders)
        {
            Side active=state.phase==Phase.NormanMelee?Side.Norman:
                state.phase==Phase.SaxonMelee?Side.Saxon:(Side)(-1);
            var required=RequiredMeleeTargets(attackers);
            var requiredIds=new HashSet<string>(required.Select(defender=>defender.id));
            var defenderIds=new HashSet<string>(defenders.Select(defender=>defender.id));
            if(active!=state.playerSide || attackers.Count==0 || defenders.Count==0 ||
               attackers.Select(a=>a.id).Distinct().Count()!=attackers.Count ||
                defenders.Select(d=>d.id).Distinct().Count()!=defenders.Count ||
               attackers.Any(a=>a.side!=active) ||
               attackers.Any(a=>!defenders.Any(d=>CanMeleeUnderObligation(a,d))) ||
                defenders.Any(d=>d.side!=Opposite(active) ||
                    !attackers.Any(a=>CanMeleeUnderObligation(a,d))) ||
                !requiredIds.SetEquals(defenderIds) ||
                attackers.Any(a=>a.engaged) || defenders.Any(d=>d.engaged))return false;
            ResolveMelee(attackers,defenders);return true;
        }
        private void ResolveMelee(List<UnitState> attackers,List<UnitState> defenders)
        {
            var attackerOrigins=attackers.ToDictionary(u=>u.id,u=>u.hex);
            var defenderOrigins=defenders.ToDictionary(u=>u.id,u=>u.hex);
            int attack=attackers.Sum(a=>defenders.Where(d=>CanMeleeUnderObligation(a,d))
                .Select(d=>Attack(a,d)).DefaultIfEmpty(0).Max());
            int defense=defenders.Sum(d=>Defense(d,true));
            int difference=attack-defense;
            var meleeResult=new MeleeCombatResult {
                attackerIds=attackers.Select(u=>u.id).ToArray(),
                attackerHexes=attackers.Select(u=>u.hex).ToArray(),
                defenderIds=defenders.Select(u=>u.id).ToArray(),
                defenderHexes=defenders.Select(u=>u.hex).ToArray(),
                attack=attack,defense=defense,difference=difference,
                attackerReducedBefore=attackers.Select(u=>u.reduced).ToArray(),
                defenderReducedBefore=defenders.Select(u=>u.reduced).ToArray(),
                attackerStatusBefore=attackers.Select(u=>u.status).ToArray(),
                defenderStatusBefore=defenders.Select(u=>u.status).ToArray()
            };
            int columnShift=0;
            if(attackers.Any(a=>StrategyEffects.PenalizesCombat(UnitTypes.Get(a).knight,Group(a).effect)))columnShift--;
            if(defenders.Any(d=>StrategyEffects.PenalizesCombat(UnitTypes.Get(d).knight,Group(d).effect)))columnShift++;
            int die=Die();string raw=RuleTables.MeleeResult(difference,die,columnShift);
            meleeResult.roll=die;meleeResult.tableResult=raw;
            Log(attackers.Count+" attacks "+defenders.Count+" at "+difference+", roll "+die+" → "+raw);
            var parts=raw.Split('/');
            if(parts[0].Contains('1'))ApplyResult(attackers[0],"1",true);
            else foreach(var a in attackers)ApplyResult(a,parts[0],true);
            if(parts[1].Contains('1'))ApplyResult(defenders[0],"1",true);
            else foreach(var d in defenders)
            {
                string result=parts[1];
                if(result.Contains('D') && attackers.Any(a=>OrderFor(a)==Order.Charge || a.charged || OrderFor(a)==Order.AttackPursue))result="R";
                ApplyResult(d,result,true);
            }
            if(parts[1].Contains('M') && parts[1].Contains('1'))
                foreach(var d in defenders.Where(d=>d.status!=Status.Eliminated))ApplyResult(d,"M",true);
            foreach(var defender in defenders.Where(d=>d.status==Status.Routed))
            {
                if(attackers.Any(a=>a.charged||OrderFor(a)==Order.Charge||OrderFor(a)==Order.AttackPursue))
                    RoutShock(defender,defenderOrigins[defender.id]);
                foreach(var attacker in attackers.Where(a=>a.status==Status.Ready &&
                    (a.charged||OrderFor(a)==Order.Charge||OrderFor(a)==Order.AttackPursue)))
                    Pursue(attacker,defender);
            }
            foreach(var attacker in attackers.Where(a=>a.status==Status.Routed))
            {
                if(defenders.Any(d=>OrderFor(d)==Order.AttackPursue||OrderFor(d)==Order.Charge||d.charged))
                    RoutShock(attacker,attackerOrigins[attacker.id]);
                foreach(var defender in defenders.Where(d=>d.status==Status.Ready &&
                    (d.charged||OrderFor(d)==Order.Charge||OrderFor(d)==Order.AttackPursue)))
                    Pursue(defender,attacker);
            }
            foreach(var a in attackers.Where(a=>a.charged && a.status!=Status.Eliminated))CheckMorale(a,true);
            foreach(var a in attackers)a.engaged=true;
            foreach(var d in defenders)d.engaged=true;
            meleeResult.attackerReducedAfter=attackers.Select(u=>u.reduced).ToArray();
            meleeResult.defenderReducedAfter=defenders.Select(u=>u.reduced).ToArray();
            meleeResult.attackerStatusAfter=attackers.Select(u=>u.status).ToArray();
            meleeResult.defenderStatusAfter=defenders.Select(u=>u.status).ToArray();
            lastMeleeResult=meleeResult;
            CheckVictory();
        }
        private void RoutShock(UnitState routed,string origin)
        {
            foreach(var friend in Living(routed.side).Where(u=>u!=routed && !UnitTypes.Get(u).leader &&
                board.Distance(u.hex,origin)==1).ToList())
            {
                var type=UnitTypes.Get(friend);int effect=Group(friend).effect;
                int roll=Die()+(StrategyEffects.PenalizesMoraleRoll(type.knight,effect)?1:0);
                char morale=type.morale;
                if(StrategyEffects.WorsensMorale(type.knight,effect) && morale<'E')morale++;
                var result=RuleTables.Morale(morale,roll);
                Log(friend.id+" rout shock morale "+morale+" roll "+roll+" → "+result);
                if(result==Status.Routed)Rout(friend);
            }
        }
        private void Pursue(UnitState pursuer,UnitState routed)
        {
            for(int step=0;step<board.Distance(pursuer.hex,routed.hex)+2;step++)
            {
                if(board.Distance(pursuer.hex,routed.hex)<=1 || InEnemyZoc(pursuer.side,pursuer.hex))break;
                var next=board.Adjacent(pursuer.hex)
                    .Where(h=>UnitAt(h,pursuer.side)==null && UnitAt(h,Opposite(pursuer.side))==null)
                    .OrderBy(h=>board.Distance(h,routed.hex)).ThenBy(h=>h).FirstOrDefault();
                if(next==null || board.Distance(next,routed.hex)>=board.Distance(pursuer.hex,routed.hex))break;
                pursuer.hex=next;TouchRoad(pursuer);
            }
            Log(pursuer.id+" pursues "+routed.id+" to "+pursuer.hex);
        }
        private int Attack(UnitState attacker,UnitState defender)
        {
            var type=UnitTypes.Get(attacker);var a=board.Hex(attacker.hex);var d=board.Hex(defender.hex);
            bool wall=OrderFor(attacker)==Order.ShieldWall;
            int value=wall?type.wallAttack:type.attack;
            value+=FlankBonus(attacker,defender,false)+LeaderBonus(attacker);
            var edge=board.Edge(attacker.hex,defender.hex);
            if(edge!=null && edge.ridge)
            {
                if(d.level>a.level)value-=type.knight?2:1;
                else if(d.level<a.level && !type.knight)value++;
            }
            bool downhill=type.knight && IsDownhill(attacker.hex,defender.hex);
            if(downhill)value+=attacker.charged?2:1;
            else if(attacker.charged)value++;
            return Math.Max(0,value);
        }
        private int Defense(UnitState unit,bool melee)
        {
            var type=UnitTypes.Get(unit);
            if(type.leader)return 1;
            bool wall=unit.status!=Status.Routed && OrderFor(unit)==Order.ShieldWall;
            int result=wall?type.wallDefense:type.defense;
            var h=board.Hex(unit.hex);
            if(h.marsh)result--;
            if(melee)
            {
                if(h.woods)result+=2;
                result+=LeaderBonus(unit);
            }
            return Math.Max(1,result);
        }
        private int FlankBonus(UnitState attacker,UnitState defender,bool fire)
        {
            int dir=board.Direction(defender.hex,attacker.hex);
            if(dir==defender.facing || dir==(defender.facing+1)%6)return 0;
            if(dir==(defender.facing+3)%6 || dir==(defender.facing+4)%6)return fire?1:2;
            return 1;
        }
        private int LeaderBonus(UnitState unit)
        {
            var unitType=UnitTypes.Get(unit);
            var leader=Living(unit.side).FirstOrDefault(candidate=>candidate.hex==unit.hex &&
                UnitTypes.Get(candidate).leader && candidate.leaderCondition==0 &&
                (unit.side==Side.Saxon || candidate.type=="William" ||
                    UnitTypes.Get(candidate).nation==unitType.nation));
            return leader==null?0:(leader.type=="William"||leader.type=="Harold"?2:1);
        }
        private void ApplyResult(UnitState unit,string result,bool melee)
        {
            if(unit.status==Status.Eliminated || result=="-")return;
            if(UnitTypes.Get(unit).leader)
            {
                if(result.Contains('1'))LeaderCasualty(unit,melee);
                else if(result.Contains('D'))ShakeLeader(unit);
                return;
            }
            if(result.Contains('1'))StepLoss(unit,melee);
            if(unit.status==Status.Eliminated)return;
            if(result.Contains('M'))CheckMorale(unit,false);
            if(result.Contains('R'))Rout(unit);
            else if(result.Contains('D') && unit.status!=Status.Routed)Disrupt(unit);
        }
        private void StepLoss(UnitState unit,bool melee)
        {
            if(!unit.reduced){unit.reduced=true;Log(unit.id+" loses a step");}
            else Eliminate(unit);
            foreach(var leader in Living(unit.side).Where(l=>l.hex==unit.hex && UnitTypes.Get(l).leader).ToList())
                LeaderCasualty(leader,melee);
        }
        private void Eliminate(UnitState unit)
        {
            unit.status=Status.Eliminated;
            if(unit.side==Side.Norman)state.normanCasualties+=RuleTables.CasualtyPoints(UnitTypes.Get(unit));
            else state.saxonCasualties+=RuleTables.CasualtyPoints(UnitTypes.Get(unit));
            Log(unit.id+" eliminated");
            if(!UnitTypes.Get(unit).leader)CheckExposedLeaders();
        }
        private void Disrupt(UnitState unit)
        {
            if(unit.status==Status.Ready){unit.status=Status.Disrupted;Log(unit.id+" disrupted");}
        }
        private void Rout(UnitState unit)
        {
            unit.status=Status.Routed;Log(unit.id+" routed");Retreat(unit,3);
        }
        public void Retreat(UnitState unit,int steps)
        {
            string lastOpenHex=unit.hex;
            for(int i=0;i<steps && unit.status!=Status.Eliminated;i++)
            {
                string prior=unit.hex;
                if((unit.side==Side.Saxon && prior.StartsWith("01")) ||
                   (unit.side==Side.Norman && !board.Adjacent(prior).Any(h=>board.Hex(h).y>board.Hex(prior).y)))
                {Eliminate(unit);break;}
                string next=board.Adjacent(unit.hex)
                    .Where(h=>UnitAt(h,Opposite(unit.side))==null && !InEnemyZoc(unit.side,h))
                    .OrderBy(h=>unit.side==Side.Saxon?
                        (board.Hex(h).y<board.Hex(prior).y?0:1):
                        (board.Hex(h).y>board.Hex(prior).y?0:1))
                    .ThenBy(h=>UnitAt(h,unit.side)==null?0:1)
                    .ThenBy(h=>unit.side==Side.Saxon?board.Hex(h).y:-board.Hex(h).y)
                    .ThenBy(h=>MoveCost(unit,unit.hex,h)).FirstOrDefault();
                if(next==null){BlockedRetreat(unit,lastOpenHex);break;}
                var occupied=UnitAt(next,unit.side);
                if(occupied!=null && i==steps-1)
                {
                    var displacement=new List<KeyValuePair<UnitState,string>>();
                    if(!BuildDisplacement(occupied,new HashSet<string>{prior},
                            new HashSet<string>(),displacement))
                    {BlockedRetreat(unit,lastOpenHex);break;}
                    foreach(var move in displacement)
                    {
                        move.Key.hex=move.Value;
                        if(move.Key.status!=Status.Eliminated)move.Key.status=Status.Disrupted;
                        TouchRoad(move.Key);Log(move.Key.id+" displaced to "+move.Value+" and disrupted");
                    }
                }
                unit.hex=next;TouchRoad(unit);
                FaceRear(unit);
                if(occupied!=null && i<steps-1)
                {
                    CheckMorale(occupied,false);
                    if(occupied.hex!=unit.hex || occupied.status==Status.Eliminated)
                        lastOpenHex=unit.hex;
                }
                else lastOpenHex=unit.hex;
            }
        }
        private bool BuildDisplacement(UnitState unit,HashSet<string> forbiddenHexes,
            HashSet<string> chain,List<KeyValuePair<UnitState,string>> moves)
        {
            if(!chain.Add(unit.id))return false;
            foreach(var destination in board.Adjacent(unit.hex)
                .Where(hex=>!forbiddenHexes.Contains(hex) &&
                    UnitAt(hex,Opposite(unit.side))==null && !InEnemyZoc(unit.side,hex))
                .OrderBy(hex=>UnitAt(hex,unit.side)==null?0:1)
                .ThenBy(hex=>MoveCost(unit,unit.hex,hex)).ThenBy(hex=>hex))
            {
                var blocker=UnitAt(destination,unit.side);
                if(blocker==null || BuildDisplacement(blocker,forbiddenHexes,chain,moves))
                {
                    moves.Add(new KeyValuePair<UnitState,string>(unit,destination));
                    return true;
                }
            }
            chain.Remove(unit.id);return false;
        }
        private void BlockedRetreat(UnitState unit,string lastOpenHex)
        {
            unit.hex=lastOpenHex;StepLoss(unit,true);
            if(unit.status==Status.Eliminated)return;
            unit.status=Status.Disrupted;
            Log(unit.id+" blocked in retreat and disrupted");
        }
        private void FaceRear(UnitState unit)
        {
            string rear=board.Adjacent(unit.hex)
                .OrderBy(hex=>unit.side==Side.Saxon?board.Hex(hex).y:-board.Hex(hex).y)
                .ThenBy(hex=>hex).FirstOrDefault();
            if(rear!=null)unit.facing=board.Direction(unit.hex,rear);
        }
        private void CheckMorale(UnitState unit,bool routIsDisrupt)
        {
            if(unit.status==Status.Eliminated)return;
            var type=UnitTypes.Get(unit);
            char morale=type.morale;
            int effect=Group(unit).effect;
            if(StrategyEffects.WorsensMorale(type.knight,effect) && morale<'E')morale++;
            int roll=Die()+(StrategyEffects.PenalizesMoraleRoll(type.knight,effect)?1:0);
            var result=RuleTables.Morale(morale,roll);
            Log(unit.id+" morale "+morale+" roll "+roll+" → "+result);
            if(result==Status.Routed && !routIsDisrupt)Rout(unit);
            else if(result!=Status.Ready)Disrupt(unit);
        }
        private void Rally(Side side)
        {
            foreach(var unit in Living(side).Where(u=>u.status==Status.Disrupted || u.status==Status.Routed).ToList())
            {
                bool nearby=Living(side).Any(l=>UnitTypes.Get(l).leader && l.leaderCondition==0 &&
                    (l.type=="William" || side==Side.Saxon || UnitTypes.Get(l).nation==UnitTypes.Get(unit).nation) &&
                    board.Distance(l.hex,unit.hex)<=Math.Max(0,UnitTypes.Get(l).rally-l.leaderPenalty));
                if(unit.status==Status.Routed)
                {
                    if(nearby){unit.status=Status.Ready;Log(unit.id+" rallies from rout");}
                    else Retreat(unit,2);
                }
                else
                {
                    int roll=Math.Max(1,Die()-(nearby?1:0));
                    var type=UnitTypes.Get(unit);char morale=type.morale;
                    if(StrategyEffects.WorsensMorale(type.knight,Group(unit).effect) && morale<'E')morale++;
                    if(RuleTables.Rally(morale,roll))
                    {unit.status=Status.Ready;Log(unit.id+" rallies");}
                }
            }
        }
        private void LeaderCasualty(UnitState leader,bool melee)
        {
            ApplyLeaderCasualtyRoll(leader,melee,Die()+Die());
        }
        public void ApplyLeaderCasualtyRoll(UnitState leader,bool melee,int roll,
            int repeatedShakenRoll=0)
        {
            if(leader==null || leader.status==Status.Eliminated ||
               !UnitTypes.Get(leader).leader)return;
            bool injured=false;
            if(roll==2 || roll==12){Eliminate(leader);injured=true;}
            else if(roll==3 || (!melee && roll==11))
            {
                if(LeaderWounded(leader))Eliminate(leader);
                else{leader.leaderPenalty++;Log(leader.id+" wounded: command and rally reduced");}
                injured=true;
            }
            else if(melee && (roll==4||roll==10||roll==11))
            {
                if(LeaderWounded(leader))Eliminate(leader);
                else{leader.leaderCondition=2;Log(leader.id+" ineffective through this assault");}
                injured=true;
            }
            else if(melee && (roll==5||roll==9))
            {
                if(leader.leaderCondition==1)
                {
                    int shakeRoll=repeatedShakenRoll>0?repeatedShakenRoll:Die();
                    if(shakeRoll<=3){Eliminate(leader);injured=true;}
                    else Log(leader.id+" survives a second shaken result");
                }
                else if(leader.leaderCondition!=2)ShakeLeader(leader);
            }
            if((leader.type=="William"||leader.type=="Harold") && injured)
                foreach(var unit in Living(leader.side).Where(u=>!UnitTypes.Get(u).leader &&
                    board.Distance(u.hex,leader.hex)<=UnitTypes.Get(leader).command).ToList())CheckMorale(unit,false);
        }
        private static bool LeaderWounded(UnitState leader)
        {
            return leader.leaderPenalty>0 || leader.leaderCondition==2;
        }
        private void ShakeLeader(UnitState leader)
        {
            if(leader.leaderCondition==2)return;
            if(leader.leaderCondition==1)
            {
                if(Die()<=3)Eliminate(leader);
                return;
            }
            leader.leaderCondition=1;leader.shakenUntil=state.turn+1;
            Log(leader.id+" shaken");
        }
        public void CheckVictory()
        {
            if(state.phase==Phase.GameOver)return;
            bool road=state.road.Count>0 && state.road.All(r=>r.owner==Side.Norman);
            bool housecarls=state.units.Where(u=>u.type=="HC").All(u=>u.status==Status.Eliminated);
            if(!road && !housecarls)return;
            bool worse=state.normanCasualties>state.saxonCasualties;
            state.result=road?(worse?"Norman tactical victory":"Norman strategic victory"):
                (worse?"Draw":"Norman strategic victory");
            state.phase=Phase.GameOver;Log(state.result);
        }
    }
}
