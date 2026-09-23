using System;
using System.Collections.Generic;
using System.Linq;

namespace Hastings
{
    public static class Setup
    {
        private static readonly string[] NormanRows = {
            "BB:1123,1121", "NB:1119,1117,1115,1113", "FB:1111,1109,1107",
            "BF:1223,1222,1221,1220,1324,1323,1322,1321",
            "NF:1218,1217,1216,1215,1214,1213,1319,1318,1317,1316,1315,1314",
            "FF:1211,1210,1209,1208,1207,1311,1310,1309,1308",
            "BK:1524,1523,1522,1623,1622",
            "NK:1518,1517,1516,1515,1514,1617,1616,1615,1614",
            "FK:1510,1509,1508,1507,1609,1608,1607", "WG:1906"
        };
        private static readonly string[] SaxonLine = {
            "0624","0623","0622","0621","0620","0619","0618","0617",
            "0616","0615","0614","0613","0612","0611","0610","0609",
            // The printed F/T line runs from 0724 through 0709. Its markings
            // share 0721 with Gyrth and 0710 with Leofwine.
            "0724","0723","0722","0721","0720","0719","0718","0717",
            "0716","0715","0714","0713","0712","0711","0710","0709"
        };
        public static GameState New(Board board, uint seed, Side playerSide=Side.Norman)
        {
            var state = new GameState { randomState = seed == 0 ? 1u : seed,
                playerSide=playerSide };
            foreach (var group in new[] {"Breton","Norman","Franco-Flemish","Left","Center","Right"})
                state.groups.Add(new GroupState { id=group, strategy=Strategy.Moderate,
                    footOrder=Order.Advance, knightOrder=Order.Advance });
            int serial=0;
            Action<string,string,string,int> add=(type,hex,group,facing) =>
            {
                if(!board.Has(hex)) throw new InvalidOperationException("Setup hex not on map: " + hex);
                state.units.Add(new UnitState { id=type+"-"+(++serial).ToString("D3"), type=type,
                    hex=hex, group=group, side=UnitTypes.Get(type).side, facing=facing });
            };
            foreach (var entry in NormanRows)
            {
                var parts=entry.Split(':'); var type=UnitTypes.Get(parts[0]);
                foreach (var hex in parts[1].Split(',')) add(parts[0],hex,type.nation,1);
            }
            foreach (var leader in new[] {("William","1906"),("Alan","1523"),
                                            ("Odo","1215"),("Eustace","1209")})
                add(leader.Item1,leader.Item2,UnitTypes.Get(leader.Item1).nation,1);
            foreach (var leader in new[] {("Harold","0514","Center"),
                                            ("Gyrth","0721","Left"),
                                            ("Leofwine","0710","Right")})
                add(leader.Item1,leader.Item2,leader.Item3,4);
            foreach(var hex in new[]{"0414","0515","0514","0513",
                "0808","0809","0810","0811","0812","0813","0814","0815",
                "0816","0817","0818","0819","0820","0821","0822","0823"})
                add("HC",hex,ClosestWing(board,hex),4);
            foreach(var hex in new[]{"0517","0522"}) add("SB",hex,ClosestWing(board,hex),4);
            var pool=new List<string>();
            for(int i=0;i<21;i++)pool.Add("F1");
            for(int i=0;i<20;i++)pool.Add("F2");
            for(int i=0;i<10;i++)pool.Add("SL");
            for(int i=pool.Count-1;i>0;i--) { int j=Next(ref state.randomState,i+1); var t=pool[i];pool[i]=pool[j];pool[j]=t; }
            for(int i=0;i<SaxonLine.Length;i++)
            {
                string type=i<5?"T":pool[i-5];
                add(type,SaxonLine[i],ClosestWing(board,SaxonLine[i]),4);
            }
            // Twelve arrive during period I; the final twelve enter at reform.
            int[] arrival={1,1,2,2,3,3,4,4,5,6,7,8};
            for(int i=27;i<pool.Count;i++)
            {
                string type=pool[i]; serial++;
                state.units.Add(new UnitState { id=type+"-"+serial.ToString("D3"), type=type,
                    side=Side.Saxon, group="Center", hex="", facing=4,
                    reservePeriod=i<39?1:2, reserveTurn=i<39?arrival[i-27]:1 });
            }
            foreach(var h in board.data.hexes.Where(h=>h.road && int.Parse(h.id.Substring(0,2))<=8))
                state.road.Add(new RoadState { hex=h.id, owner=Side.Saxon });
            state.log.Add(playerSide==Side.Norman?
                "New standard game. Set Norman facings, then begin the first turn.":
                "New standard game as the Saxons. The Norman army is controlled by the AI.");
            return state;
        }
        private static string ClosestWing(Board board, string hex)
        {
            int left=board.Distance(hex,"0721"), center=board.Distance(hex,"0514"), right=board.Distance(hex,"0710");
            return left<=center && left<=right?"Left":right<center?"Right":"Center";
        }
        private static int Next(ref uint state,int limit)
        {
            state^=state<<13;state^=state>>17;state^=state<<5;
            return (int)(state%(uint)limit);
        }
    }
}
