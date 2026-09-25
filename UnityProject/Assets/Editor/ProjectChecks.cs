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
        var mapTexture=Resources.Load<Texture2D>("Art/Map/hex_map");
        Check(mapTexture!=null && mapTexture.width==5900 && mapTexture.height==4800,
            "Map texture was downsampled during import: "+
            (mapTexture==null?"missing":mapTexture.width+"x"+mapTexture.height));
        Check(Resources.Load<Texture2D>("Art/Counters/Markers/Assault_Period")!=null,
            "Assault period marker art missing");
        Check(Resources.Load<Texture2D>("Art/Counters/Markers/Battle_Turn")!=null,
            "Battle turn marker art missing");
        foreach(var terrain in new[]{"clear","ridge","marsh","stream","woods","road"})
        {
            var swatch=Resources.Load<Texture2D>("Art/Terrain/"+terrain);
            Check(swatch!=null,"Terrain chart swatch missing: "+terrain);
            Check(swatch.width==200 && swatch.height==172,
                "Terrain chart swatch was distorted during import: "+terrain+" "+
                swatch.width+"x"+swatch.height);
        }
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
        Check(board.Edge("0823","0923")!=null && board.Edge("0823","0923").ridge,
            "Senlac Hill ridge edge missing");
        Check(board.Edge("0923","1023")!=null && !board.Edge("0923","1023").ridge,
            "Gentle slope incorrectly marked as a ridge");
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
        var boardPoint=new Vector2(800,1434);
        var saxonPoint=BoardViewMath.OrientBattlefield(boardPoint,board.data.width,true);
        var restoredPoint=BoardViewMath.OrientBattlefield(saxonPoint,board.data.width,true);
        Check(restoredPoint==boardPoint && saxonPoint.x==board.data.width-boardPoint.x &&
            saxonPoint.y==BoardViewMath.BattlefieldHeight-boardPoint.y,
            "Saxon battlefield orientation is not a 180 degree rotation");
        foreach(Strategy strategy in Enum.GetValues(typeof(Strategy)))
            for(int dice=2;dice<=12;dice++)
            {
                int duration,effect;bool optional;
                RuleTables.RollOrder(Side.Saxon,false,strategy,dice,out duration,out effect,out optional);
                RuleTables.RollOrder(Side.Norman,false,strategy,dice,out duration,out effect,out optional);
                RuleTables.RollOrder(Side.Norman,true,strategy,dice,out duration,out effect,out optional);
            }
        Check(StrategyEffects.Code(false,-9)=='B' && StrategyEffects.Code(false,-8)=='A' &&
            StrategyEffects.Code(false,-7)=='A' && StrategyEffects.Code(false,-6)=='-' &&
            StrategyEffects.Code(true,-9)=='B' && StrategyEffects.Code(true,-8)=='B' &&
            StrategyEffects.Code(true,-7)=='B' && StrategyEffects.Code(true,-6)=='A' &&
            StrategyEffects.Code(true,-4)=='A' && StrategyEffects.Code(true,-3)=='-',
            "Negative strategy-effect bands do not match the map");
        Check(StrategyEffects.Code(false,5)=='C' && StrategyEffects.Code(false,8)=='C' &&
            StrategyEffects.Code(false,9)=='D' && StrategyEffects.Code(false,12)=='D' &&
            StrategyEffects.Code(true,8)=='-' && StrategyEffects.Code(true,9)=='C' &&
            StrategyEffects.Code(true,12)=='C' && StrategyEffects.Code(true,13)=='D' &&
            StrategyEffects.MovementPenalty(false,13)==1,
            "Positive strategy-effect bands do not match the map");
        foreach(var type in new[]{"BB","BF","BK","NB","NF","NK","WG","FB","FF","FK",
                                  "HC","T","F1","F2","SB","SL","William","Alan","Odo",
                                  "Eustace","Harold","Gyrth","Leofwine"})
        {
            var t=UnitTypes.Get(type);string side=t.side==Side.Norman?"Normans":"Saxons";
            Check(Resources.Load<Texture2D>("Art/Counters/"+side+"/"+t.art)!=null,"Missing counter art: "+type);
        }
        var state=Setup.New(board,12345);
        Check((int)Phase.Reform==7 && (int)Phase.GameOver==8,
            "Existing save phase values changed");
        Check(state.playerSide==Side.Norman,"Legacy new games did not default to the Normans");
        Check(state.units.Count(u=>u.type=="T")==5,"Thegn setup count");
        Check(state.units.Count(u=>u.type=="HC")==20,"Housecarl setup count");
        Check(state.units.Count(u=>u.side==Side.Norman && !UnitTypes.Get(u).leader)==60,
            "Norman combat counter count");
        Check(state.units.Count(u=>u.hex=="" && u.reservePeriod==1)==12,"First period reserve count");
        Check(state.units.Count(u=>u.hex=="" && u.reservePeriod==2)==12,"Second period reserve count");
        Check(!state.units.Any(u=>u.hex=="1307"),"Errata setup hex 1307 used");
        Check(state.units.Where(u=>!UnitTypes.Get(u).leader && u.hex!="")
            .GroupBy(u=>u.hex).All(g=>g.Count()==1),"Combat units stacked at setup");
        foreach(var leaderType in new[]{"Harold","Gyrth","Leofwine"})
        {
            var leader=state.units.Single(u=>u.type==leaderType);
            Check(state.units.Any(u=>u.hex==leader.hex && !UnitTypes.Get(u).leader),
                leaderType+" must share its setup hex with a combat unit");
        }
        var odo=state.units.Single(u=>u.type=="Odo");
        Check(odo.group=="Norman" && UnitTypes.Get(odo).nation=="Norman" &&
            state.units.Any(u=>u.hex==odo.hex && !UnitTypes.Get(u).leader &&
                UnitTypes.Get(u).nation=="Norman"),
            "Odo must share a hex with a Norman unit and command Normans");
        var unitLabels=UnitDisplayNames.Build(state);
        var flemishBowmen=state.units.Where(u=>u.type=="FB").ToList();
        Check(flemishBowmen.Count==3 &&
            flemishBowmen.Select(u=>unitLabels[u.id]).SequenceEqual(new[]{
                "Flemish Bowman 1","Flemish Bowman 2","Flemish Bowman 3"}),
            "Flemish bowmen must have per-type display numbers");
        Check(UnitDisplayNames.InEvent("T1: "+flemishBowmen[0].id+" faces 0",unitLabels)==
            "T1: Flemish Bowman 1 faces 0","Event log still shows a raw unit id");
        foreach(float counterScale in new[]{1f,2.5f,4f})
        {
            var center=new Vector2(300,200);
            var stackedUnit=CounterLayout.RectFor(center,counterScale,false,false);
            var stackedLeader=CounterLayout.RectFor(center,counterScale,true,false);
            Check(stackedUnit.center==center && stackedLeader.center==center,
                "Stacked counters are not centered on their hex");
            var spreadUnit=CounterLayout.RectFor(center,counterScale,false,true);
            var spreadLeader=CounterLayout.RectFor(center,counterScale,true,true);
            Check(!spreadUnit.Overlaps(spreadLeader),
                "Hover spread still overlaps counters at scale "+counterScale);
            var movingUnit=CounterLayout.RectFor(center,counterScale,false,.5f);
            var movingLeader=CounterLayout.RectFor(center,counterScale,true,.5f);
            Check(movingUnit.center.x<stackedUnit.center.x &&
                  movingUnit.center.x>spreadUnit.center.x &&
                  movingLeader.center.x>stackedLeader.center.x &&
                  movingLeader.center.x<spreadLeader.center.x,
                "Stack spread does not interpolate between stacked and open positions");
        }
        var engine=new GameEngine(board,state);engine.Begin();engine.ResolveOrders();
        Check(state.phase==Phase.NormanFire,"Opening order phase did not advance");
        Check(state.orderResults.Count==6 &&
            state.orderResults.Count(r=>r.side==Side.Norman)==3 &&
            state.orderResults.All(r=>r.roll>=2 && r.roll<=12),
            "Order roll results were not retained for the player summary");
        foreach(var result in state.orderResults.Where(r=>r.side==Side.Norman))
        {
            int footDuration,footEffect,knightDuration,knightEffect;
            bool footOptional,knightOptional;
            var expectedFoot=RuleTables.RollOrder(Side.Norman,false,result.strategy,result.footRoll,
                out footDuration,out footEffect,out footOptional);
            var expectedKnights=RuleTables.RollOrder(Side.Norman,true,result.strategy,result.knightRoll,
                out knightDuration,out knightEffect,out knightOptional);
            Check(result.footRoll>=2 && result.footRoll<=12 &&
                result.knightRoll>=2 && result.knightRoll<=12 &&
                result.footOrder==expectedFoot && result.knightOrder==expectedKnights &&
                result.effectChange==footEffect+knightEffect,
                result.group+" did not resolve its separate foot and knight rolls");
        }
        Check(state.orderResults.Where(r=>r.side==Side.Norman)
            .Any(r=>r.footRoll!=r.knightRoll),
            "Norman foot and knight sections reused the same dice roll");
        var optionalState=Setup.New(board,54321);optionalState.phase=Phase.NormanFire;
        var optionalGroup=optionalState.groups.First(g=>g.id=="Breton");
        optionalGroup.footOptional=true;
        optionalState.orderResults.Add(new OrderRollResult {group="Breton",side=Side.Norman,
            footOptional=true,hasKnights=true});
        var optionalEngine=new GameEngine(board,optionalState);
        var waitingBowman=optionalState.units.First(u=>u.type=="NB");
        var waitingTarget=optionalState.units.First(u=>u.type=="HC");
        waitingBowman.hex="1112";waitingTarget.hex=board.Adjacent(waitingBowman.hex).First();
        waitingBowman.facing=board.Direction(waitingBowman.hex,waitingTarget.hex);
        Check(!optionalEngine.CanFire(waitingBowman,waitingTarget),
            "Missile fire was allowed before optional battle orders were completed");
        Check(optionalEngine.SetOptionalOrder("Breton",false,Order.FireInPlace) &&
            !optionalGroup.footOptional && optionalGroup.footOrder==Order.FireInPlace &&
            optionalState.orderResults[0].footOrder==Order.FireInPlace &&
            optionalEngine.CanFire(waitingBowman,waitingTarget),
            "Optional order choice was not applied to its section");
        var json=JsonUtility.ToJson(state);
        var restored=JsonUtility.FromJson<GameState>(json);
        Check(restored.randomState==state.randomState && restored.units.Count==state.units.Count &&
              restored.phase==state.phase && restored.orderResults.Count==state.orderResults.Count,
              "Save state round trip failed");
        var restoredEngine=new GameEngine(board,restored);
        var originalMoves=engine.LegalMoves(state.units.First(u=>u.type=="BK"))
            .Keys.OrderBy(x=>x).ToArray();
        var restoredMoves=restoredEngine.LegalMoves(restored.units.First(u=>u.type=="BK"))
            .Keys.OrderBy(x=>x).ToArray();
        Check(originalMoves.SequenceEqual(restoredMoves),"Saved game changed legal moves");
        Check(engine.Die()==restoredEngine.Die() && state.randomState==restored.randomState,
            "Saved game changed subsequent dice");
        var saxonState=Setup.New(board,24680,Side.Saxon);
        Check(saxonState.playerSide==Side.Saxon,"Saxon side selection was not stored");
        var saxonEngine=new GameEngine(board,saxonState);
        saxonEngine.Begin();
        saxonEngine.SetStrategy("Left",Strategy.Defensive);
        saxonEngine.ResolveOrders();
        Check(saxonState.groups.First(g=>g.id=="Left").strategy==Strategy.Defensive,
            "Player-selected Saxon strategy was replaced by the AI");
        foreach(var group in saxonState.groups.Where(g=>g.footOptional||g.knightOptional))
        {
            if(group.footOptional)saxonEngine.SetOptionalOrder(group.id,false,Order.Advance);
            if(group.knightOptional)saxonEngine.SetOptionalOrder(group.id,true,Order.Advance);
        }
        saxonEngine.Advance();
        Check(saxonState.phase==Phase.SaxonReaction,
            "Norman AI opening did not hand control to the Saxon reaction phase");
        saxonEngine.Advance();
        Check(saxonState.phase==Phase.SaxonDefenseFire,"Saxon defensive fire phase missing");
        saxonEngine.Advance();
        Check(saxonState.phase==Phase.SaxonFire,"Saxon offensive fire phase missing");
        saxonEngine.Advance();
        Check(saxonState.phase==Phase.SaxonMove,"Saxon movement phase missing");
        saxonEngine.Advance();
        Check(saxonState.phase==Phase.SaxonMelee,"Saxon melee phase missing");
        var saxonJson=JsonUtility.ToJson(saxonState);
        Check(JsonUtility.FromJson<GameState>(saxonJson).playerSide==Side.Saxon,
            "Saxon side selection did not survive a save round trip");
        var saxonOptionalState=Setup.New(board,97531,Side.Saxon);
        saxonOptionalState.phase=Phase.NormanFire;
        var saxonOptionalGroup=saxonOptionalState.groups.First(g=>g.id=="Left");
        saxonOptionalGroup.footOptional=true;
        saxonOptionalState.orderResults.Add(new OrderRollResult {group="Left",side=Side.Saxon,
            footOptional=true});
        var saxonOptionalEngine=new GameEngine(board,saxonOptionalState);
        Check(saxonOptionalEngine.SetOptionalOrder("Left",false,Order.AttackPursue) &&
            saxonOptionalGroup.footOrder==Order.AttackPursue && !saxonOptionalGroup.footOptional,
            "Saxon optional order choice was not applied");
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
        var chargeState=Setup.New(board,86420);chargeState.phase=Phase.NormanMove;
        chargeState.groups.First(group=>group.id=="Breton").knightOrder=Order.Charge;
        var chargeEngine=new GameEngine(board,chargeState);
        var chargeUnit=chargeState.units.First(unit=>unit.type=="BK");
        int chargeStartDistance=chargeEngine.NearestEnemyDistance(Side.Norman,chargeUnit.hex);
        var chargeMoves=chargeEngine.LegalMoves(chargeUnit).Values.ToList();
        Check(chargeMoves.Count>0 && chargeMoves.All(move=>
                chargeEngine.NearestEnemyDistance(Side.Norman,move.destination)<chargeStartDistance) &&
              chargeMoves.Select(move=>chargeEngine.NearestEnemyDistance(Side.Norman,move.destination))
                .Distinct().Count()==1,
            "Charge-order movement did not require the closest reachable destinations");
        Check(chargeEngine.RequiredChargeMoves(Side.Norman).Any(unit=>unit.id==chargeUnit.id),
            "Unmoved Charge-order knight was not reported as required movement");
        chargeUnit.moved=true;
        Check(!chargeEngine.RequiredChargeMoves(Side.Norman).Any(unit=>unit.id==chargeUnit.id),
            "Moved Charge-order knight remained in the required movement list");
        var fireState=Setup.New(board,37);fireState.phase=Phase.NormanFire;
        var fireEngine=new GameEngine(board,fireState);
        var bowman=fireState.units.First(u=>u.type=="NB");
        var fireTarget=fireState.units.First(u=>u.type=="HC");
        bowman.hex="1112";fireTarget.hex=board.Adjacent(bowman.hex).First();
        string firedAtHex=fireTarget.hex;
        bowman.facing=board.Direction(bowman.hex,fireTarget.hex);
        fireTarget.facing=board.Direction(fireTarget.hex,bowman.hex);
        Check(fireEngine.Fire(new List<UnitState>{bowman},fireTarget),
            "Legal bow fire was rejected");
        Check(fireEngine.lastFireResult!=null &&
            fireEngine.lastFireResult.shooterIds.SequenceEqual(new[]{bowman.id}) &&
            fireEngine.lastFireResult.targetId==fireTarget.id &&
            fireEngine.lastFireResult.targetHex==firedAtHex &&
            fireEngine.lastFireResult.strength>0 && fireEngine.lastFireResult.defense>0,
            "Missile fire did not expose a visual result");
        var meleeState=Setup.New(board,38);meleeState.phase=Phase.NormanMelee;
        var meleeEngine=new GameEngine(board,meleeState);
        var meleeAttacker=meleeState.units.First(u=>u.type=="NF");
        var meleeDefender=meleeState.units.First(u=>u.type=="HC");
        foreach(var unit in meleeState.units)unit.hex="";
        meleeAttacker.hex="1112";meleeDefender.hex=board.Adjacent(meleeAttacker.hex).First();
        meleeAttacker.facing=board.Direction(meleeAttacker.hex,meleeDefender.hex);
        meleeDefender.facing=board.Direction(meleeDefender.hex,meleeAttacker.hex);
        Check(meleeEngine.Melee(new List<UnitState>{meleeAttacker},
            new List<UnitState>{meleeDefender}),"Legal melee was rejected");
        Check(meleeEngine.lastMeleeResult!=null &&
            meleeEngine.lastMeleeResult.attackerIds.SequenceEqual(new[]{meleeAttacker.id}) &&
            meleeEngine.lastMeleeResult.defenderIds.SequenceEqual(new[]{meleeDefender.id}) &&
            meleeEngine.lastMeleeResult.attackerHexes[0]=="1112" &&
            meleeEngine.lastMeleeResult.attack>0 && meleeEngine.lastMeleeResult.defense>0 &&
            !string.IsNullOrEmpty(meleeEngine.lastMeleeResult.tableResult),
            "Melee did not expose a visual result");
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
        Check(InterfaceThemeCatalog.All.Length==3 &&
            InterfaceThemeCatalog.All.Distinct().Count()==3,"Expected three interface themes");
        foreach(var theme in InterfaceThemeCatalog.All)
        {
            var skin=InterfaceThemeCatalog.Get(theme);
            Check(skin!=null && !string.IsNullOrEmpty(skin.displayName) &&
                !string.IsNullOrEmpty(skin.description),"Interface theme metadata missing: "+theme);
            Check(skin.panelTexture!=null && skin.cardTexture!=null &&
                skin.buttonTexture!=null && skin.primaryTexture!=null,
                "Interface theme textures missing: "+theme);
            Check(skin.ornament!=null && skin.divider!=null,
                "Interface theme ornament missing: "+theme);
            foreach(var icon in new[]{"menu","hide","focus","fit","melee","missile",
                "morale","terrain","rulebook","controls"})
                Check(skin.Icon(icon)!=null,"Interface theme icon missing: "+theme+" "+icon);
            Check(skin.buttonTexture.width==80 && skin.buttonTexture.height==48,
                "Interface button texture has the wrong dimensions: "+theme);
        }
        Debug.Log("HASTINGS CHECKS PASSED: map, tables, art, setup and state round trip");
    }
    public static void RunSimulation()
    {
        var board=new Board(JsonUtility.FromJson<MapData>(Resources.Load<TextAsset>("Data/Map").text));
        foreach(uint seed in new uint[]{4077,1819,2026})Simulate(board,seed,Side.Norman);
        Simulate(board,1066,Side.Saxon);
    }
    private static void Simulate(Board board,uint seed,Side playerSide)
    {
        var engine=new GameEngine(board,Setup.New(board,seed,playerSide));engine.Begin();
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
        Debug.Log("HASTINGS SIMULATION PASSED "+playerSide+" seed "+seed+": "+engine.state.result+
                  ", "+engine.state.log.Count+" events");
    }
    private static void Check(bool condition,string message)
    { if(!condition)throw new Exception("HASTINGS CHECK FAILED: "+message); }
}
