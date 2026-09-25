using System;
using System.Collections.Generic;

namespace Hastings
{
    public static class RuleTables
    {
        // Columns: aggressive, moderate, cautious, defensive. A 2d6 result
        // omitted from a row belongs to another row of the same chart.
        private static readonly string[][] Saxon = {
            new [] {"3,4", "4,8", "6,7", "4,6,7"},
            new [] {"5,6", "5", "5", "5"},
            new [] {"7", "6", "4", ""},
            new [] {"8,9", "7,9", "8,9", "9,10,12"},
            new [] {"10,11,12", "3,10,11,12", "3,10,11,12", "3,8,11"},
            new [] {"2", "2", "2", "2"}
        };
        private static readonly string[][] NormanFoot = {
            new [] {"5", "4,5", "3,4,5", "4,5,9"},
            new [] {"10,11,12", "9,10,12", "9,10,11", "8,10,11,12"},
            new [] {"6,7,8,9", "6,7,8,11", "6,7,8", "3,6,7"},
            new [] {"3,4", "3", "12", ""},
            new [] {"2", "2", "2", "2"}
        };
        private static readonly string[][] NormanKnights = {
            new [] {"6", "7", "7,8,12", "3,7,8,11"},
            new [] {"7,8,12", "8,9", "9,10", "9,10"},
            new [] {"9,10", "10", "11", "12"},
            new [] {"3,4,5", "3,4,5,6", "4,5,6", "4,5,6"},
            new [] {"11", "11,12", "3", ""},
            new [] {"2", "2", "2", "2"}
        };
        private static bool HasRoll(string list, int dice)
        {
            foreach (var value in list.Split(',')) if (value == dice.ToString()) return true;
            return false;
        }
        public static Order RollOrder(Side side, bool knight, Strategy strategy, int dice,
                                      out int duration, out int effect, out bool optional)
        {
            var table = side == Side.Saxon ? Saxon : knight ? NormanKnights : NormanFoot;
            int index = -1;
            int column=3-(int)strategy;
            for (int row=0; row<table.Length; row++) if (HasRoll(table[row][column], dice)) { index=row; break; }
            if (index < 0) throw new InvalidOperationException("Missing order table result: " + side + " " + strategy + " " + dice);
            optional = index == table.Length-1;
            duration = 1; effect = 0;
            if (optional) return knight ? Order.Advance : Order.Advance;
            if (side == Side.Saxon)
            {
                switch(index)
                {
                    case 0: effect=-2; return Order.ShieldWall;
                    case 1: effect=1; return Order.AttackPursue;
                    case 2: effect=1; duration=2; return Order.AttackPursue;
                    case 3: return Order.Advance;
                    default: effect=-1; return Order.FireInPlace;
                }
            }
            if (knight)
            {
                switch(index)
                {
                    case 0: effect=-2; return Order.Hold;
                    case 1: effect=2; return Order.Charge;
                    case 2: effect=2; duration=2; return Order.Charge;
                    case 3: effect=1; return Order.Advance;
                    default: effect=1; duration=2; return Order.Advance;
                }
            }
            switch(index)
            {
                case 0: effect=-2; return Order.ShieldWall;
                case 1: effect=-1; return Order.FireInPlace;
                case 2: effect=1; return Order.Advance;
                default: effect=1; duration=2; return Order.Advance;
            }
        }
        public static readonly string[,] Melee = {
            {"1/-","1/-","1/-","1/-","1/-","1/D","1/D","M/-","D/-","1/1","D/1"},
            {"1/-","1/-","1/-","1/-","1/D","M/-","M/-","D/-","1/1","D/1","-/M"},
            {"1/-","1/-","1/-","1/D","M/-","D/-","1/1","D/1","-/M","-/1","-/1"},
            {"1/-","1/-","1/D","M/1","D/-","1/1","1/1","D/1","-/M","-/1","-/1"},
            {"1/-","1/D","M/-","D/-","1/1","1/1","D/1","-/M","-/1","-/1","-/1M"},
            {"1/D","M/-","D/-","1/1","1/1","D/1","-/M","-/1","-/1","-/1M","-/1M"}
        };
        public static int MeleeColumn(int differential)
        {
            if (differential <= -6) return 0;
            if (differential <= 1) return differential + 6;
            if (differential <= 3) return 8;
            if (differential <= 5) return 9;
            return 10;
        }
        public static string MeleeResult(int differential, int roll, int columnShift=0)
        {
            if (differential <= -6) return "1/-";
            int column=Math.Max(0,Math.Min(10,MeleeColumn(differential)+columnShift));
            return Melee[Math.Max(1,Math.Min(6,roll))-1,column];
        }
        public static readonly string[,] Missile = {
            {"-","-","-","-","-","M","M","M","D","D"},
            {"-","-","-","-","M","M","M","D","D","D"},
            {"-","-","-","M","M","M","D","D","D","1"},
            {"-","-","M","M","M","D","D","D","1","1"},
            {"-","M","M","M","D","D","D","1","1","1"},
            {"M","M","M","D","D","D","1","1","1","1"}
        };
        public static int MissileColumn(int strength, int defense)
        {
            if (defense <= 0) defense=1;
            float odds=(float)strength/defense;
            float[] thresholds={.25f, 1f/3f, .5f, 2f/3f, 1f, 1.5f, 2f, 3f, 4f, 5f};
            int result=-1;
            for(int i=0;i<thresholds.Length;i++) if(odds+0.0001f>=thresholds[i]) result=i;
            return result;
        }
        public static int MissileStrength(string weapon, int range)
        {
            if (weapon=="B") return range==1?5:range==2?4:range==3?3:0;
            if (weapon=="J") return range==1?3:range==2?2:0;
            if (weapon=="S") return range==1?2:range<=3?1:0;
            return 0;
        }
        public static Status Morale(char rating, int roll)
        {
            int column=rating-'A';
            string[] rows={"-----", "---DD", "---DR", "-DDRR", "DDRRR", "DRRRR"};
            char result=rows[Math.Max(0,Math.Min(5,roll-1))][Math.Max(0,Math.Min(4,column))];
            return result=='D'?Status.Disrupted:result=='R'?Status.Routed:Status.Ready;
        }
        public static bool Rally(char rating, int roll) { return roll <= 5-(rating-'A'); }
        public static int CasualtyPoints(UnitType type)
        {
            if(type.id=="William") return 50; if(type.id=="Harold") return 25;
            if(type.id=="Odo") return 10; if(type.leader) return 5;
            return type.morale=='A'?15:type.morale=='B'?7:type.morale=='C'?3:type.morale=='D'?2:1;
        }
    }

    public static class StrategyEffects
    {
        public static char Code(bool knight,int effect)
        {
            if(effect<=-9)return 'B';
            if(effect<=-7)return knight?'B':'A';
            if(effect<=-4)return knight?'A':'-';
            if(effect>=13)return 'D';
            if(effect>=9)return knight?'C':'D';
            if(effect>=5)return knight?'-':'C';
            return '-';
        }
        public static bool WorsensMorale(bool knight,int effect)
        {return Code(knight,effect)=='B';}
        public static bool PenalizesMoraleRoll(bool knight,int effect)
        {
            char code=Code(knight,effect);
            return code=='A'||code=='B';
        }
        public static int MovementPenalty(bool knight,int effect)
        {
            char code=Code(knight,effect);
            return code=='C'||code=='D'?1:0;
        }
        public static bool PenalizesCombat(bool knight,int effect)
        {return Code(knight,effect)=='D';}
    }
}
