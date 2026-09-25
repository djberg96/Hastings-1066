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
        string facingHex="1112";
        for(int facing=0;facing<6;facing++)
        {
            var origin=new Vector2(board.Hex(facingHex).x,board.Hex(facingHex).y);
            var front=board.Adjacent(facingHex).Where(hex=>
                board.Direction(facingHex,hex)==facing ||
                board.Direction(facingHex,hex)==(facing+1)%6)
                .Select(hex=>new Vector2(board.Hex(hex).x,board.Hex(hex).y)-origin).ToArray();
            float rotation=BoardViewMath.FacingRotationDegrees(facing,false)*Mathf.Deg2Rad;
            var arrow=new Vector2(Mathf.Sin(rotation),-Mathf.Cos(rotation));
            Check(front.Length==2 && Vector2.Dot(arrow.normalized,(front[0]+front[1]).normalized)>.99f,
                "Counter art facing disagrees with frontal hexes for direction "+facing);
            Check(Mathf.DeltaAngle(BoardViewMath.FacingRotationDegrees(facing,true),
                    BoardViewMath.FacingRotationDegrees(facing,false)+180f)==0,
                "Rotated counter facing disagrees with the Saxon view");
        }
        Check(BoardViewMath.TurnFacing(1,true)==0 && BoardViewMath.TurnFacing(1,false)==2,
            "Q/E facing controls do not turn right and left as labeled");
        foreach(Strategy strategy in Enum.GetValues(typeof(Strategy)))
            for(int dice=2;dice<=12;dice++)
            {
                int duration,effect;bool optional;
                RuleTables.RollOrder(Side.Saxon,false,strategy,dice,out duration,out effect,out optional);
                RuleTables.RollOrder(Side.Norman,false,strategy,dice,out duration,out effect,out optional);
                RuleTables.RollOrder(Side.Norman,true,strategy,dice,out duration,out effect,out optional);
            }
        Check(Enumerable.Range(1,6).All(roll=>RuleTables.MeleeResult(-6,roll)=="1/-") &&
              RuleTables.MeleeResult(-5,6)=="M/-",
            "A melee differential of exactly -6 was not an automatic attacker step loss");
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
        var bowOrderState=Setup.New(board,12346);
        var normanBow=bowOrderState.units.First(unit=>unit.type=="NB");
        var normanFoot=bowOrderState.units.First(unit=>unit.type=="NF");
        var saxonBow=bowOrderState.units.First(unit=>unit.type=="SB");
        var saxonFoot=bowOrderState.units.First(unit=>unit.type=="HC");
        bowOrderState.groups.First(group=>group.id==normanBow.group).footOrder=Order.ShieldWall;
        bowOrderState.groups.First(group=>group.id==saxonBow.group).footOrder=Order.ShieldWall;
        var bowOrderEngine=new GameEngine(board,bowOrderState);
        Check(bowOrderEngine.OrderFor(normanBow)==Order.FireInPlace &&
              bowOrderEngine.OrderFor(normanFoot)==Order.ShieldWall &&
              bowOrderEngine.OrderFor(saxonBow)==Order.FireInPlace &&
              bowOrderEngine.OrderFor(saxonFoot)==Order.ShieldWall,
            "Bowmen adopted Shield Wall instead of Melee/Fire in Place");
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
                result.footRoll==result.knightRoll && result.roll==result.footRoll &&
                result.footOrder==expectedFoot && result.knightOrder==expectedKnights &&
                result.effectChange==footEffect+knightEffect,
                result.group+" did not read one nationality roll for foot and knights");
        }
        Check(state.orderResults.Where(r=>r.side==Side.Norman)
            .All(r=>r.footRoll==r.knightRoll),
            "Norman foot and knight sections did not reuse the nationality roll");
        GameState extendedNormanState=null;GameEngine extendedNormanEngine=null;
        GroupState normanRerollGroup=null;OrderRollResult normanRerollResult=null;
        for(uint extendedSeed=1;extendedSeed<=100 && normanRerollGroup==null;extendedSeed++)
        {
            var candidate=Setup.New(board,extendedSeed,Side.Norman);
            candidate.period=1;candidate.turn=9;candidate.extendedTo=10;candidate.phase=Phase.Orders;
            var candidateEngine=new GameEngine(board,candidate);candidateEngine.ResolveOrders();
            var pending=candidate.groups.FirstOrDefault(group=>group.footReroll||group.knightReroll);
            if(pending==null)continue;
            extendedNormanState=candidate;extendedNormanEngine=candidateEngine;
            normanRerollGroup=pending;
            normanRerollResult=candidate.orderResults.Last(result=>result.group==pending.id);
        }
        Check(normanRerollGroup!=null && extendedNormanEngine.OptionsPending(),
            "Extended Norman Shield Wall/Hold result did not offer a reroll");
        bool rerollKnights=normanRerollGroup.knightReroll;
        int originalSectionRoll=rerollKnights?normanRerollResult.knightRoll:normanRerollResult.footRoll;
        Check(extendedNormanEngine.ResolveExtendedReroll(normanRerollGroup.id,
                rerollKnights,true) &&
              (rerollKnights?normanRerollResult.knightRerolled:normanRerollResult.footRerolled) &&
              (rerollKnights?normanRerollResult.knightRoll:normanRerollResult.footRoll)>=2 &&
              normanRerollResult.totalEffect==normanRerollGroup.effect,
            "Extended Norman section reroll did not replace its order roll and effect");
        Check(originalSectionRoll>=2,"Extended Norman reroll lost its original section roll");
        foreach(var pending in extendedNormanState.groups.Where(group=>group.footReroll||
            group.knightReroll).ToList())
        {
            if(pending.footReroll)extendedNormanEngine.ResolveExtendedReroll(pending.id,false,false);
            if(pending.knightReroll)extendedNormanEngine.ResolveExtendedReroll(pending.id,true,false);
        }
        GameState extendedSaxonState=null;GameEngine extendedSaxonEngine=null;
        GroupState saxonRerollGroup=null;OrderRollResult saxonRerollResult=null;
        for(uint extendedSeed=101;extendedSeed<=250 && saxonRerollGroup==null;extendedSeed++)
        {
            var candidate=Setup.New(board,extendedSeed,Side.Saxon);
            candidate.period=1;candidate.turn=9;candidate.extendedTo=10;candidate.phase=Phase.Orders;
            var candidateEngine=new GameEngine(board,candidate);candidateEngine.ResolveOrders();
            var pending=candidate.groups.FirstOrDefault(group=>group.footReroll);
            if(pending==null)continue;
            extendedSaxonState=candidate;extendedSaxonEngine=candidateEngine;
            saxonRerollGroup=pending;
            saxonRerollResult=candidate.orderResults.Last(result=>result.group==pending.id);
        }
        Check(saxonRerollGroup!=null && saxonRerollResult.footOrder==Order.AttackPursue &&
              extendedSaxonEngine.ResolveExtendedReroll(saxonRerollGroup.id,false,false) &&
              !saxonRerollGroup.footReroll && !saxonRerollResult.footRerolled,
            "Extended Saxon Attack & Pursue result did not offer a keep-or-reroll choice");
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
        var saxonFacingUnit=saxonState.units.First(unit=>unit.side==Side.Saxon &&
            !UnitTypes.Get(unit).leader);
        var normanFacingUnit=saxonState.units.First(unit=>unit.side==Side.Norman &&
            !UnitTypes.Get(unit).leader);
        Check(saxonEngine.CanFace(saxonFacingUnit) && !saxonEngine.CanFace(normanFacingUnit) &&
              saxonEngine.Face(saxonFacingUnit,(saxonFacingUnit.facing+1)%6),
            "A Saxon player could not set Saxon facings during setup");
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
        var dueReinforcements=saxonState.units.Where(u=>u.side==Side.Saxon && u.hex=="" &&
            u.reservePeriod==saxonState.period && u.reserveTurn==saxonState.turn).ToList();
        Check(dueReinforcements.Count==2,"Opening Saxon reinforcements were not scheduled");
        saxonEngine.Advance();
        Check(saxonState.phase==Phase.SaxonMove &&
              dueReinforcements.All(u=>board.Has(u.hex) && u.reservePeriod==0 &&
                !u.reserveOrder && u.group=="Center"),
            "Human Saxon reinforcements did not enter and join Harold's wing");
        Check(dueReinforcements.Select(u=>u.hex).Distinct().Count()==dueReinforcements.Count,
            "Human Saxon reinforcements entered in the same hex");
        saxonEngine.Advance();
        Check(saxonState.phase==Phase.SaxonMelee,"Saxon melee phase missing");
        var saxonJson=JsonUtility.ToJson(saxonState);
        Check(JsonUtility.FromJson<GameState>(saxonJson).playerSide==Side.Saxon,
            "Saxon side selection did not survive a save round trip");
        var wingState=Setup.New(board,24681,Side.Saxon);wingState.phase=Phase.Orders;
        var wingEngine=new GameEngine(board,wingState);
        Check(wingEngine.SaxonWingProblem()=="","Initial Saxon wings violated minimum sizes");
        var wingLeaders=wingEngine.AvailableSaxonLeaders();
        var legalAssignment=wingEngine.Living(Side.Saxon)
            .Where(unit=>!UnitTypes.Get(unit).leader)
            .SelectMany(unit=>wingLeaders.Where(leader=>leader.group!=unit.group)
                .Select(leader=>new{unit,leader}))
            .First(pair=>wingEngine.CanAssignSaxonLeader(pair.unit,pair.leader));
        Check(wingEngine.SetSaxonLeader(legalAssignment.unit,legalAssignment.leader) &&
              legalAssignment.unit.group==legalAssignment.leader.group,
            "A Saxon player could not assign a unit to a leader");
        var illegalAssignment=wingEngine.Living(Side.Saxon)
            .Where(unit=>!UnitTypes.Get(unit).leader)
            .SelectMany(unit=>wingLeaders.Select(leader=>new{unit,leader}))
            .First(pair=>!wingEngine.CanAssignSaxonLeader(pair.unit,pair.leader));
        Check(!wingEngine.SetSaxonLeader(illegalAssignment.unit,illegalAssignment.leader),
            "A Saxon unit was assigned beyond a leader's command radius");
        foreach(var unit in wingEngine.Living(Side.Saxon).Where(unit=>!UnitTypes.Get(unit).leader))
            unit.group="Left";
        Check(wingEngine.SaxonWingProblem()!="",
            "Undersized Saxon wings were accepted during the order phase");
        var aiMovementState=Setup.New(board,24683,Side.Norman);
        aiMovementState.phase=Phase.NormanMelee;
        foreach(var group in aiMovementState.groups.Where(group=>
            group.id=="Left" || group.id=="Center" || group.id=="Right"))
            group.footOrder=Order.AttackPursue;
        var aiMovementEngine=new GameEngine(board,aiMovementState);
        aiMovementEngine.Advance();
        Check(aiMovementEngine.automaticMovements.Any(movement=>
                movement.kind=="Saxon movement" && movement.path.Count>1),
            "Saxon AI movement did not retain paths for animation");
        var saxonReformState=Setup.New(board,24682,Side.Saxon);saxonReformState.phase=Phase.Reform;
        var saxonReformEngine=new GameEngine(board,saxonReformState);
        var saxonReformUnit=saxonReformState.units.First(unit=>unit.side==Side.Saxon &&
            !UnitTypes.Get(unit).leader);
        string saxonReformHex=board.data.hexes.Where(hex=>hex.level>=4 &&
                int.Parse(hex.id.Substring(0,2))<=8 &&
                saxonReformEngine.UnitAt(hex.id,Side.Saxon)==null &&
                saxonReformEngine.UnitAt(hex.id,Side.Norman)==null)
            .Select(hex=>hex.id).First();
        Check(saxonReformEngine.ReformMove(saxonReformUnit,saxonReformHex) &&
              !saxonReformEngine.ReformMove(normanFacingUnit,saxonReformHex),
            "Saxon reform placement was not controlled by the Saxon player");
        var saxonOptionalState=Setup.New(board,97531,Side.Saxon);
        saxonOptionalState.phase=Phase.NormanFire;
        var saxonOptionalGroup=saxonOptionalState.groups.First(g=>g.id=="Left");
        saxonOptionalGroup.footOptional=true;
        saxonOptionalState.orderResults.Add(new OrderRollResult {group="Left",side=Side.Saxon,
            footOptional=true});
        var saxonOptionalEngine=new GameEngine(board,saxonOptionalState);
        Check(saxonOptionalEngine.SetOptionalOrder("Left",false,Order.FireInPlace) &&
            saxonOptionalGroup.footOrder==Order.FireInPlace && !saxonOptionalGroup.footOptional &&
            saxonOptionalState.orderResults[0].footOrder==Order.FireInPlace,
            "Saxon optional result did not allow Melee/Fire in Place");
        saxonOptionalGroup.footOptional=true;
        Check(saxonOptionalEngine.SetOptionalOrder("Left",false,Order.AttackPursue) &&
            saxonOptionalGroup.footOrder==Order.AttackPursue,
            "Saxon optional result no longer allowed Attack & Pursue");
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
        var recordedChargeMove=chargeMoves.OrderBy(move=>move.destination).First();
        string recordedChargeOrigin=chargeUnit.hex;
        Check(chargeEngine.Move(chargeUnit,recordedChargeMove.destination) &&
              chargeEngine.lastMovementResult!=null &&
              chargeEngine.lastMovementResult.chargeOrder &&
              chargeEngine.lastMovementResult.originHex==recordedChargeOrigin &&
              chargeEngine.lastMovementResult.finalHex==chargeUnit.hex &&
              chargeEngine.lastMovementResult.path.First()==recordedChargeOrigin &&
              chargeEngine.lastMovementResult.path.Last()==chargeUnit.hex,
            "Charge-order movement did not record its traversed path");
        Check(!chargeEngine.RequiredChargeMoves(Side.Norman).Any(unit=>unit.id==chargeUnit.id),
            "Moved Charge-order knight remained in the required movement list");
        var ridgeState=Setup.New(board,1);ridgeState.phase=Phase.NormanMove;
        foreach(var unit in ridgeState.units)unit.status=Status.Eliminated;
        var ridgeKnight=ridgeState.units.First(unit=>unit.type=="BK");
        var ridgeEnemy=ridgeState.units.First(unit=>unit.type=="HC");
        var ridgeEdge=board.data.edges.First(edge=>edge.ridge &&
            board.Adjacent(edge.b).Any(hex=>hex!=edge.a && board.Distance(edge.a,hex)==2));
        string ridgeTarget=board.Adjacent(ridgeEdge.b)
            .First(hex=>hex!=ridgeEdge.a && board.Distance(ridgeEdge.a,hex)==2);
        ridgeKnight.status=Status.Ready;ridgeKnight.hex=ridgeEdge.a;
        ridgeEnemy.status=Status.Ready;ridgeEnemy.hex=ridgeTarget;
        ridgeEnemy.facing=board.Direction(ridgeTarget,ridgeEdge.b);
        ridgeState.groups.First(group=>group.id==ridgeKnight.group).knightOrder=Order.Charge;
        ridgeState.randomState=1;
        var ridgeEngine=new GameEngine(board,ridgeState);
        Check(ridgeEngine.LegalMoves(ridgeKnight).ContainsKey(ridgeEdge.b) &&
              ridgeEngine.Move(ridgeKnight,ridgeEdge.b) &&
              ridgeKnight.status==Status.Disrupted && ridgeKnight.hex==ridgeEdge.a &&
              ridgeEngine.lastMovementResult.interrupted &&
              ridgeEngine.lastMovementResult.interruptionCause=="ridge" &&
              ridgeEngine.lastMovementResult.interruptionFrom==ridgeEdge.a &&
              ridgeEngine.lastMovementResult.interruptionTo==ridgeEdge.b,
            "Failed ridge morale did not stop and record Charge-order movement before crossing");
        var guardState=Setup.New(board,86421);guardState.phase=Phase.NormanMove;
        var guardEngine=new GameEngine(board,guardState);
        var guard=guardState.units.First(unit=>unit.type=="WG");
        var guardGroup=guardState.groups.First(group=>group.id==guard.group);
        guardGroup.knightOrder=Order.Advance;
        Check(guardEngine.OrderFor(guard)==Order.Advance &&
              guardEngine.MovementAllowance(guard)==6,
            "William's living Guard did not receive its independent charge allowance");
        guardState.units.First(unit=>unit.type=="William").status=Status.Eliminated;
        Check(guardEngine.OrderFor(guard)==Order.Advance &&
              guardEngine.MovementAllowance(guard)==4,
            "William's Guard retained its independent charge allowance after William was lost");
        guardGroup.knightOrder=Order.Charge;
        Check(guardEngine.OrderFor(guard)==Order.Charge &&
              guardEngine.MovementAllowance(guard)==6,
            "William's Guard did not follow the Norman knight order after William was lost");
        var pursueState=Setup.New(board,97531);pursueState.phase=Phase.SaxonMove;
        foreach(var unit in pursueState.units)unit.status=Status.Eliminated;
        var pursueUnit=pursueState.units.First(unit=>unit.type=="F1");
        var pursueTarget=pursueState.units.First(unit=>unit.type=="NF");
        pursueUnit.status=Status.Ready;pursueUnit.hex="1112";
        pursueTarget.status=Status.Ready;
        pursueTarget.hex=board.data.hexes.Select(hex=>hex.id)
            .First(hex=>board.Distance(pursueUnit.hex,hex)==4);
        pursueState.groups.First(group=>group.id==pursueUnit.group).footOrder=Order.AttackPursue;
        var pursueEngine=new GameEngine(board,pursueState);
        int pursueStartDistance=pursueEngine.NearestEnemyDistance(Side.Saxon,pursueUnit.hex);
        var pursueMoves=pursueEngine.LegalMoves(pursueUnit).Values.ToList();
        Check(pursueMoves.Count>0 && pursueMoves.All(move=>
                pursueEngine.NearestEnemyDistance(Side.Saxon,move.destination)<pursueStartDistance) &&
              pursueMoves.Select(move=>pursueEngine.NearestEnemyDistance(
                    Side.Saxon,move.destination)).Distinct().Count()==1,
            "Attack & Pursue movement did not require the closest reachable destinations");
        Check(pursueEngine.RequiredAttackPursueMoves(Side.Saxon)
                .Any(unit=>unit.id==pursueUnit.id),
            "Unmoved Attack & Pursue unit was not reported as required movement");
        pursueTarget.hex="1112";
        pursueUnit.hex=board.Adjacent(pursueTarget.hex).First();
        pursueTarget.facing=board.Direction(pursueTarget.hex,pursueUnit.hex);
        string adjacentDestination=board.Adjacent(pursueTarget.hex).First(hex=>
            hex!=pursueUnit.hex && board.Direction(pursueTarget.hex,hex)==
                (pursueTarget.facing+1)%6);
        var adjacentPursueMoves=pursueEngine.LegalMoves(pursueUnit);
        Check(adjacentPursueMoves.ContainsKey(adjacentDestination) &&
              adjacentPursueMoves.Values.All(move=>
                pursueEngine.NearestEnemyDistance(Side.Saxon,move.destination)==1) &&
              !pursueEngine.RequiredAttackPursueMoves(Side.Saxon)
                .Any(unit=>unit.id==pursueUnit.id),
            "Attack & Pursue did not permit optional movement between adjacent enemy hexes");
        var bowExitState=Setup.New(board,97532);bowExitState.phase=Phase.NormanMove;
        foreach(var unit in bowExitState.units)unit.status=Status.Eliminated;
        var trappedBow=bowExitState.units.First(unit=>unit.type=="NB");
        var bowEnemy=bowExitState.units.First(unit=>unit.type=="HC");
        trappedBow.status=Status.Ready;trappedBow.hex="1112";
        bowEnemy.status=Status.Ready;bowEnemy.hex=board.Adjacent(trappedBow.hex).First();
        bowEnemy.facing=board.Direction(bowEnemy.hex,trappedBow.hex);
        bowExitState.groups.First(group=>group.id==trappedBow.group).footOrder=Order.Advance;
        var bowExitEngine=new GameEngine(board,bowExitState);
        var bowExitMoves=bowExitEngine.LegalMoves(trappedBow);
        Check(bowExitEngine.InEnemyZoc(Side.Norman,trappedBow.hex) && bowExitMoves.Count>0 &&
              bowExitMoves.Keys.All(hex=>!bowExitEngine.InEnemyZoc(Side.Norman,hex)) &&
              bowExitEngine.RequiredBowmenZocMoves(Side.Norman)
                .Any(unit=>unit.id==trappedBow.id) &&
              bowExitEngine.RequiredMovementUnits(Side.Norman)
                .Count(unit=>unit.id==trappedBow.id)==1,
            "A bowman with a legal enemy-ZOC exit was not required to leave");
        trappedBow.moved=true;
        Check(!bowExitEngine.RequiredBowmenZocMoves(Side.Norman)
                .Any(unit=>unit.id==trappedBow.id),
            "A bowman that left an enemy ZOC remained in the required movement list");
        var reserveContactState=Setup.New(board,97533,Side.Saxon);
        reserveContactState.phase=Phase.SaxonMove;
        foreach(var unit in reserveContactState.units)unit.status=Status.Eliminated;
        var reserveUnit=reserveContactState.units.First(unit=>unit.type=="F1");
        var reserveEnemy=reserveContactState.units.First(unit=>unit.type=="NF");
        var reserveLeader=reserveContactState.units.First(unit=>unit.type=="Gyrth");
        reserveUnit.status=Status.Ready;reserveUnit.hex="1112";reserveUnit.reserveOrder=true;
        reserveUnit.group="Right";
        reserveEnemy.status=Status.Ready;
        reserveEnemy.hex=board.data.hexes.Select(hex=>hex.id)
            .First(hex=>board.Distance(reserveUnit.hex,hex)==2);
        int reserveEnemyTowardUnit=board.Direction(reserveEnemy.hex,reserveUnit.hex);
        reserveEnemy.facing=(reserveEnemyTowardUnit+2)%6;
        reserveLeader.status=Status.Ready;
        reserveLeader.hex=board.data.hexes.Select(hex=>hex.id)
            .OrderByDescending(hex=>board.Distance(hex,reserveUnit.hex)).First();
        var reserveContactEngine=new GameEngine(board,reserveContactState);
        var contactMove=reserveContactEngine.LegalMoves(reserveUnit).Values.First(move=>
            board.Distance(move.destination,reserveEnemy.hex)==1);
        Check(board.Distance(reserveLeader.hex,contactMove.destination)>
                UnitTypes.Get(reserveLeader).command &&
              reserveContactEngine.Move(reserveUnit,contactMove.destination) &&
              !reserveUnit.reserveOrder && reserveUnit.group==reserveLeader.group,
            "A reinforcement retained its temporary Advance order after enemy contact");
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
            fireEngine.recentFireResults.Count==1 &&
            fireEngine.recentFireResults[0]==fireEngine.lastFireResult &&
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
            meleeEngine.recentMeleeResults.Count==1 &&
            meleeEngine.recentMeleeResults[0]==meleeEngine.lastMeleeResult &&
            meleeEngine.lastMeleeResult.attackerIds.SequenceEqual(new[]{meleeAttacker.id}) &&
            meleeEngine.lastMeleeResult.defenderIds.SequenceEqual(new[]{meleeDefender.id}) &&
            meleeEngine.lastMeleeResult.attackerHexes[0]=="1112" &&
            meleeEngine.lastMeleeResult.attack>0 && meleeEngine.lastMeleeResult.defense>0 &&
            !string.IsNullOrEmpty(meleeEngine.lastMeleeResult.tableResult),
            "Melee did not expose a visual result");
        var downhillState=Setup.New(board,380);downhillState.phase=Phase.NormanMelee;
        downhillState.playerSide=Side.Norman;
        foreach(var unit in downhillState.units)unit.hex="";
        var downhillKnight=downhillState.units.First(unit=>unit.type=="NK");
        var downhillDefender=downhillState.units.First(unit=>unit.type=="HC");
        string downhillOrigin=board.data.hexes.Select(hex=>hex.id).First(from=>
            board.Adjacent(from).Any(to=>board.Hex(from).level>board.Hex(to).level &&
                (board.Edge(from,to)==null || !board.Edge(from,to).ridge)));
        string downhillTarget=board.Adjacent(downhillOrigin).First(to=>
            board.Hex(downhillOrigin).level>board.Hex(to).level &&
            (board.Edge(downhillOrigin,to)==null || !board.Edge(downhillOrigin,to).ridge));
        downhillKnight.hex=downhillOrigin;downhillDefender.hex=downhillTarget;
        downhillKnight.facing=board.Direction(downhillOrigin,downhillTarget);
        downhillDefender.facing=board.Direction(downhillTarget,downhillOrigin);
        downhillState.groups.First(group=>group.id==downhillKnight.group).knightOrder=Order.Advance;
        var chargedDownhillState=JsonUtility.FromJson<GameState>(JsonUtility.ToJson(downhillState));
        var downhillEngine=new GameEngine(board,downhillState);
        Check(downhillEngine.Melee(new List<UnitState>{downhillKnight},
                new List<UnitState>{downhillDefender}),
            "Legal downhill knight melee was rejected");
        int ordinaryDownhillAttack=downhillEngine.lastMeleeResult.attack;
        var chargedDownhillKnight=chargedDownhillState.units.First(unit=>unit.id==downhillKnight.id);
        var chargedDownhillDefender=chargedDownhillState.units.First(unit=>unit.id==downhillDefender.id);
        chargedDownhillKnight.charged=true;
        var chargedDownhillEngine=new GameEngine(board,chargedDownhillState);
        Check(chargedDownhillEngine.Melee(new List<UnitState>{chargedDownhillKnight},
                new List<UnitState>{chargedDownhillDefender}) &&
              chargedDownhillEngine.lastMeleeResult.attack==ordinaryDownhillAttack+1,
            "A downhill charge received both the ordinary downhill and charge bonuses");
        var leaderBonusState=Setup.New(board,383);leaderBonusState.phase=Phase.NormanMelee;
        leaderBonusState.playerSide=Side.Norman;
        foreach(var unit in leaderBonusState.units)unit.hex="";
        var bretonFoot=leaderBonusState.units.First(unit=>unit.type=="BF");
        var leaderBonusDefender=leaderBonusState.units.First(unit=>unit.type=="HC");
        bretonFoot.hex="1112";
        leaderBonusDefender.hex=board.Adjacent(bretonFoot.hex).First();
        bretonFoot.facing=board.Direction(bretonFoot.hex,leaderBonusDefender.hex);
        leaderBonusDefender.facing=board.Direction(leaderBonusDefender.hex,bretonFoot.hex);
        leaderBonusState.groups.First(group=>group.id==bretonFoot.group).footOrder=Order.Advance;
        var wrongLeaderState=JsonUtility.FromJson<GameState>(JsonUtility.ToJson(leaderBonusState));
        wrongLeaderState.units.First(unit=>unit.type=="Odo").hex=bretonFoot.hex;
        var rightLeaderState=JsonUtility.FromJson<GameState>(JsonUtility.ToJson(leaderBonusState));
        rightLeaderState.units.First(unit=>unit.type=="Alan").hex=bretonFoot.hex;
        var noLeaderEngine=new GameEngine(board,leaderBonusState);
        Check(noLeaderEngine.Melee(new List<UnitState>{bretonFoot},
                new List<UnitState>{leaderBonusDefender}),"Leader bonus baseline melee was rejected");
        int noLeaderAttack=noLeaderEngine.lastMeleeResult.attack;
        var wrongLeaderEngine=new GameEngine(board,wrongLeaderState);
        Check(wrongLeaderEngine.Melee(
                new List<UnitState>{wrongLeaderState.units.First(unit=>unit.id==bretonFoot.id)},
                new List<UnitState>{wrongLeaderState.units.First(unit=>unit.id==leaderBonusDefender.id)}) &&
              wrongLeaderEngine.lastMeleeResult.attack==noLeaderAttack,
            "A Norman subordinate aided a unit of another nationality");
        var rightLeaderEngine=new GameEngine(board,rightLeaderState);
        Check(rightLeaderEngine.Melee(
                new List<UnitState>{rightLeaderState.units.First(unit=>unit.id==bretonFoot.id)},
                new List<UnitState>{rightLeaderState.units.First(unit=>unit.id==leaderBonusDefender.id)}) &&
              rightLeaderEngine.lastMeleeResult.attack==noLeaderAttack+1,
            "A Norman subordinate did not aid a unit of his own nationality");
        var woundState=Setup.New(board,384);
        var woundEngine=new GameEngine(board,woundState);
        var woundedLeader=woundState.units.First(unit=>unit.type=="Alan");
        woundedLeader.leaderCondition=2;
        woundEngine.ApplyLeaderCasualtyRoll(woundedLeader,false,3);
        Check(woundedLeader.status==Status.Eliminated,
            "A leader wounded from both casualty rows survived a second wound");
        var crossWoundState=Setup.New(board,385);
        var crossWoundEngine=new GameEngine(board,crossWoundState);
        var crossWoundedLeader=crossWoundState.units.First(unit=>unit.type=="Alan");
        crossWoundedLeader.leaderPenalty=1;
        crossWoundEngine.ApplyLeaderCasualtyRoll(crossWoundedLeader,true,4);
        Check(crossWoundedLeader.status==Status.Eliminated,
            "A permanently wounded leader survived an assault-long wound");
        var shakenWoundState=Setup.New(board,386);
        var shakenWoundEngine=new GameEngine(board,shakenWoundState);
        var shakenWoundedLeader=shakenWoundState.units.First(unit=>unit.type=="Alan");
        shakenWoundedLeader.leaderCondition=1;shakenWoundedLeader.shakenUntil=2;
        shakenWoundEngine.ApplyLeaderCasualtyRoll(shakenWoundedLeader,false,3);
        Check(shakenWoundedLeader.status!=Status.Eliminated &&
              shakenWoundedLeader.leaderPenalty==1 && shakenWoundedLeader.leaderCondition==1,
            "A shaken leader did not become wounded but remain alive");
        var repeatedShakeState=Setup.New(board,387);
        var repeatedShakeEngine=new GameEngine(board,repeatedShakeState);
        var repeatedShakeLeader=repeatedShakeState.units.First(unit=>unit.type=="Alan");
        repeatedShakeLeader.leaderCondition=1;repeatedShakeLeader.shakenUntil=2;
        repeatedShakeEngine.ApplyLeaderCasualtyRoll(repeatedShakeLeader,true,5,3);
        Check(repeatedShakeLeader.status==Status.Eliminated,
            "A repeated shaken result did not apply its 1d6 kill check");
        var survivingShakeState=Setup.New(board,388);
        var survivingShakeEngine=new GameEngine(board,survivingShakeState);
        var survivingShakeLeader=survivingShakeState.units.First(unit=>unit.type=="Alan");
        survivingShakeLeader.leaderCondition=1;survivingShakeLeader.shakenUntil=2;
        survivingShakeEngine.ApplyLeaderCasualtyRoll(survivingShakeLeader,true,9,4);
        Check(survivingShakeLeader.status!=Status.Eliminated &&
              survivingShakeLeader.leaderCondition==1 && survivingShakeLeader.shakenUntil==2,
            "A leader that survived a repeated shaken result had its duration changed");
        CheckRetreatAndDisplacement();
        var obligationState=Setup.New(board,381);obligationState.phase=Phase.NormanMelee;
        obligationState.playerSide=Side.Norman;
        foreach(var unit in obligationState.units)unit.hex="";
        var obligationAttacker=obligationState.units.First(u=>u.type=="NF");
        var mutualDefender=obligationState.units.First(u=>u.type=="HC");
        var oneWayDefender=obligationState.units.Where(u=>u.type=="HC").Skip(1).First();
        obligationAttacker.hex="1112";obligationAttacker.facing=0;
        var frontalHexes=board.Adjacent(obligationAttacker.hex).Where(hex=>
            board.Direction(obligationAttacker.hex,hex)==obligationAttacker.facing ||
            board.Direction(obligationAttacker.hex,hex)==(obligationAttacker.facing+1)%6).ToList();
        Check(frontalHexes.Count==2,"Could not create two-target melee check");
        mutualDefender.hex=frontalHexes[0];oneWayDefender.hex=frontalHexes[1];
        mutualDefender.facing=board.Direction(mutualDefender.hex,obligationAttacker.hex);
        int towardAttacker=board.Direction(oneWayDefender.hex,obligationAttacker.hex);
        oneWayDefender.facing=(towardAttacker+2)%6;
        obligationState.groups.First(group=>group.id==obligationAttacker.group).footOrder=Order.Advance;
        var obligationEngine=new GameEngine(board,obligationState);
        Check(obligationEngine.MandatoryMeleeTargets(obligationAttacker)
                .Select(unit=>unit.id).SequenceEqual(new[]{mutualDefender.id}),
            "Mutual-ZOC defender was not the sole mandatory target");
        Check(obligationEngine.RequiredMeleeAttackers(Side.Norman)
                .Any(unit=>unit.id==obligationAttacker.id),
            "Mutual-ZOC attacker was not marked for mandatory melee");
        Check(!obligationEngine.Melee(new List<UnitState>{obligationAttacker},
                new List<UnitState>{oneWayDefender}),
            "Attacker was allowed to evade mandatory melee by attacking a one-way target");
        Check(!obligationEngine.Melee(new List<UnitState>{obligationAttacker},
                new List<UnitState>{mutualDefender,oneWayDefender}),
            "Attacker with one mutual ZOC was allowed to add a non-mutual target");
        Check(obligationEngine.Melee(new List<UnitState>{obligationAttacker},
                new List<UnitState>{mutualDefender}),
            "Required mutual-ZOC melee was rejected");
        var twoTargetState=Setup.New(board,382);twoTargetState.phase=Phase.NormanMelee;
        twoTargetState.playerSide=Side.Norman;
        foreach(var unit in twoTargetState.units)unit.hex="";
        var twoTargetAttacker=twoTargetState.units.First(u=>u.type=="NF");
        var twoTargetDefenders=twoTargetState.units.Where(u=>u.type=="HC").Take(2).ToList();
        twoTargetAttacker.hex="1112";twoTargetAttacker.facing=0;
        for(int i=0;i<twoTargetDefenders.Count;i++)
        {
            twoTargetDefenders[i].hex=frontalHexes[i];
            twoTargetDefenders[i].facing=board.Direction(twoTargetDefenders[i].hex,twoTargetAttacker.hex);
        }
        var twoTargetGroup=twoTargetState.groups.First(group=>group.id==twoTargetAttacker.group);
        twoTargetGroup.footOrder=Order.Advance;
        var twoTargetEngine=new GameEngine(board,twoTargetState);
        Check(twoTargetEngine.MandatoryMeleeTargets(twoTargetAttacker).Count==2,
            "Both mutual-ZOC defenders were not mandatory");
        twoTargetGroup.footOrder=Order.ShieldWall;
        Check(twoTargetEngine.MandatoryMeleeTargets(twoTargetAttacker).Count==0 &&
              !twoTargetEngine.RequiredMeleeAttackers(Side.Norman)
                .Any(unit=>unit.id==twoTargetAttacker.id),
            "Shield Wall unit was incorrectly required to melee");
        Check(twoTargetEngine.RequiredMeleeTargets(new[]{twoTargetAttacker}).Count==2,
            "Voluntary Shield Wall melee did not include every frontal target");
        twoTargetGroup.footOrder=Order.Advance;
        Check(!twoTargetEngine.Melee(new List<UnitState>{twoTargetAttacker},
                new List<UnitState>{twoTargetDefenders[0]}),
            "Two-target mandatory melee allowed one defender to be omitted");
        Check(twoTargetEngine.Melee(new List<UnitState>{twoTargetAttacker},twoTargetDefenders),
            "Two-target mandatory melee was rejected");
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
            if(s.phase==Phase.Orders)
            {
                if(s.playerSide==Side.Saxon && engine.SaxonWingProblem()!="")
                {
                    var leaders=engine.AvailableSaxonLeaders();
                    var units=engine.Living(Side.Saxon).Where(unit=>!UnitTypes.Get(unit).leader)
                        .OrderBy(unit=>leaders.Count(leader=>
                            engine.CanAssignSaxonLeader(unit,leader)))
                        .ThenBy(unit=>unit.id).ToList();
                    var assigned=leaders.ToDictionary(leader=>leader.id,leader=>0);
                    foreach(var unit in units)
                    {
                        var leader=leaders.Where(candidate=>
                                engine.CanAssignSaxonLeader(unit,candidate))
                            .OrderBy(candidate=>assigned[candidate.id])
                            .ThenBy(candidate=>board.Distance(unit.hex,candidate.hex))
                            .FirstOrDefault();
                        Check(leader!=null && engine.SetSaxonLeader(unit,leader),
                            "Could not assign "+unit.id+" to a Saxon leader in simulation");
                        assigned[leader.id]++;
                    }
                }
                engine.ResolveOrders();
            }
            else if(s.phase==Phase.Reform)
            {
                if(s.playerSide==Side.Norman)
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
                foreach(var group in s.groups.Where(g=>g.footReroll||g.knightReroll).ToList())
                {
                    if(group.footReroll)engine.ResolveExtendedReroll(group.id,false,true);
                    if(group.knightReroll)engine.ResolveExtendedReroll(group.id,true,true);
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
    private static void CheckRetreatAndDisplacement()
    {
        var map=new MapData {
            width=300,height=350,
            hexes=new[]{
                new HexData{id="0101",x=100,y=0},
                new HexData{id="0102",x=200,y=0},
                new HexData{id="0201",x=100,y=86},
                new HexData{id="0301",x=100,y=172},
                new HexData{id="0401",x=100,y=258}
            },
            edges=new[]{
                new EdgeData{a="0401",b="0301"},new EdgeData{a="0301",b="0201"},
                new EdgeData{a="0201",b="0101"},new EdgeData{a="0101",b="0102"}
            }
        };
        var board=new Board(map);
        Func<GameState> newState=()=>new GameState {
            randomState=1,
            groups=new List<GroupState>{new GroupState{id="Center",footOrder=Order.Advance}},
            units=new List<UnitState>()
        };
        var passState=newState();
        var passing=new UnitState{id="routing",type="F1",hex="0401",group="Center",
            side=Side.Saxon,status=Status.Routed};
        var passed=new UnitState{id="passed",type="HC",hex="0301",group="Center",
            side=Side.Saxon,status=Status.Ready};
        passState.units.Add(passing);passState.units.Add(passed);
        new GameEngine(board,passState).Retreat(passing,2);
        Check(passing.hex=="0201" && passed.hex=="0301" && passed.status==Status.Ready,
            "A routed unit displaced a friendly unit that it merely passed through");
        Check(passing.facing==board.Direction("0201","0101"),
            "A routed unit did not face its rear line");

        var chainState=newState();
        var chainRout=new UnitState{id="chain-rout",type="F1",hex="0301",group="Center",
            side=Side.Saxon,status=Status.Routed};
        var firstBlocker=new UnitState{id="block-1",type="HC",hex="0201",group="Center",
            side=Side.Saxon,status=Status.Ready};
        var secondBlocker=new UnitState{id="block-2",type="HC",hex="0101",group="Center",
            side=Side.Saxon,status=Status.Ready};
        chainState.units.AddRange(new[]{chainRout,firstBlocker,secondBlocker});
        new GameEngine(board,chainState).Retreat(chainRout,1);
        Check(chainRout.hex=="0201" && chainRout.status==Status.Routed &&
              firstBlocker.hex=="0101" && firstBlocker.status==Status.Disrupted &&
              secondBlocker.hex=="0102" && secondBlocker.status==Status.Disrupted,
            "A legal chain displacement was not completed atomically");

        var blockedState=newState();
        var blockedRout=new UnitState{id="blocked-rout",type="F1",hex="0301",group="Center",
            side=Side.Saxon,status=Status.Routed};
        var blockedFirst=new UnitState{id="blocked-1",type="HC",hex="0201",group="Center",
            side=Side.Saxon,status=Status.Ready};
        var blockedSecond=new UnitState{id="blocked-2",type="HC",hex="0101",group="Center",
            side=Side.Saxon,status=Status.Ready};
        var enemy=new UnitState{id="enemy",type="NF",hex="0102",group="Norman",
            side=Side.Norman,status=Status.Ready,facing=0};
        blockedState.groups.Add(new GroupState{id="Norman",footOrder=Order.Advance});
        blockedState.units.AddRange(new[]{blockedRout,blockedFirst,blockedSecond,enemy});
        new GameEngine(board,blockedState).Retreat(blockedRout,1);
        Check(blockedRout.hex=="0301" && blockedRout.reduced &&
              blockedRout.status==Status.Disrupted &&
              blockedFirst.hex=="0201" && blockedFirst.status==Status.Ready &&
              blockedSecond.hex=="0101" && blockedSecond.status==Status.Ready,
            "A failed displacement was not atomic or did not reduce and disrupt the routed unit");
    }
    private static void Check(bool condition,string message)
    { if(!condition)throw new Exception("HASTINGS CHECK FAILED: "+message); }
}
