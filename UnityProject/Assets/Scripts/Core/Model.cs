using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hastings
{
    public enum Side { Norman, Saxon }
    public enum Phase { Setup, Orders, NormanFire, NormanMove, NormanMelee, NormanReaction,
        NormanDefenseFire, Reform, GameOver, SaxonReaction, SaxonDefenseFire, SaxonFire,
        SaxonMove, SaxonMelee }
    public enum Status { Ready, Disrupted, Routed, Eliminated }
    public enum Strategy { Defensive, Cautious, Moderate, Aggressive }
    public enum Order { ShieldWall, FireInPlace, Advance, AttackPursue, Hold, Charge }

    [Serializable] public class HexData
    {
        public string id;
        public float x, y;
        public int level;
        public bool woods, marsh, road;
    }
    [Serializable] public class EdgeData
    {
        public string a, b;
        public bool stream, ridge;
    }
    [Serializable] public class MapData
    {
        public int width, height;
        public HexData[] hexes;
        public EdgeData[] edges;
    }
    [Serializable] public class UnitState
    {
        public string id, type, hex, group;
        public Side side;
        public int facing;
        public bool reduced, moved, fired, reacted, charged, engaged;
        public Status status;
        public int leaderPenalty, leaderCondition, shakenUntil;
        public int reservePeriod, reserveTurn, entrySpent;
        public bool reserveOrder;
    }
    [Serializable] public class GroupState
    {
        public string id;
        public Strategy strategy;
        public Order footOrder, knightOrder;
        public int footDuration, knightDuration, effect, fireSegments, carry;
        public int footPendingEffect, knightPendingEffect;
        public bool firedThisSegment;
        public bool footOptional, knightOptional;
    }
    [Serializable] public class RoadState
    {
        public string hex;
        public Side owner;
    }
    [Serializable] public class OrderRollResult
    {
        public string group;
        public Side side;
        public Strategy strategy;
        public int roll, footRoll, knightRoll;
        public Order footOrder, knightOrder;
        public int footDuration, knightDuration;
        public int effectChange, totalEffect;
        public bool hasKnights, footOptional, knightOptional;
        public bool footContinued, knightContinued;
    }
    public sealed class MissileFireResult
    {
        public string[] shooterIds, shooterHexes;
        public string targetId, targetHex, tableResult;
        public int strength, defense, roll;
        public bool high, targetReducedBefore, targetReducedAfter;
        public Status targetStatusBefore, targetStatusAfter;
    }
    public sealed class MeleeCombatResult
    {
        public string[] attackerIds, attackerHexes, defenderIds, defenderHexes;
        public int attack, defense, difference, roll;
        public string tableResult;
        public bool[] attackerReducedBefore, attackerReducedAfter;
        public bool[] defenderReducedBefore, defenderReducedAfter;
        public Status[] attackerStatusBefore, attackerStatusAfter;
        public Status[] defenderStatusBefore, defenderStatusAfter;
    }
    [Serializable] public class GameState
    {
        public int saveVersion = 1;
        public int period = 1, turn = 1, extendedTo = 8;
        public Phase phase = Phase.Setup;
        public Side playerSide = Side.Norman;
        public uint randomState;
        public List<UnitState> units = new List<UnitState>();
        public List<GroupState> groups = new List<GroupState>();
        public List<RoadState> road = new List<RoadState>();
        public List<OrderRollResult> orderResults = new List<OrderRollResult>();
        public int normanCasualties, saxonCasualties;
        public string result = "";
        public List<string> log = new List<string>();
    }

    public sealed class UnitType
    {
        public readonly string id, art, nation, missile;
        public readonly Side side;
        public readonly int attack, defense, wallAttack, wallDefense, command, rally;
        public readonly char morale;
        public readonly bool knight, leader, guard;
        public UnitType(string id, Side side, string nation, string art, int attack, int defense,
                        int wallAttack, int wallDefense, char morale, string missile = "",
                        bool knight = false, bool leader = false, int command = 0, int rally = 0,
                        bool guard = false)
        {
            this.id = id; this.side = side; this.nation = nation; this.art = art;
            this.attack = attack; this.defense = defense;
            this.wallAttack = wallAttack; this.wallDefense = wallDefense;
            this.morale = morale; this.missile = missile;
            this.knight = knight; this.leader = leader;
            this.command = command; this.rally = rally; this.guard = guard;
        }
    }

    public static class UnitTypes
    {
        private static readonly Dictionary<string, UnitType> Types = new Dictionary<string, UnitType>();
        static UnitTypes()
        {
            Add(new UnitType("BB", Side.Norman, "Breton", "Breton_Bowmen", 1, 2, 1, 2, 'C', "B"));
            Add(new UnitType("BF", Side.Norman, "Breton", "Breton_Infantry", 4, 4, 2, 6, 'D'));
            Add(new UnitType("BK", Side.Norman, "Breton", "Breton_Cavalry", 8, 6, 8, 6, 'B', knight:true));
            Add(new UnitType("NB", Side.Norman, "Norman", "Norman_Bowmen", 1, 2, 1, 2, 'C', "B"));
            Add(new UnitType("NF", Side.Norman, "Norman", "Norman_Infantry", 4, 4, 2, 6, 'C'));
            Add(new UnitType("NK", Side.Norman, "Norman", "Norman_Cavalry", 8, 6, 8, 6, 'B', knight:true));
            Add(new UnitType("WG", Side.Norman, "Norman", "Norman_Cavalry_Special", 8, 6, 8, 6, 'A', knight:true, guard:true));
            Add(new UnitType("FB", Side.Norman, "Franco-Flemish", "Franco-Flemish_Bowmen", 1, 2, 1, 2, 'C', "B"));
            Add(new UnitType("FF", Side.Norman, "Franco-Flemish", "Franco-Flemish_Infantry", 4, 4, 2, 6, 'C'));
            Add(new UnitType("FK", Side.Norman, "Franco-Flemish", "Franco-Flemish_Cavalry", 8, 6, 8, 6, 'B', knight:true));
            Add(new UnitType("HC", Side.Saxon, "Saxon", "Housecarle", 7, 5, 3, 8, 'A'));
            Add(new UnitType("T", Side.Saxon, "Saxon", "Thegn", 4, 4, 2, 6, 'C'));
            Add(new UnitType("F1", Side.Saxon, "Saxon", "Great_Fyrd_1", 3, 3, 3, 3, 'D', "J"));
            Add(new UnitType("F2", Side.Saxon, "Saxon", "Great_Fyrd_2", 2, 3, 2, 3, 'E', "J"));
            Add(new UnitType("SB", Side.Saxon, "Saxon", "Great_Fyrd_Bowmen", 1, 2, 1, 2, 'C', "B"));
            Add(new UnitType("SL", Side.Saxon, "Saxon", "Slingers", 1, 2, 1, 2, 'E', "S"));
            Add(new UnitType("William", Side.Norman, "Norman", "William", 0, 0, 0, 0, 'A', leader:true, command:7, rally:2));
            Add(new UnitType("Alan", Side.Norman, "Breton", "Alan", 0, 0, 0, 0, 'B', leader:true, command:3, rally:0));
            Add(new UnitType("Odo", Side.Norman, "Norman", "Odo", 0, 0, 0, 0, 'B', leader:true, command:6, rally:1));
            Add(new UnitType("Eustace", Side.Norman, "Franco-Flemish", "Eustace", 0, 0, 0, 0, 'B', leader:true, command:4, rally:0));
            Add(new UnitType("Harold", Side.Saxon, "Saxon", "Harold", 0, 0, 0, 0, 'A', leader:true, command:6, rally:2));
            Add(new UnitType("Gyrth", Side.Saxon, "Saxon", "Gyrth", 0, 0, 0, 0, 'B', leader:true, command:5, rally:0));
            Add(new UnitType("Leofwine", Side.Saxon, "Saxon", "Leofwine", 0, 0, 0, 0, 'B', leader:true, command:4, rally:0));
        }
        private static void Add(UnitType type) { Types.Add(type.id, type); }
        public static UnitType Get(UnitState unit) { return Types[unit.type]; }
        public static UnitType Get(string id) { return Types[id]; }
    }

    public sealed class Board
    {
        public readonly MapData data;
        private readonly Dictionary<string, HexData> hexes = new Dictionary<string, HexData>();
        private readonly Dictionary<string, List<string>> neighbors = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, EdgeData> edges = new Dictionary<string, EdgeData>();
        public Board(MapData data)
        {
            this.data = data;
            foreach (var h in data.hexes) { hexes[h.id] = h; neighbors[h.id] = new List<string>(); }
            foreach (var e in data.edges)
            {
                if (!hexes.ContainsKey(e.a) || !hexes.ContainsKey(e.b)) continue;
                neighbors[e.a].Add(e.b); neighbors[e.b].Add(e.a);
                edges[Key(e.a, e.b)] = e;
            }
        }
        private static string Key(string a, string b) { return string.CompareOrdinal(a,b)<0 ? a+"|"+b : b+"|"+a; }
        public bool Has(string id) { return id != null && hexes.ContainsKey(id); }
        public HexData Hex(string id) { return hexes[id]; }
        public IEnumerable<string> Adjacent(string id) { return neighbors[id]; }
        public EdgeData Edge(string a, string b) { EdgeData e; return edges.TryGetValue(Key(a,b), out e) ? e : null; }
        public int Distance(string a, string b)
        {
            var ha=Hex(a);var hb=Hex(b);
            int r=(int)Math.Round((hb.y-ha.y)/86f);
            int q=(int)Math.Round((hb.x-ha.x)/100f-r/2f);
            return (Math.Abs(q)+Math.Abs(r)+Math.Abs(q+r))/2;
        }
        public int Direction(string a, string b)
        {
            var ha=Hex(a); var hb=Hex(b);
            double angle=Math.Atan2(-(hb.y-ha.y),hb.x-ha.x);
            return ((int)Math.Round(angle/(Math.PI/3))+6)%6;
        }
        public string Nearest(float x, float y)
        {
            string result=null; float best=65*65;
            foreach (var h in data.hexes)
            { float d=(x-h.x)*(x-h.x)+(y-h.y)*(y-h.y); if(d<best){best=d;result=h.id;} }
            return result;
        }
    }
}
