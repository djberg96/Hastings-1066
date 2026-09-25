using System;
using System.Collections.Generic;
using System.Linq;

namespace Hastings
{
    public sealed class MoveOption
    {
        public string destination;
        public int cost;
        public List<string> path;
        public bool charge;
    }

    public sealed partial class GameEngine
    {
        public readonly Board board;
        public GameState state;
        public MissileFireResult lastFireResult;
        public MeleeCombatResult lastMeleeResult;
        public GameEngine(Board board, GameState state) { this.board=board; this.state=state; }
        public IEnumerable<UnitState> Living(Side side) { return state.units.Where(u=>u.side==side && u.status!=Status.Eliminated && board.Has(u.hex)); }
        public UnitState UnitAt(string hex, Side? side=null, bool leader=false)
        {
            return state.units.FirstOrDefault(u=>u.hex==hex && u.status!=Status.Eliminated &&
                (!side.HasValue || u.side==side.Value) && UnitTypes.Get(u).leader==leader);
        }
        public GroupState Group(UnitState unit) { return state.groups.First(g=>g.id==unit.group); }
        public Order OrderFor(UnitState unit)
        {
            var type=UnitTypes.Get(unit);
            if(type.leader)return Order.Advance;
            if(unit.side==Side.Saxon && unit.reserveOrder)return Order.Advance;
            if(type.guard && Living(Side.Norman).Any(u=>u.type=="William")) return Order.Advance;
            var order=type.knight?Group(unit).knightOrder:Group(unit).footOrder;
            if(type.missile=="B" && order==Order.ShieldWall)return Order.FireInPlace;
            return order;
        }
        public int Die()
        {
            uint x=state.randomState; if(x==0)x=1;
            x^=x<<13; x^=x>>17; x^=x<<5; state.randomState=x;
            return (int)(x%6)+1;
        }
        public void Log(string message)
        {
            state.log.Add($"I{state.period} T{state.turn}: {message}");
            if(state.log.Count>250)state.log.RemoveAt(0);
        }
        public void Begin()
        {
            if(state.phase!=Phase.Setup) return;
            state.phase=Phase.Orders; Log("Choose "+state.playerSide+" strategies.");
        }
        public void SetStrategy(string group, Strategy strategy)
        {
            bool norman=new[]{"Norman","Breton","Franco-Flemish"}.Contains(group);
            bool saxon=new[]{"Left","Center","Right"}.Contains(group);
            if(state.phase!=Phase.Orders ||
               (state.playerSide==Side.Norman?!norman:!saxon))return;
            state.groups.First(g=>g.id==group).strategy=strategy;
        }
        public bool OptionsPending() { return state.groups.Any(g=>g.footOptional||g.knightOptional); }
        public void ResolveOrders()
        {
            if(state.phase!=Phase.Orders)return;
            if(state.orderResults==null)state.orderResults=new List<OrderRollResult>();
            else state.orderResults.Clear();
            ReassignSaxonWings();
            foreach(var group in state.groups)
            {
                if((group.id=="Left"||group.id=="Center"||group.id=="Right") &&
                    !Living(Side.Saxon).Any(u=>u.group==group.id && !UnitTypes.Get(u).leader))continue;
                var side=group.id=="Left"||group.id=="Center"||group.id=="Right"?Side.Saxon:Side.Norman;
                if(side!=state.playerSide)
                    group.strategy=side==Side.Saxon?ChooseSaxonStrategy(group.id):
                        ChooseNormanStrategy(group.id);
                int footDuration=1, knightDuration=1, footEffect=0, knightEffect=0;
                bool footOptional=false, knightOptional=false;
                bool footContinued=group.footDuration>1;
                bool knightContinued=side==Side.Norman && group.knightDuration>1;
                bool needsRoll=!footContinued || (side==Side.Norman && !knightContinued);
                int orderRoll=needsRoll?Die()+Die():0;
                int footRoll=footContinued?0:orderRoll;
                int knightRoll=side==Side.Norman&&!knightContinued?orderRoll:0;
                if(footContinued){group.footDuration--;footEffect=group.footPendingEffect;}
                else
                {
                    group.footOrder=RuleTables.RollOrder(side,false,group.strategy,footRoll,
                        out footDuration,out footEffect,out footOptional);
                    group.footPendingEffect=footDuration>1?footEffect:0;
                }
                if(side==Side.Norman)
                {
                    if(knightContinued){group.knightDuration--;knightEffect=group.knightPendingEffect;}
                    else
                    {
                        group.knightOrder=RuleTables.RollOrder(side,true,group.strategy,knightRoll,
                            out knightDuration,out knightEffect,out knightOptional);
                        group.knightPendingEffect=knightDuration>1?knightEffect:0;
                    }
                }
                group.footDuration=Math.Max(group.footDuration,footDuration);
                group.knightDuration=Math.Max(group.knightDuration,knightDuration);
                group.footOptional=footOptional && side==state.playerSide;
                group.knightOptional=knightOptional && side==state.playerSide;
                if(footOptional && side!=state.playerSide)
                    group.footOrder=ChooseOptionalOrder(side,false,group.strategy);
                if(knightOptional && side!=state.playerSide)
                    group.knightOrder=ChooseOptionalOrder(side,true,group.strategy);
                group.effect+=footEffect+knightEffect;
                state.orderResults.Add(new OrderRollResult {
                    group=group.id,side=side,strategy=group.strategy,roll=orderRoll,
                    footRoll=footRoll,knightRoll=knightRoll,
                    footOrder=group.footOrder,knightOrder=group.knightOrder,
                    footDuration=group.footDuration,knightDuration=group.knightDuration,
                    effectChange=footEffect+knightEffect,totalEffect=group.effect,
                    hasKnights=side==Side.Norman,footOptional=footOptional,
                    knightOptional=knightOptional,footContinued=footContinued,
                    knightContinued=knightContinued
                });
                Log(group.id+" chooses "+group.strategy+", "+
                    (orderRoll>0?"rolls "+orderRoll+"; ":"")+
                    (footContinued?"foot continues "+group.footOrder:
                        "foot: "+group.footOrder)+
                    (side==Side.Norman?(knightContinued?"; knights continue "+group.knightOrder:
                        "; knights: "+group.knightOrder):"")+
                    "; effect "+group.effect);
            }
            Rally(Side.Norman);
            state.phase=Phase.NormanFire;
        }
        public bool SetOptionalOrder(string groupId,bool knight,Order order)
        {
            if(state.phase!=Phase.NormanFire)return false;
            var group=state.groups.FirstOrDefault(g=>g.id==groupId);
            if(group==null)return false;
            var side=groupId=="Left"||groupId=="Center"||groupId=="Right"?
                Side.Saxon:Side.Norman;
            if(side!=state.playerSide)return false;
            if(knight)
            {
                if(!group.knightOptional || (order!=Order.Hold && order!=Order.Advance && order!=Order.Charge))return false;
                group.knightOrder=order;group.knightOptional=false;
            }
            else
            {
                bool legal=side==Side.Saxon?
                    order==Order.ShieldWall||order==Order.AttackPursue||order==Order.Advance:
                    order==Order.ShieldWall||order==Order.FireInPlace||order==Order.Advance;
                if(!group.footOptional || !legal)return false;
                group.footOrder=order;group.footOptional=false;
            }
            var result=state.orderResults==null?null:
                state.orderResults.LastOrDefault(r=>r.group==groupId);
            if(result!=null)
            {
                if(knight)result.knightOrder=order;
                else result.footOrder=order;
            }
            Log(groupId+" chooses optional "+(knight?"knight":"foot")+" order: "+order);return true;
        }
        private Strategy ChooseSaxonStrategy(string wing)
        {
            var units=Living(Side.Saxon).Where(u=>u.group==wing && !UnitTypes.Get(u).leader).ToList();
            int threats=units.Count(u=>Living(Side.Norman).Any(n=>board.Distance(u.hex,n.hex)<=2));
            if(threats>units.Count/3)return Strategy.Defensive;
            return state.turn<4?Strategy.Cautious:Strategy.Moderate;
        }
        private Strategy ChooseNormanStrategy(string group)
        {
            var units=Living(Side.Norman).Where(u=>u.group==group && !UnitTypes.Get(u).leader).ToList();
            int close=units.Count(u=>NearestEnemyDistance(Side.Norman,u.hex)<=3);
            if(close>units.Count/2)return Strategy.Aggressive;
            return state.turn<3?Strategy.Moderate:Strategy.Aggressive;
        }
        private static Order ChooseOptionalOrder(Side side,bool knight,Strategy strategy)
        {
            if(side==Side.Saxon)
                return strategy==Strategy.Defensive?Order.ShieldWall:
                    strategy==Strategy.Aggressive?Order.AttackPursue:Order.Advance;
            if(knight)return strategy==Strategy.Aggressive?Order.Charge:
                strategy==Strategy.Defensive?Order.Hold:Order.Advance;
            return strategy==Strategy.Defensive?Order.ShieldWall:
                strategy==Strategy.Cautious?Order.FireInPlace:Order.Advance;
        }
        private void ReassignSaxonWings()
        {
            var leaders=Living(Side.Saxon).Where(u=>UnitTypes.Get(u).leader && u.leaderCondition==0).ToList();
            var units=Living(Side.Saxon).Where(u=>!UnitTypes.Get(u).leader).ToList();
            if(leaders.Count==0)
            {foreach(var unit in units){unit.group="Center";unit.reserveOrder=false;}return;}
            foreach(var unit in units)
            {
                var near=leaders.OrderBy(l=>board.Distance(l.hex,unit.hex)).First();
                bool commanded=board.Distance(near.hex,unit.hex)<=Math.Max(0,UnitTypes.Get(near).command-near.leaderPenalty);
                if(unit.reserveOrder && !commanded)continue;
                unit.reserveOrder=false;unit.group=near.group;
            }
            int minimum=(int)Math.Ceiling(units.Count*(leaders.Count<3?1.0/3:1.0/5));
            foreach(var leader in leaders)
            {
                int count=units.Count(u=>u.group==leader.group);
                if(count>=minimum)continue;
                var donors=units.Where(u=>!u.reserveOrder && u.group!=leader.group &&
                    units.Count(v=>v.group==u.group)>minimum)
                    .OrderBy(u=>board.Distance(u.hex,leader.hex)).ToList();
                foreach(var donor in donors)
                {donor.group=leader.group;count++;if(count>=minimum)break;}
            }
        }
        public bool CanFace(UnitState unit)
        {
            if(unit.side!=state.playerSide)return false;
            if(state.phase==Phase.Setup)return state.playerSide==Side.Norman;
            return (unit.side==Side.Norman &&
                    (state.phase==Phase.NormanMove||state.phase==Phase.Reform)) ||
                   (unit.side==Side.Saxon && state.phase==Phase.SaxonMove);
        }
        public bool Face(UnitState unit,int direction)
        {
            if(UnitTypes.Get(unit).leader || !CanFace(unit))return false;
            unit.facing=((direction%6)+6)%6; Log(unit.id+" faces "+unit.facing);return true;
        }
        public bool Controls(UnitState unit,string hex)
        {
            if(unit.status!=Status.Ready || UnitTypes.Get(unit).leader || !board.Has(unit.hex) || !board.Adjacent(unit.hex).Contains(hex))return false;
            int dir=board.Direction(unit.hex,hex);
            return dir==unit.facing || dir==(unit.facing+1)%6;
        }
        public bool InEnemyZoc(Side side,string hex)
        {
            return Living(side==Side.Norman?Side.Saxon:Side.Norman).Any(u=>Controls(u,hex));
        }
        public int MovementAllowance(UnitState unit)
        {
            var type=UnitTypes.Get(unit);
            if(type.leader)return unit.leaderCondition>0?2:6;
            if(unit.status!=Status.Ready)return 0;
            var order=OrderFor(unit);
            if(order==Order.ShieldWall || order==Order.Hold || order==Order.FireInPlace)return 1;
            int result=type.knight?(order==Order.Charge?6:4):3;
            result-=StrategyEffects.MovementPenalty(type.knight,Group(unit).effect);
            return Math.Max(0,result-unit.entrySpent);
        }
        private int MoveCost(UnitState unit,string from,string to)
        {
            var type=UnitTypes.Get(unit);var hex=board.Hex(to);var edge=board.Edge(from,to);
            int cost=hex.woods||hex.marsh?(type.knight?3:2):1;
            if(edge!=null && edge.stream)cost++;
            return cost;
        }
        public Dictionary<string,MoveOption> LegalMoves(UnitState unit,bool reaction=false)
        {
            var result=new Dictionary<string,MoveOption>();
            if(!board.Has(unit.hex)||unit.status==Status.Eliminated)return result;
            if(reaction)
            {
                if(unit.reacted || unit.status==Status.Routed || !InCommand(unit))return result;
                if(unit.status==Status.Disrupted && !InEnemyZoc(unit.side,unit.hex))return result;
                var order=OrderFor(unit);
                if(order==Order.ShieldWall||order==Order.AttackPursue||order==Order.Charge)return result;
                if(unit.side==Side.Saxon && !UnitTypes.Get(unit).knight &&
                   Living(Side.Norman).Any(n=>UnitTypes.Get(n).knight && Controls(n,unit.hex)))return result;
                int before=NearestEnemyDistance(unit.side,unit.hex);
                foreach(var to in board.Adjacent(unit.hex))
                    if(UnitAt(to,unit.side)==null && UnitAt(to,Opposite(unit.side))==null &&
                       !InEnemyZoc(unit.side,to) && NearestEnemyDistance(unit.side,to)>before)
                        result[to]=new MoveOption{destination=to,cost=0,path=new List<string>{unit.hex,to}};
                return result;
            }
            if(unit.moved || unit.status!=Status.Ready)return result;
            int allowance=MovementAllowance(unit);var initial=OrderFor(unit);
            var type=UnitTypes.Get(unit);
            var enemySide=Opposite(unit.side);
            var friendlyOccupied=new HashSet<string>(Living(unit.side)
                .Where(other=>!UnitTypes.Get(other).leader).Select(other=>other.hex));
            var allEnemyUnits=Living(enemySide).ToList();
            var enemyUnits=allEnemyUnits.Where(other=>!UnitTypes.Get(other).leader).ToList();
            var enemyOccupied=new HashSet<string>(enemyUnits.Select(other=>other.hex));
            var enemyZoc=new HashSet<string>();
            foreach(var enemy in enemyUnits.Where(enemy=>enemy.status==Status.Ready))
                foreach(var adjacent in board.Adjacent(enemy.hex))
                    if(Controls(enemy,adjacent))enemyZoc.Add(adjacent);
            var distanceCache=new Dictionary<string,int>();
            Func<string,int> enemyDistance=hex=>
            {
                int distance;
                if(distanceCache.TryGetValue(hex,out distance))return distance;
                distance=enemyUnits.Count==0?999:enemyUnits.Min(enemy=>board.Distance(hex,enemy.hex));
                distanceCache[hex]=distance;return distance;
            };
            bool canCharge=type.knight && (initial==Order.Charge || type.guard);
            var enemyAdjacent=new HashSet<string>();
            if(canCharge)foreach(var enemy in allEnemyUnits)
                foreach(var adjacent in board.Adjacent(enemy.hex))enemyAdjacent.Add(adjacent);
            var uphillCache=new Dictionary<string,bool>();
            var open=new Queue<MoveOption>();open.Enqueue(new MoveOption{destination=unit.hex,cost=0,path=new List<string>{unit.hex}});
            var best=new Dictionary<string,int>{{unit.hex,0}};
            while(open.Count>0)
            {
                var current=open.Dequeue();
                foreach(var to in board.Adjacent(current.destination))
                {
                    if(enemyOccupied.Contains(to))continue;
                    if(current.path.Contains(to))continue;
                    bool currentZoc=enemyZoc.Contains(current.destination);
                    if(currentZoc&&current.destination!=unit.hex)continue;
                    bool zoc=enemyZoc.Contains(to);
                    if(currentZoc&&zoc &&
                       !(initial==Order.AttackPursue && current.destination==unit.hex))continue;
                    if(zoc && (!type.leader && type.missile=="B"))continue;
                    if(zoc && friendlyOccupied.Contains(to))continue;
                    var crossing=board.Edge(current.destination,to);
                    if(type.knight && friendlyOccupied.Contains(to) &&
                        ((crossing!=null && crossing.ridge)||board.Hex(to).marsh))continue;
                    if(type.leader && zoc && !friendlyOccupied.Contains(to))continue;
                    int cost=(initial==Order.ShieldWall||initial==Order.Hold||initial==Order.FireInPlace)?
                        1:MoveCost(unit,current.destination,to)+current.cost;
                    if(cost>allowance)continue;
                    if(initial==Order.ShieldWall || initial==Order.Hold || initial==Order.FireInPlace)
                    {
                        if(current.path.Count>1)continue;
                        if(zoc)continue;
                        int before=enemyDistance(unit.hex),after=enemyDistance(to);
                        if((initial==Order.ShieldWall||initial==Order.Hold) && after<=before)continue;
                        if(initial==Order.FireInPlace && after==before)continue;
                    }
                    if(best.ContainsKey(to) && best[to]<=cost)continue;
                    best[to]=cost;
                    var path=new List<string>(current.path){to};
                    var option=new MoveOption{destination=to,cost=cost,path=path,
                        charge=canCharge && ChargePath(path,enemyAdjacent,uphillCache)};
                    if(!friendlyOccupied.Contains(to) || type.leader)result[to]=option;
                    if(!zoc)open.Enqueue(option);
                }
            }
            if(initial==Order.Charge && result.Count>0)
            {
                int starting=enemyDistance(unit.hex);
                int closest=result.Values.Min(option=>enemyDistance(option.destination));
                if(closest>=starting)result.Clear();
                else foreach(var destination in result.Where(pair=>
                        enemyDistance(pair.Value.destination)>closest)
                        .Select(pair=>pair.Key).ToList())result.Remove(destination);
            }
            if(initial==Order.AttackPursue && result.Count>0)
            {
                int starting=enemyDistance(unit.hex);
                int closest=starting==1?1:
                    result.Values.Min(option=>enemyDistance(option.destination));
                foreach(var destination in result.Where(pair=>
                        enemyDistance(pair.Value.destination)>closest)
                        .Select(pair=>pair.Key).ToList())result.Remove(destination);
            }
            return result;
        }
        private bool ChargePath(List<string> path,HashSet<string> enemyAdjacent,
            Dictionary<string,bool> uphillCache)
        {
            if(path.Count<2 || !enemyAdjacent.Contains(path[path.Count-1]))return false;
            for(int i=1;i<path.Count;i++)
            {
                var e=board.Edge(path[i-1],path[i]);var h=board.Hex(path[i]);
                if((e!=null && (e.ridge||e.stream))||h.woods||h.marsh)return false;
                if(i>=path.Count-2)
                {
                    string key=path[i-1]+">"+path[i];bool uphill;
                    if(!uphillCache.TryGetValue(key,out uphill))
                    {uphill=IsUphill(path[i-1],path[i]);uphillCache[key]=uphill;}
                    if(uphill)return false;
                }
            }
            return true;
        }
        private bool IsUphill(string from,string to)
        {
            var origin=board.Hex(from);var target=board.Hex(to);
            if(target.level>origin.level)return true;
            foreach(var hill in board.data.hexes)
            {
                if(hill.level<=origin.level || board.Distance(from,hill.id)>2)continue;
                if(hill.id=="1419" || hill.level==5)continue;
                if(board.Distance(to,hill.id)<board.Distance(from,hill.id))return true;
            }
            return false;
        }
        private bool IsDownhill(string from,string to) { return IsUphill(to,from); }
        public bool Move(UnitState unit,string destination,bool reaction=false)
        {
            if(!board.Has(destination) || unit.side!=state.playerSide)return false;
            if(reaction && !((unit.side==Side.Norman && state.phase==Phase.NormanReaction) ||
               (unit.side==Side.Saxon && state.phase==Phase.SaxonReaction)))return false;
            if(!reaction && !((unit.side==Side.Norman && state.phase==Phase.NormanMove) ||
               (unit.side==Side.Saxon && state.phase==Phase.SaxonMove)))return false;
            MoveOption option;
            if(!LegalMoves(unit,reaction).TryGetValue(destination,out option))return false;
            MoveCore(unit,option,reaction);return true;
        }
        private void MoveCore(UnitState unit,MoveOption option,bool reaction)
        {
            string origin=unit.hex;
            string lastOpenHex=origin;
            foreach(var step in option.path.Skip(1))
            {
                var e=board.Edge(unit.hex,step);
                if(UnitTypes.Get(unit).knight && e!=null && e.ridge)
                {
                    CheckMorale(unit,true);
                    if(unit.status!=Status.Ready){unit.hex=lastOpenHex;break;}
                }
                unit.hex=step;
                if(UnitTypes.Get(unit).knight && board.Hex(step).marsh)
                {
                    CheckMorale(unit,true);
                    if(unit.status!=Status.Ready)
                    {
                        if(Living(unit.side).Any(other=>other!=unit &&
                            !UnitTypes.Get(other).leader && other.hex==step))unit.hex=lastOpenHex;
                        TouchRoad(unit);break;
                    }
                }
                if(!Living(unit.side).Any(other=>other!=unit &&
                    !UnitTypes.Get(other).leader && other.hex==step))lastOpenHex=step;
                TouchRoad(unit);
                if(InEnemyZoc(unit.side,unit.hex))break;
            }
            unit.charged=!reaction && unit.status==Status.Ready && option.charge && unit.hex==option.destination;
            if(reaction){unit.reacted=true;CheckMorale(unit,false);}
            else unit.moved=true;
            if(unit.side==Side.Saxon && unit.reserveOrder)
            {
                var commander=Living(Side.Saxon).Where(u=>UnitTypes.Get(u).leader &&
                    u.leaderCondition==0 && board.Distance(u.hex,unit.hex)<=
                    Math.Max(0,UnitTypes.Get(u).command-u.leaderPenalty))
                    .OrderBy(u=>board.Distance(u.hex,unit.hex)).FirstOrDefault();
                if(commander!=null){unit.reserveOrder=false;unit.group=commander.group;}
            }
            Log(unit.id+" moves "+origin+" → "+unit.hex+(unit.charged?" (charge)":""));
            CheckExposedLeaders();
            CheckVictory();
        }
        private void TouchRoad(UnitState unit)
        {
            var road=state.road.FirstOrDefault(r=>r.hex==unit.hex);
            if(road!=null)road.owner=unit.side;
        }
        public int NearestEnemyDistance(Side side,string hex)
        {
            var enemies=Living(Opposite(side)).Where(u=>!UnitTypes.Get(u).leader).ToList();
            return enemies.Count==0?999:enemies.Min(u=>board.Distance(hex,u.hex));
        }
        public static Side Opposite(Side side) { return side==Side.Norman?Side.Saxon:Side.Norman; }
        private bool InCommand(UnitState unit)
        {
            return Living(unit.side).Any(l=>UnitTypes.Get(l).leader && l.leaderCondition==0 &&
                (l.side==Side.Saxon || l.type=="William" || UnitTypes.Get(l).nation==UnitTypes.Get(unit).nation) &&
                board.Distance(l.hex,unit.hex)<=Math.Max(0,UnitTypes.Get(l).command-l.leaderPenalty));
        }
        private void CheckExposedLeaders()
        {
            foreach(var leader in state.units.Where(u=>u.status!=Status.Eliminated &&
                UnitTypes.Get(u).leader && board.Has(u.hex)).ToList())
            {
                if(UnitAt(leader.hex,leader.side)!=null)continue;
                if(!Living(Opposite(leader.side)).Any(e=>!UnitTypes.Get(e).leader &&
                    board.Distance(e.hex,leader.hex)==1))continue;
                var visited=new HashSet<string>{leader.hex};
                bool escaped=true;
                for(int i=0;i<3;i++)
                {
                    var next=board.Adjacent(leader.hex).Where(h=>!visited.Contains(h) &&
                        UnitAt(h,Opposite(leader.side))==null &&
                        (!InEnemyZoc(leader.side,h)||UnitAt(h,leader.side)!=null))
                        .OrderByDescending(h=>NearestEnemyDistance(leader.side,h)).ThenBy(h=>h).FirstOrDefault();
                    if(next==null){escaped=false;break;}
                    leader.hex=next;visited.Add(next);
                }
                if(!escaped)Eliminate(leader);
                else Log(leader.id+" retreats to "+leader.hex);
            }
        }
    }
}
