using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hastings;
using UnityEditor;
using UnityEngine;

public static class ProjectChecks
{
    public static void Run()
    {
        var source=Resources.Load<TextAsset>("Data/Map");
        Check(source!=null,"Map data missing");
        var board=new Board(JsonUtility.FromJson<MapData>(source.text));
        Check(board.data.hexes.Length==703,"Expected 703 playable hexes");
        Check(board.data.hexes.Select(h=>h.id).Distinct().Count()==703,"Duplicate hex ids");
        Check(board.Has("0116")&&board.Has("0811")&&board.Has("1420"),"Key hex missing");
        Check(board.Distance("0116","0811")>0,"Hex distance broken");
        for(int rowNumber=1;rowNumber<=25;rowNumber++)
        {
            var row=board.data.hexes.Where(h=>int.Parse(h.id.Substring(0,2))==rowNumber)
                .OrderByDescending(h=>h.x).ToArray();
            Check(row.Length>0,"Missing numbered hex row "+rowNumber);
            for(int i=1;i<row.Length;i++)
                Check(int.Parse(row[i].id.Substring(2,2))==
                    int.Parse(row[i-1].id.Substring(2,2))+1,
                    "Hex numbers do not increase from right to left in row "+rowNumber);
        }
        Check(board.Hex("0101").x>board.Hex("0102").x &&
            board.Hex("0102").x>board.Hex("0103").x,"Upper-right hex numbering reversed");
        float viewScale=0;Vector2 viewPan=Vector2.zero;
        BoardViewMath.Resize(ref viewScale,ref viewPan,0,0,1500,1000,board.data.width);
        float firstScale=viewScale;
        float centerX=(750-viewPan.x)/viewScale;
        float centerY=(500-viewPan.y)/viewScale;
        BoardViewMath.Resize(ref viewScale,ref viewPan,1500,1000,3200,1400,board.data.width);
        Check(viewScale>firstScale*2 && Mathf.Abs((1600-viewPan.x)/viewScale-centerX)<.01f &&
            Mathf.Abs((700-viewPan.y)/viewScale-centerY)<.01f,
            "Map did not enlarge and preserve its center after window resize");
        float fullScale=BoardViewMath.FitWhole(3200,1400,board.data.width,board.data.height);
        Check(board.data.width*fullScale<=3200 && board.data.height*fullScale<=1400,
            "Full-map view does not fit the window");
        var edgePan=BoardViewMath.ClampPan(new Vector2(100,-10000),2,800,600,500,400);
        Check(edgePan==new Vector2(0,-200),"Map pan escaped a board edge");
        var centeredPan=BoardViewMath.ClampPan(new Vector2(999,-999),.5f,1000,800,500,400);
        Check(centeredPan==new Vector2(375,300),"Small map was not centered");
        foreach(Strategy strategy in Enum.GetValues(typeof(Strategy)))
            for(int dice=2;dice<=12;dice++)
            {
                int duration,effect;bool optional;
                RuleTables.RollOrder(Side.Saxon,false,strategy,dice,out duration,out effect,out optional);
                RuleTables.RollOrder(Side.Norman,false,strategy,dice,out duration,out effect,out optional);
                RuleTables.RollOrder(Side.Norman,true,strategy,dice,out duration,out effect,out optional);
            }
        foreach(var type in new[]{"BB","BF","BK","NB","NF","NK","WG","FB","FF","FK",
                                  "HC","T","F1","F2","SB","SL","William","Alan","Odo",
                                  "Eustace","Harold","Gyrth","Leofwine"})
        {
            var t=UnitTypes.Get(type);string side=t.side==Side.Norman?"Normans":"Saxons";
            Check(Resources.Load<Texture2D>("Art/Counters/"+side+"/"+t.art)!=null,"Missing counter art: "+type);
        }
        var state=Setup.New(board,12345);
        Check(state.units.Count(u=>u.type=="T")==5,"Thegn setup count");
        Check(state.units.Count(u=>u.type=="HC")==20,"Housecarl setup count");
        Check(state.units.Count(u=>u.side==Side.Norman && !UnitTypes.Get(u).leader)==60,
            "Norman combat counter count");
        Check(state.units.Count(u=>u.hex=="" && u.reservePeriod==1)==12,"First period reserve count");
        Check(state.units.Count(u=>u.hex=="" && u.reservePeriod==2)==12,"Second period reserve count");
        Check(!state.units.Any(u=>u.hex=="1307"),"Errata setup hex 1307 used");
        Check(state.units.Where(u=>!UnitTypes.Get(u).leader && u.hex!="")
            .GroupBy(u=>u.hex).All(g=>g.Count()==1),"Combat units stacked at setup");
        var engine=new GameEngine(board,state);engine.Begin();engine.ResolveOrders();
        Check(state.phase==Phase.NormanFire,"Opening order phase did not advance");
        var json=JsonUtility.ToJson(state);
        var restored=JsonUtility.FromJson<GameState>(json);
        Check(restored.randomState==state.randomState && restored.units.Count==state.units.Count &&
              restored.phase==state.phase,"Save state round trip failed");
        var restoredEngine=new GameEngine(board,restored);
        var originalMoves=engine.LegalMoves(state.units.First(u=>u.type=="BK"))
            .Keys.OrderBy(x=>x).ToArray();
        var restoredMoves=restoredEngine.LegalMoves(restored.units.First(u=>u.type=="BK"))
            .Keys.OrderBy(x=>x).ToArray();
        Check(originalMoves.SequenceEqual(restoredMoves),"Saved game changed legal moves");
        Check(engine.Die()==restoredEngine.Die() && state.randomState==restored.randomState,
            "Saved game changed subsequent dice");
        string slot="HastingsVerification"+Guid.NewGuid().ToString("N");
        try
        {
            GameStorage.Save(slot,state);
            var diskState=GameStorage.Load(slot);
            Check(diskState.randomState==state.randomState && diskState.phase==state.phase,
                "Named save slot failed to restore state");
            diskState.turn=7;
            GameStorage.Save(slot,diskState);
            Check(GameStorage.Load(slot).turn==7,"Overwriting a named save slot failed");
        }
        finally
        {
            string path=Path.Combine(Application.persistentDataPath,"Saves",slot+".json");
            if(File.Exists(path))File.Delete(path);
        }
        var roadState=Setup.New(board,33);
        foreach(var road in roadState.road)road.owner=Side.Norman;
        new GameEngine(board,roadState).CheckVictory();
        Check(roadState.result=="Norman strategic victory","Road victory failed");
        var tacticalState=Setup.New(board,35);tacticalState.normanCasualties=100;
        foreach(var road in tacticalState.road)road.owner=Side.Norman;
        new GameEngine(board,tacticalState).CheckVictory();
        Check(tacticalState.result=="Norman tactical victory","Road tactical victory failed");
        var casualtyState=Setup.New(board,34);casualtyState.normanCasualties=100;
        foreach(var hc in casualtyState.units.Where(u=>u.type=="HC"))hc.status=Status.Eliminated;
        new GameEngine(board,casualtyState).CheckVictory();
        Check(casualtyState.result=="Draw","Housecarl casualty draw failed");
        var housecarlState=Setup.New(board,36);
        foreach(var hc in housecarlState.units.Where(u=>u.type=="HC"))hc.status=Status.Eliminated;
        new GameEngine(board,housecarlState).CheckVictory();
        Check(housecarlState.result=="Norman strategic victory","Housecarl strategic victory failed");
        Debug.Log("HASTINGS CHECKS PASSED: map, tables, art, setup and state round trip");
    }
    public static void RunSimulation()
    {
        var board=new Board(JsonUtility.FromJson<MapData>(Resources.Load<TextAsset>("Data/Map").text));
        foreach(uint seed in new uint[]{4077,1819,2026})Simulate(board,seed);
    }
    private static void Simulate(Board board,uint seed)
    {
        var engine=new GameEngine(board,Setup.New(board,seed));engine.Begin();
        for(int step=0;step<300 && engine.state.phase!=Phase.GameOver;step++)
        {
            var s=engine.state;
            if(s.phase==Phase.Orders)engine.ResolveOrders();
            else if(s.phase==Phase.Reform)
            {
                var sites=board.data.hexes.Where(h=>int.Parse(h.id.Substring(2,2))>=6 &&
                    int.Parse(h.id.Substring(2,2))<=26 &&
                    board.data.hexes.Where(x=>x.level>=4 && int.Parse(x.id.Substring(0,2))<=9)
                        .Min(x=>board.Distance(h.id,x.id))>=4).OrderByDescending(h=>h.y).ToList();
                foreach(var unit in engine.Living(Side.Norman).ToList())
                {
                    bool placed=false;
                    foreach(var site in sites)
                        if(engine.ReformMove(unit,site.id)){placed=true;break;}
                    Check(placed,"Could not reform "+unit.id);
                }
                Check(engine.FinishReform(),"Reform phase could not complete");
            }
            else
            {
                foreach(var group in s.groups.Where(g=>g.footOptional||g.knightOptional))
                {
                    if(group.footOptional)engine.SetOptionalOrder(group.id,false,Order.Advance);
                    if(group.knightOptional)engine.SetOptionalOrder(group.id,true,Order.Advance);
                }
                engine.Advance();
            }
            var stacks=s.units.Where(u=>u.status!=Status.Eliminated && u.hex!="" && !UnitTypes.Get(u).leader)
                .GroupBy(u=>u.hex).Where(g=>g.Count()>1).ToList();
            Check(stacks.Count==0,"Stacking violation at step "+step+" phase "+s.phase+" turn "+s.turn+": "+
                string.Join(";",stacks.Select(g=>g.Key+"="+string.Join(",",g.Select(u=>u.id).ToArray())).ToArray())+
                " LOG "+string.Join(" | ",s.log.Skip(Math.Max(0,s.log.Count-20)).ToArray())+
                " FK056 "+string.Join(" | ",s.log.Where(x=>x.Contains("FK-056")).ToArray()));
        }
        Check(engine.state.phase==Phase.GameOver,"Game did not finish in 300 segments");
        Debug.Log("HASTINGS SIMULATION PASSED seed "+seed+": "+engine.state.result+
                  ", "+engine.state.log.Count+" events");
    }
    private static void Check(bool condition,string message)
    { if(!condition)throw new Exception("HASTINGS CHECK FAILED: "+message); }
}
