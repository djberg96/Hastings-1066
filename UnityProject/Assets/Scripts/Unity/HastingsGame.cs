using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hastings;
using UnityEngine;

public sealed class HastingsGame : MonoBehaviour
{
    private Board board;
    private GameEngine game;
    private Texture2D map, titleBackground, assaultPeriodMarker, battleTurnMarker,
        movementHex;
    private readonly Dictionary<string,Texture2D> counters=new Dictionary<string,Texture2D>();
    private readonly Dictionary<string,Texture2D> terrainSwatches=new Dictionary<string,Texture2D>();
    private readonly Dictionary<string,float> stackSpread=new Dictionary<string,float>();
    private readonly List<string> selected=new List<string>();
    private readonly List<string> selectedTargets=new List<string>();
    private Vector2 pan;
    private float scale, lastMapWidth, lastMapHeight;
    private float panelScale=1f, chartScale=1f, orderScale=1f;
    private float missileEffectStarted, missileEffectUntil,meleeEffectStarted,meleeEffectUntil;
    private MissileFireResult missileResult;
    private MeleeCombatResult meleeResult;
    private bool showMenu=true, showUnits=true, showHelp, showOrderResults,
        orderReviewMode, showStrategyTrack, fullMapMode;
    private int orderReviewTab;
    private string saveSlot="Game 1", notice="", chart="", menuPage="main",
        highTrajectoryTargetId="";
    private Vector2 panelScroll, chartScroll, menuScroll, orderScroll;
    private GUIStyle small, hexNumber, hexNumberShadow,
        menuTitle, menuSubtitle, menuDescription, menuButton, menuPrimary, menuTextField,
        panelTitle, panelStatus, panelSection, panelBody, panelMuted, panelValue,
        panelCard, panelButton, panelNavButton, panelPrimary, panelLink,
        fireModeButton, fireModeSelected,
        panelBretonButton, panelNormanButton, panelFlemishButton,
        strategyHeading, strategyScale, strategyMarker, strategyLegend, strategyToggle,
        strategyBand, strategyEffectNote,
        missileMapResult, missileMapDetail, statusMarker,
        controlHeading, controlBadge, controlAction, controlRow, fireCompleteBadge,
        orderTitle, orderSubtitle, orderSection, orderCard, orderCardTitle,
        orderRoll, orderType, orderName, orderText, orderChoice, orderEffect,
        orderEffectHeading, orderEffectValue, orderEffectDetail, orderEffectWarning,
        chartTitle, chartSection, chartHeader, chartRowHeader, chartCell, chartMuted, chartNote,
        chartTab, chartTabSelected, chartClose, trackMarkerText, trackMarkerTextShadow;
    private string hoveredHex="";
    private const float StackSpreadSeconds=.22f;
    private const string DisplayPrefsVersion="display-prefs-version";

    private void Awake()
    {
        RestoreWindowedDisplay();
        var asset=Resources.Load<TextAsset>("Data/Map");
        if(asset==null){Debug.LogError("Map.json is missing");return;}
        board=new Board(JsonUtility.FromJson<MapData>(asset.text));
        map=Resources.Load<Texture2D>("Art/Map/hex_map");
        titleBackground=Resources.Load<Texture2D>("Art/Menu/title_tapestry");
        assaultPeriodMarker=Resources.Load<Texture2D>("Art/Counters/Markers/Assault_Period");
        battleTurnMarker=Resources.Load<Texture2D>("Art/Counters/Markers/Battle_Turn");
        movementHex=CreateHexOverlay(128,148);
        foreach(var terrain in new[]{"clear","ridge","marsh","stream","woods","road"})
            terrainSwatches[terrain]=Resources.Load<Texture2D>("Art/Terrain/"+terrain);
    }
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if(showOrderResults)
            {
                if(game==null || !game.OptionsPending())showOrderResults=false;
                return;
            }
            if(chart!=""){chart="";return;}
            if(game==null){showMenu=true;menuPage="main";}
            else if(!showMenu){showMenu=true;menuPage="main";}
            else if(menuPage!="main")menuPage="main";
            else showMenu=false;
        }
        UpdateStackSpread();
        if(game==null||showMenu||chart!=""||showOrderResults||!Application.isFocused)return;
        var viewDirection=new Vector2(
            (Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),
            (Input.GetKey(KeyCode.S)?1:0)-(Input.GetKey(KeyCode.W)?1:0));
        if(viewDirection.sqrMagnitude==0)return;
        fullMapMode=false;
        pan-=viewDirection.normalized*Mathf.Max(360f,Screen.height*.65f)*Time.unscaledDeltaTime;
    }
    private void UpdateStackSpread()
    {
        if(game==null || !showUnits){stackSpread.Clear();return;}
        string opening=!showMenu && chart=="" && !showOrderResults && LeaderStackAt(hoveredHex)?hoveredHex:"";
        if(opening!="" && !stackSpread.ContainsKey(opening))stackSpread[opening]=0f;
        foreach(var hex in stackSpread.Keys.ToArray())
        {
            stackSpread[hex]=Mathf.MoveTowards(stackSpread[hex],hex==opening?1f:0f,
                Time.unscaledDeltaTime/StackSpreadSeconds);
            if(hex!=opening && stackSpread[hex]<=0f)stackSpread.Remove(hex);
        }
    }
    private float SpreadFor(string hex)
    {
        float spread;
        return stackSpread.TryGetValue(hex,out spread)?spread:0f;
    }
    private static float PanelWidth() { return Mathf.Clamp(Screen.width*.24f,500f,900f); }
    private static float PanelUiScale() { return Mathf.Clamp(Screen.height/900f,1.2f,1.8f); }
    private Side PlayerSide() { return game==null?Side.Norman:game.state.playerSide; }
    private bool SaxonView() { return PlayerSide()==Side.Saxon; }
    private bool PlayerMovePhase()
    {
        return game!=null && ((PlayerSide()==Side.Norman && game.state.phase==Phase.NormanMove) ||
            (PlayerSide()==Side.Saxon && game.state.phase==Phase.SaxonMove));
    }
    private bool PlayerReactionPhase()
    {
        return game!=null && ((PlayerSide()==Side.Norman && game.state.phase==Phase.NormanReaction) ||
            (PlayerSide()==Side.Saxon && game.state.phase==Phase.SaxonReaction));
    }
    private bool PlayerFirePhase()
    {
        if(game==null)return false;
        return PlayerSide()==Side.Norman?
            game.state.phase==Phase.NormanFire||game.state.phase==Phase.NormanDefenseFire:
            game.state.phase==Phase.SaxonFire||game.state.phase==Phase.SaxonDefenseFire;
    }
    private bool PlayerMeleePhase()
    {
        return game!=null && ((PlayerSide()==Side.Norman && game.state.phase==Phase.NormanMelee) ||
            (PlayerSide()==Side.Saxon && game.state.phase==Phase.SaxonMelee));
    }
    private Vector2 OrientedBoardPoint(float x,float y)
    {
        return BoardViewMath.OrientBattlefield(new Vector2(x,y),board.data.width,SaxonView());
    }
    private Vector2 MapPoint(float x,float y)
    {
        var point=OrientedBoardPoint(x,y);
        return new Vector2(pan.x+point.x*scale,pan.y+point.y*scale);
    }
    private void OnGUI()
    {
        if(board==null||map==null){GUI.Label(new Rect(20,20,700,40),"Hastings assets are missing. Run Tools/generate_assets.py.");return;}
        int body=Mathf.Clamp(Mathf.RoundToInt(Screen.height/65f),17,23);
        GUI.skin.label.fontSize=body;
        GUI.skin.button.fontSize=body;
        GUI.skin.button.padding=new RectOffset(10,10,8,8);
        GUI.skin.toggle.fontSize=body;
        GUI.skin.textField.fontSize=body;
        if(panelTitle==null)
        {
            small=new GUIStyle(GUI.skin.label){fontSize=body-3,wordWrap=true};
            hexNumber=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,
                fontStyle=FontStyle.Normal,wordWrap=false,clipping=TextClipping.Clip,
                padding=new RectOffset(0,0,0,0)};
            hexNumber.normal.textColor=new Color(.19f,.20f,.13f,.86f);
            hexNumberShadow=new GUIStyle(hexNumber);
            hexNumberShadow.normal.textColor=new Color(.98f,.96f,.78f,.60f);
            menuTitle=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(Mathf.RoundToInt(Screen.height*.055f),42,76),
                fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            menuSubtitle=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(Mathf.RoundToInt(Screen.height*.024f),21,34),
                alignment=TextAnchor.MiddleCenter};
            menuTitle.normal.textColor=new Color(.25f,.12f,.08f);
            menuSubtitle.normal.textColor=new Color(.35f,.21f,.13f);
            menuDescription=new GUIStyle(GUI.skin.label){fontSize=20,wordWrap=true,
                alignment=TextAnchor.UpperLeft};
            menuDescription.normal.textColor=new Color(.30f,.20f,.14f);
            menuButton=new GUIStyle(GUI.skin.button){fontSize=30,
                fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,
                padding=new RectOffset(18,18,10,10)};
            menuButton.normal.background=SolidTexture(new Color(.82f,.73f,.59f));
            menuButton.hover.background=SolidTexture(new Color(.91f,.81f,.64f));
            menuButton.active.background=SolidTexture(new Color(.74f,.63f,.49f));
            menuButton.normal.textColor=new Color(.21f,.15f,.11f);
            menuButton.hover.textColor=menuButton.normal.textColor;
            menuButton.active.textColor=menuButton.normal.textColor;
            menuPrimary=ButtonStyle(30,new Color(.60f,.21f,.16f),new Color(.73f,.27f,.20f),
                new Color(.99f,.96f,.88f));
            menuPrimary.fontStyle=FontStyle.Bold;
            menuTextField=new GUIStyle(GUI.skin.textField){fontSize=menuButton.fontSize,
                alignment=TextAnchor.MiddleCenter};
            panelTitle=LabelStyle(27,true,new Color(.18f,.13f,.10f));
            panelStatus=LabelStyle(17,false,new Color(.38f,.30f,.23f));
            panelSection=LabelStyle(13,true,new Color(.56f,.19f,.14f));
            panelBody=LabelStyle(16,false,new Color(.20f,.16f,.12f));
            panelMuted=LabelStyle(14,false,new Color(.36f,.30f,.24f));
            panelValue=LabelStyle(22,true,new Color(.29f,.18f,.13f));
            panelCard=new GUIStyle(GUI.skin.box){padding=new RectOffset(15,15,13,15),
                margin=new RectOffset(0,0,0,12)};
            panelCard.normal.background=SolidTexture(new Color(.98f,.965f,.91f));
            panelButton=ButtonStyle(15,new Color(.82f,.75f,.62f),new Color(.91f,.82f,.67f),
                new Color(.19f,.14f,.11f));
            panelNavButton=new GUIStyle(panelButton){margin=new RectOffset(0,0,0,0)};
            panelPrimary=ButtonStyle(18,new Color(.60f,.21f,.16f),new Color(.73f,.27f,.20f),
                new Color(.99f,.97f,.90f));
            panelPrimary.fontStyle=FontStyle.Bold;
            panelLink=ButtonStyle(14,new Color(.92f,.88f,.78f),new Color(.83f,.76f,.62f),
                new Color(.27f,.19f,.14f));
            fireModeButton=ButtonStyle(14,new Color(.82f,.75f,.62f),new Color(.91f,.82f,.67f),
                new Color(.24f,.16f,.11f));
            fireModeSelected=ButtonStyle(14,new Color(.55f,.23f,.17f),new Color(.63f,.27f,.20f),
                new Color(.99f,.96f,.88f));
            fireModeSelected.fontStyle=FontStyle.Bold;
            panelBretonButton=ButtonStyle(15,new Color(.72f,.81f,.64f),new Color(.80f,.88f,.71f),
                new Color(.15f,.25f,.13f));
            panelNormanButton=ButtonStyle(15,new Color(.85f,.70f,.63f),new Color(.91f,.77f,.69f),
                new Color(.31f,.13f,.10f));
            panelFlemishButton=ButtonStyle(15,new Color(.68f,.79f,.86f),new Color(.76f,.86f,.92f),
                new Color(.12f,.22f,.30f));
            strategyHeading=LabelStyle(11,true,new Color(.56f,.19f,.14f));
            strategyScale=LabelStyle(9,false,new Color(.40f,.34f,.27f));
            strategyScale.alignment=TextAnchor.MiddleCenter;
            strategyScale.wordWrap=false;
            strategyMarker=LabelStyle(11,true,new Color(.99f,.96f,.88f));
            strategyMarker.alignment=TextAnchor.MiddleCenter;
            strategyMarker.padding=new RectOffset(0,0,0,0);
            strategyLegend=LabelStyle(10,false,new Color(.36f,.29f,.22f));
            strategyLegend.alignment=TextAnchor.MiddleLeft;
            strategyLegend.wordWrap=false;
            strategyToggle=ButtonStyle(13,new Color(.86f,.80f,.68f),new Color(.93f,.86f,.71f),
                new Color(.40f,.17f,.12f));
            strategyToggle.fontStyle=FontStyle.Bold;
            strategyToggle.alignment=TextAnchor.MiddleLeft;
            strategyBand=LabelStyle(14,true,new Color(.35f,.27f,.21f));
            strategyBand.alignment=TextAnchor.LowerCenter;
            strategyBand.wordWrap=true;
            strategyEffectNote=LabelStyle(13,false,new Color(.36f,.29f,.22f));
            strategyEffectNote.alignment=TextAnchor.UpperLeft;
            strategyEffectNote.wordWrap=true;
            strategyEffectNote.richText=true;
            missileMapResult=LabelStyle(18,true,new Color(.99f,.96f,.88f));
            missileMapResult.alignment=TextAnchor.MiddleCenter;
            missileMapResult.wordWrap=false;
            missileMapDetail=LabelStyle(12,true,new Color(.32f,.20f,.14f));
            missileMapDetail.alignment=TextAnchor.MiddleCenter;
            missileMapDetail.wordWrap=false;
            statusMarker=LabelStyle(15,true,Color.white);
            statusMarker.alignment=TextAnchor.MiddleCenter;
            statusMarker.wordWrap=false;
            statusMarker.padding=new RectOffset(0,0,0,0);
            controlHeading=LabelStyle(12,true,new Color(.56f,.19f,.14f));
            controlHeading.margin=new RectOffset(0,0,8,3);
            controlBadge=LabelStyle(12,true,new Color(.99f,.96f,.88f));
            controlBadge.alignment=TextAnchor.MiddleCenter;
            controlBadge.normal.background=SolidTexture(new Color(.47f,.27f,.20f));
            controlBadge.padding=new RectOffset(6,6,3,3);
            fireCompleteBadge=new GUIStyle(controlBadge);
            fireCompleteBadge.normal.background=SolidTexture(new Color(.25f,.43f,.23f));
            controlAction=LabelStyle(13,false,new Color(.28f,.21f,.16f));
            controlAction.alignment=TextAnchor.MiddleLeft;
            controlRow=new GUIStyle(GUI.skin.box){padding=new RectOffset(5,7,4,4),
                margin=new RectOffset(0,0,0,3)};
            controlRow.normal.background=SolidTexture(new Color(.93f,.89f,.80f));
            orderTitle=LabelStyle(28,true,new Color(.99f,.96f,.88f));
            orderSubtitle=LabelStyle(16,false,new Color(.40f,.31f,.23f));
            orderSection=LabelStyle(17,true,new Color(.56f,.19f,.14f));
            orderCard=new GUIStyle(GUI.skin.box){padding=new RectOffset(18,18,14,16),
                margin=new RectOffset(0,0,0,10)};
            orderCard.normal.background=SolidTexture(new Color(.98f,.965f,.91f));
            orderCardTitle=LabelStyle(20,true,new Color(.27f,.16f,.11f));
            orderRoll=LabelStyle(13,true,new Color(.99f,.96f,.88f));
            orderRoll.alignment=TextAnchor.MiddleCenter;
            orderRoll.normal.background=SolidTexture(new Color(.55f,.23f,.17f));
            orderRoll.padding=new RectOffset(10,10,5,5);
            orderType=LabelStyle(11,true,new Color(.56f,.19f,.14f));
            orderName=LabelStyle(17,true,new Color(.25f,.15f,.11f));
            orderText=LabelStyle(13,false,new Color(.34f,.27f,.21f));
            orderChoice=ButtonStyle(13,new Color(.78f,.69f,.54f),new Color(.88f,.78f,.60f),
                new Color(.25f,.15f,.11f));
            orderChoice.fontStyle=FontStyle.Bold;
            orderEffect=new GUIStyle(GUI.skin.box){padding=new RectOffset(11,11,9,9)};
            orderEffect.normal.background=SolidTexture(new Color(.90f,.85f,.73f));
            orderEffectHeading=LabelStyle(12,true,new Color(.51f,.20f,.15f));
            orderEffectHeading.alignment=TextAnchor.MiddleLeft;
            orderEffectValue=LabelStyle(12,true,new Color(.27f,.18f,.13f));
            orderEffectValue.alignment=TextAnchor.MiddleCenter;
            orderEffectValue.normal.background=SolidTexture(new Color(.82f,.75f,.62f));
            orderEffectValue.padding=new RectOffset(8,8,4,4);
            orderEffectDetail=LabelStyle(12,false,new Color(.35f,.28f,.21f));
            orderEffectWarning=new GUIStyle(orderEffectDetail);
            orderEffectWarning.fontStyle=FontStyle.Bold;
            orderEffectWarning.normal.textColor=new Color(.55f,.19f,.14f);
            chartTitle=LabelStyle(28,true,new Color(.99f,.96f,.88f));
            chartSection=LabelStyle(19,true,new Color(.54f,.20f,.15f));
            chartHeader=LabelStyle(15,true,new Color(.96f,.94f,.87f));
            chartHeader.alignment=TextAnchor.MiddleCenter;
            chartRowHeader=LabelStyle(15,true,new Color(.27f,.19f,.14f));
            chartRowHeader.alignment=TextAnchor.MiddleCenter;
            chartCell=LabelStyle(16,false,new Color(.23f,.17f,.13f));
            chartCell.alignment=TextAnchor.MiddleCenter;
            chartMuted=new GUIStyle(chartCell);
            chartMuted.normal.textColor=new Color(.59f,.53f,.43f);
            chartNote=LabelStyle(16,false,new Color(.32f,.25f,.19f));
            chartTab=new GUIStyle(panelButton);
            chartTabSelected=new GUIStyle(panelPrimary);
            chartClose=new GUIStyle(panelLink);
            trackMarkerText=LabelStyle(24,true,new Color(.99f,.96f,.88f));
            trackMarkerText.alignment=TextAnchor.MiddleCenter;
            trackMarkerText.padding=new RectOffset(0,0,0,0);
            trackMarkerTextShadow=new GUIStyle(trackMarkerText);
            trackMarkerTextShadow.normal.textColor=new Color(.10f,.06f,.04f,.70f);
        }
        panelScale=PanelUiScale();
        UpdatePanelStyles();
        float menuScale=Mathf.Clamp(Screen.height/900f,1.1f,1.8f);
        menuTitle.fontSize=Mathf.RoundToInt(52*menuScale);
        menuSubtitle.fontSize=Mathf.RoundToInt(22*menuScale);
        menuDescription.fontSize=Mathf.RoundToInt(17*menuScale);
        menuButton.fontSize=Mathf.RoundToInt(24*menuScale);
        menuPrimary.fontSize=menuButton.fontSize;
        menuTextField.fontSize=menuButton.fontSize;
        if(game==null)
        {
            showMenu=true;
            DrawTitleBackground();
            DrawMenu();
            return;
        }
        float strategyTrackHeight=showStrategyTrack?
            Mathf.Clamp(Screen.height*.26f,330f,400f):
            Mathf.Clamp(Screen.height*.045f,54f,68f);
        Rect mapRect=new Rect(0,0,Mathf.Max(100,Screen.width-PanelWidth()),
            Screen.height-strategyTrackHeight);
        var strategyTrackRect=new Rect(0,mapRect.yMax,mapRect.width,strategyTrackHeight);
        ResizeMapView(mapRect);
        HandleInput(mapRect);
        pan=BoardViewMath.ClampPan(pan,scale,mapRect.width,mapRect.height,
            board.data.width,board.data.height);
        DrawMap(mapRect);
        DrawStrategyEffectsTrack(strategyTrackRect);
        if(showOrderResults)GUI.enabled=false;
        DrawPanel(new Rect(mapRect.xMax,0,PanelWidth(),Screen.height));
        GUI.enabled=true;
        if(showMenu)DrawMenu();
        if(chart!="")DrawChart();
        if(showOrderResults && !showMenu && chart=="")DrawOrderResults();
    }
    private void ResizeMapView(Rect region)
    {
        if(Mathf.Approximately(lastMapWidth,region.width) &&
           Mathf.Approximately(lastMapHeight,region.height))return;
        if(fullMapMode)
        {
            scale=BoardViewMath.FitWhole(region.width,region.height,
                board.data.width,board.data.height);
            pan=new Vector2((region.width-board.data.width*scale)/2f,
                (region.height-board.data.height*scale)/2f);
        }
        else BoardViewMath.Resize(ref scale,ref pan,lastMapWidth,lastMapHeight,
            region.width,region.height,board.data.width);
        lastMapWidth=region.width;lastMapHeight=region.height;
    }
    private static Texture2D SolidTexture(Color color)
    {
        var result=new Texture2D(1,1,TextureFormat.RGBA32,false);
        result.SetPixel(0,0,color);result.Apply();return result;
    }
    private static Texture2D CreateHexOverlay(int width,int height)
    {
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
        {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,
            hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color[width*height];
        const int samples=4;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            float alpha=0;
            for(int sy=0;sy<samples;sy++)for(int sx=0;sx<samples;sx++)
            {
                float nx=Mathf.Abs(((x+(sx+.5f)/samples)/width-.5f)*2f);
                float ny=Mathf.Abs(((y+(sy+.5f)/samples)/height-.5f)*2f);
                bool outer=ny<=1f && nx<=Mathf.Min(1f,2f*(1f-ny));
                float innerScale=.90f;
                float ix=nx/innerScale,iy=ny/innerScale;
                bool inner=iy<=1f && ix<=Mathf.Min(1f,2f*(1f-iy));
                if(outer)alpha+=inner?.19f:.88f;
            }
            pixels[y*width+x]=new Color(1,1,1,alpha/(samples*samples));
        }
        texture.SetPixels(pixels);texture.Apply();return texture;
    }
    private static GUIStyle LabelStyle(int size,bool bold,Color color)
    {
        var style=new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,
            wordWrap=true};
        style.normal.textColor=color;
        return style;
    }
    private static GUIStyle ButtonStyle(int size,Color background,Color hover,Color textColor)
    {
        var style=new GUIStyle(GUI.skin.button){fontSize=size,alignment=TextAnchor.MiddleCenter,
            padding=new RectOffset(8,8,5,5)};
        style.normal.background=SolidTexture(background);
        style.hover.background=SolidTexture(hover);
        style.active.background=SolidTexture(background*.85f);
        style.normal.textColor=textColor;
        style.hover.textColor=textColor;
        style.active.textColor=textColor;
        return style;
    }
    private void UpdatePanelStyles()
    {
        float p=panelScale;
        panelTitle.fontSize=Mathf.RoundToInt(27*p);
        panelStatus.fontSize=Mathf.RoundToInt(17*p);
        panelSection.fontSize=Mathf.RoundToInt(13*p);
        panelBody.fontSize=Mathf.RoundToInt(16*p);
        panelMuted.fontSize=Mathf.RoundToInt(14*p);
        panelValue.fontSize=Mathf.RoundToInt(22*p);
        panelButton.fontSize=Mathf.RoundToInt(15*p);
        panelNavButton.fontSize=panelButton.fontSize;
        panelPrimary.fontSize=Mathf.RoundToInt(18*p);
        panelLink.fontSize=Mathf.RoundToInt(14*p);
        fireModeButton.fontSize=Mathf.RoundToInt(14*p);
        fireModeSelected.fontSize=fireModeButton.fontSize;
        panelBretonButton.fontSize=panelButton.fontSize;
        panelNormanButton.fontSize=panelButton.fontSize;
        panelFlemishButton.fontSize=panelButton.fontSize;
        strategyHeading.fontSize=Mathf.RoundToInt(11*p);
        strategyScale.fontSize=Mathf.Max(9,Mathf.RoundToInt(9*p));
        strategyMarker.fontSize=Mathf.RoundToInt(11*p);
        strategyLegend.fontSize=Mathf.RoundToInt(10*p);
        strategyToggle.fontSize=Mathf.RoundToInt(13*p);
        strategyBand.fontSize=Mathf.RoundToInt(14*p);
        strategyEffectNote.fontSize=Mathf.RoundToInt(13*p);
        controlHeading.fontSize=Mathf.RoundToInt(12*p);
        controlBadge.fontSize=Mathf.RoundToInt(12*p);
        fireCompleteBadge.fontSize=controlBadge.fontSize;
        controlAction.fontSize=Mathf.RoundToInt(13*p);
        int side=Mathf.RoundToInt(15*p),top=Mathf.RoundToInt(13*p);
        panelCard.padding=new RectOffset(side,side,top,Mathf.RoundToInt(15*p));
        panelCard.margin=new RectOffset(0,0,0,Mathf.RoundToInt(12*p));
        controlHeading.margin=new RectOffset(0,0,Mathf.RoundToInt(8*p),Mathf.RoundToInt(3*p));
        controlBadge.padding=new RectOffset(Mathf.RoundToInt(6*p),Mathf.RoundToInt(6*p),
            Mathf.RoundToInt(3*p),Mathf.RoundToInt(3*p));
        controlRow.padding=new RectOffset(Mathf.RoundToInt(5*p),Mathf.RoundToInt(7*p),
            Mathf.RoundToInt(4*p),Mathf.RoundToInt(4*p));
        controlRow.margin=new RectOffset(0,0,0,Mathf.RoundToInt(3*p));
        GUI.skin.toggle.fontSize=panelBody.fontSize;
    }
    private static void Fill(Rect rect,Color color)
    {
        var old=GUI.color;
        GUI.color=color;
        GUI.DrawTexture(rect,Texture2D.whiteTexture);
        GUI.color=old;
    }
    private void DrawTitleBackground()
    {
        if(titleBackground!=null)
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),titleBackground,ScaleMode.ScaleAndCrop);
        else
        {GUI.color=new Color(.75f,.66f,.50f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);}
        GUI.color=new Color(0,0,0,.12f);
        GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
        GUI.color=Color.white;
    }
    private void HandleInput(Rect region)
    {
        Event e=Event.current;
        if(e.type==EventType.KeyDown)
        {
            if(showUnits && !showMenu && chart=="" && !showOrderResults &&
                    (e.keyCode==KeyCode.Q||e.keyCode==KeyCode.E) && selected.Count==1)
            {
                var unit=SelectedUnits().FirstOrDefault();
                if(unit!=null)game.Face(unit,unit.facing+(e.keyCode==KeyCode.E?1:-1));e.Use();
            }
        }
        if(showMenu||chart!=""||showOrderResults||!region.Contains(e.mousePosition))return;
        if(e.type==EventType.ScrollWheel)
        {
            fullMapMode=false;
            float old=scale;scale=Mathf.Clamp(scale*(e.delta.y>0?.88f:1.12f),.12f,4f);
            pan=e.mousePosition-(e.mousePosition-pan)*(scale/old);e.Use();
        }
        else if(e.type==EventType.MouseDrag && (e.button==1||e.button==2))
        {fullMapMode=false;pan+=e.delta;e.Use();}
        else if(e.type==EventType.MouseDown && e.button==0 && game!=null && showUnits)
        {
            string hex=HexAtPointer(e.mousePosition);
            if(hex!=null)ClickHex(hex,e.shift,e.alt||PointerOnSplayedLeader(hex,e.mousePosition));
            e.Use();
        }
    }
    private Rect CounterRect(UnitState unit,float spread)
    {
        var h=board.Hex(unit.hex);
        return CounterLayout.RectFor(MapPoint(h.x,h.y),
            scale,UnitTypes.Get(unit).leader,spread);
    }
    private bool LeaderStackAt(string hex)
    {
        if(game==null || string.IsNullOrEmpty(hex))return false;
        var stack=game.state.units.Where(u=>u.hex==hex && u.status!=Status.Eliminated);
        return stack.Any(u=>UnitTypes.Get(u).leader) &&
            stack.Any(u=>!UnitTypes.Get(u).leader);
    }
    private string HexAtPointer(Vector2 point)
    {
        // Keep the stack open while the pointer moves onto a spread counter.
        if(showUnits && LeaderStackAt(hoveredHex) && game.state.units.Any(u=>
            u.hex==hoveredHex && u.status!=Status.Eliminated &&
            CounterRect(u,1f).Contains(point)))return hoveredHex;
        var boardPoint=BoardViewMath.OrientBattlefield(
            new Vector2((point.x-pan.x)/scale,(point.y-pan.y)/scale),
            board.data.width,SaxonView());
        return board.Nearest(boardPoint.x,boardPoint.y);
    }
    private bool PointerOnSplayedLeader(string hex,Vector2 point)
    {
        return LeaderStackAt(hex) && game.state.units.Any(u=>u.hex==hex &&
            u.status!=Status.Eliminated && UnitTypes.Get(u).leader &&
            CounterRect(u,SpreadFor(hex)).Contains(point));
    }
    private void ClickHex(string hex,bool add,bool preferLeader)
    {
        if(game==null)return;
        var player=PlayerSide();var opponent=GameEngine.Opposite(player);
        var friendly=game.UnitAt(hex,player);
        var leader=game.UnitAt(hex,player,true);
        var enemy=game.UnitAt(hex,opponent);
        var enemyLeader=game.UnitAt(hex,opponent,true);
        var selection=SelectedUnits();
        if(!preferLeader && PlayerMovePhase() && selection.Count==1 && game.Move(selection[0],hex))return;
        if(!preferLeader && PlayerReactionPhase() && selection.Count==1 && game.Move(selection[0],hex,true))return;
        if(!preferLeader && game.state.phase==Phase.Reform && player==Side.Norman &&
           selection.Count==1 && game.ReformMove(selection[0],hex))return;
        if((enemy!=null||enemyLeader!=null) && selection.Count>0)
        {
            if(PlayerFirePhase())
            {
                var target=enemy??enemyLeader;
                if(game.Fire(selection,target,false))ShowMissileResult();
                else if(selection.All(shooter=>game.CanFire(shooter,target,true)))
                {
                    highTrajectoryTargetId=target.id;
                    notice="";
                }
                else notice="That unit is not a legal direct-fire target, or its missile supply is exhausted.";
                return;
            }
            if(PlayerMeleePhase() && enemy!=null)
            {
                if(add)
                {
                    if(selection.Any(attacker=>!attacker.engaged && !enemy.engaged &&
                        game.CanMelee(attacker,enemy)))
                    {if(!selectedTargets.Contains(enemy.id))selectedTargets.Add(enemy.id);notice="";}
                    else notice=MeleeFailure(selection,enemy);
                    return;
                }
                if(!game.Melee(selection,new List<UnitState>{enemy}))notice=MeleeFailure(selection,enemy);
                else ShowMeleeResult();
                return;
            }
        }
        if(friendly!=null || leader!=null)
        {
            highTrajectoryTargetId="";
            var unit=preferLeader?(leader??friendly):(friendly??leader);
            if(add)
            {if(!selected.Contains(unit.id))selected.Add(unit.id);}
            else{selected.Clear();selected.Add(unit.id);}
        }
        else if(!add){selected.Clear();highTrajectoryTargetId="";}
    }
    private List<UnitState> SelectedUnits()
    {
        if(game==null)return new List<UnitState>();
        return game.state.units.Where(u=>selected.Contains(u.id)&&u.status!=Status.Eliminated).ToList();
    }
    private void DrawMap(Rect region)
    {
        GUI.color=new Color(.11f,.14f,.15f);
        GUI.DrawTexture(region,Texture2D.whiteTexture);
        GUI.color=Color.white;
        GUI.BeginGroup(region);
        DrawBoardTexture();
        if(game!=null)
        {
            var e=Event.current;
            hoveredHex=!showMenu && chart=="" && !showOrderResults && region.Contains(e.mousePosition)?
                HexAtPointer(e.mousePosition):"";
            var unit=SelectedUnits().FirstOrDefault();
            if(showUnits && unit!=null && (PlayerMovePhase() || PlayerReactionPhase()))
            {
                bool reaction=PlayerReactionPhase();
                foreach(var move in game.LegalMoves(unit,reaction).Values)
                {
                    var h=board.Hex(move.destination);
                    var center=MapPoint(h.x,h.y);
                    float width=96*scale,height=111*scale;
                    GUI.color=move.charge?new Color(.82f,.31f,.15f,.96f):
                        new Color(.95f,.84f,.49f,.90f);
                    GUI.DrawTexture(new Rect(center.x-width/2,center.y-height/2,width,height),
                        movementHex,ScaleMode.StretchToFill);
                }
                GUI.color=Color.white;
            }
            DrawMissilePaths();
            DrawMeleePaths();
            DrawHexNumbers(region);
            DrawTrackMarkers();
            if(showUnits)
            {
                // Paint every leader after combat counters so it stays on top.
                foreach(var u in game.state.units.Where(u=>u.status!=Status.Eliminated && board.Has(u.hex))
                    .OrderBy(u=>UnitTypes.Get(u).leader?1:0)
                    .ThenBy(u=>u.hex==hoveredHex?1:0))
                {
                    var type=UnitTypes.Get(u);
                    var rect=CounterRect(u,SpreadFor(u.hex));
                    var texture=CounterTexture(u);
                    var old=GUI.matrix;
                    if(!type.leader)GUIUtility.RotateAroundPivot((u.facing-1)*60+
                        (SaxonView()?180:0),rect.center);
                    if(texture!=null)GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill);
                    if(selected.Contains(u.id))DrawSelectionOutline(rect);
                    GUI.matrix=old;
                    if(u.status==Status.Disrupted||u.status==Status.Routed)
                        DrawUnitStatusMarker(u.status,rect);
                }
                DrawFireDesignationHighlights();
                DrawMeleeDesignationHighlights();
            }
            DrawMissileImpact(region);
            DrawMeleeImpact(region);
        }
        GUI.EndGroup();
    }
    private void DrawBoardTexture()
    {
        var full=new Rect(pan.x,pan.y,board.data.width*scale,board.data.height*scale);
        if(!SaxonView())
        {
            GUI.DrawTexture(full,map,ScaleMode.StretchToFill);
            return;
        }
        float field=BoardViewMath.BattlefieldHeight;
        float strip=board.data.height-field;
        var battlefield=new Rect(pan.x,pan.y,board.data.width*scale,field*scale);
        // Reverse both texture axes instead of rotating the GUI matrix. Unity
        // clips GUI draw calls before applying that matrix, which made most of
        // a zoomed battlefield disappear whenever its rect crossed the view.
        GUI.DrawTextureWithTexCoords(battlefield,map,
            new Rect(1,1,-1,-field/board.data.height),true);
        GUI.DrawTextureWithTexCoords(new Rect(pan.x,pan.y+field*scale,
            board.data.width*scale,strip*scale),map,
            new Rect(0,0,1,strip/board.data.height),true);
    }
    private void DrawTrackMarkers()
    {
        var state=game.state;
        int displayedTurn=state.turn;
        // Reform and game-over states are entered just after the final turn is
        // advanced. Leave the marker on the turn that was actually completed.
        if(state.phase==Phase.Reform ||
           (state.phase==Phase.GameOver && state.period==2 && state.turn>8 &&
            state.result=="Saxon strategic victory"))displayedTurn--;
        displayedTurn=Mathf.Clamp(displayedTurn,1,11);
        DrawTrackMarker(assaultPeriodMarker,new Vector2(state.period==1?200:300,2292),
            state.period==1?"I":"II");
        DrawTrackMarker(battleTurnMarker,new Vector2(600+(displayedTurn-1)*100,2292),
            displayedTurn.ToString());
    }
    private void DrawTrackMarker(Texture2D texture,Vector2 mapCenter,string label)
    {
        if(texture==null)return;
        // Keep the marker value readable in a fitted map view. The original
        // two-line counter captions collapse into noise at normal game scale.
        float size=Mathf.Max(28f,60*scale);
        var rect=new Rect(pan.x+mapCenter.x*scale-size/2,
            pan.y+mapCenter.y*scale-size/2,size,size);
        float shadowOffset=Mathf.Clamp(3f*scale,1f,3f);
        GUI.color=new Color(0,0,0,.30f);
        GUI.DrawTexture(new Rect(rect.x+shadowOffset,rect.y+shadowOffset,rect.width,rect.height),
            Texture2D.whiteTexture);
        GUI.color=Color.white;
        GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill);
        float inset=Mathf.Clamp(size*.075f,2f,5f);
        var face=new Rect(rect.x+inset,rect.y+inset,rect.width-2*inset,rect.height-2*inset);
        GUI.color=new Color(.43f,.17f,.13f);
        GUI.DrawTexture(face,Texture2D.whiteTexture);
        GUI.color=Color.white;
        int fontSize=Mathf.Clamp(Mathf.RoundToInt(size*.56f),15,36);
        trackMarkerText.fontSize=fontSize;
        trackMarkerTextShadow.fontSize=fontSize;
        GUI.Label(new Rect(face.x+1,face.y+1,face.width,face.height),label,trackMarkerTextShadow);
        GUI.Label(face,label,trackMarkerText);
    }
    private void DrawSelectionOutline(Rect rect)
    {
        float line=Mathf.Clamp(1.5f*scale,1.5f,6f);
        GUI.color=new Color(1f,.83f,.22f);
        GUI.DrawTexture(new Rect(rect.x-line,rect.y-line,rect.width+2*line,line),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x-line,rect.yMax,rect.width+2*line,line),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x-line,rect.y,line,rect.height),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax,rect.y,line,rect.height),Texture2D.whiteTexture);
        GUI.color=Color.white;
    }
    private void DrawUnitStatusMarker(Status status,Rect counter)
    {
        float size=Mathf.Clamp(27f*scale,18f,34f);
        var marker=new Rect(counter.xMax-size*.68f,counter.y-size*.32f,size,size);
        Color fill=status==Status.Routed?new Color(.62f,.13f,.10f):new Color(.78f,.52f,.12f);
        Fill(new Rect(marker.x-2,marker.y-2,marker.width+4,marker.height+4),
            new Color(.20f,.12f,.08f,.96f));
        Fill(marker,fill);
        statusMarker.fontSize=Mathf.Clamp(Mathf.RoundToInt(size*.63f),12,22);
        statusMarker.normal.textColor=status==Status.Routed?Color.white:new Color(.18f,.11f,.06f);
        GUI.Label(marker,status==Status.Routed?"R":"D",statusMarker);
    }
    private void DrawFireDesignationHighlights()
    {
        if(!PlayerFirePhase())return;
        var shooters=SelectedUnits();
        var enemies=game.Living(GameEngine.Opposite(PlayerSide())).ToList();
        float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*5.5f);
        foreach(var unit in game.Living(PlayerSide()).Where(unit=>
            !UnitTypes.Get(unit).leader && !unit.fired &&
            enemies.Any(target=>game.CanFire(unit,target,false)||game.CanFire(unit,target,true))))
        {
            var rect=CounterRect(unit,SpreadFor(unit.hex));
            float inset=3f+2f*pulse;
            DrawRectOutline(new Rect(rect.x-inset,rect.y-inset,
                rect.width+2*inset,rect.height+2*inset),2f+1.5f*pulse,
                new Color(1f,.69f,.17f,.68f+.18f*pulse));
        }
        if(shooters.Count==0)return;
        foreach(var target in game.Living(GameEngine.Opposite(PlayerSide())).Where(target=>
            shooters.All(shooter=>game.CanFire(shooter,target,false))))
        {
            var hex=board.Hex(target.hex);
            var center=MapPoint(hex.x,hex.y);
            float size=Mathf.Max(48f,74*scale);
            var rect=new Rect(center.x-size/2f,center.y-size/2f,size,size);
            DrawRectOutline(rect,Mathf.Clamp(3*scale,2f,6f),new Color(1f,.67f,.12f,.92f));
        }
        var highTarget=PendingHighTrajectoryTarget();
        if(highTarget!=null)
        {
            var rect=CounterRect(highTarget,SpreadFor(highTarget.hex));
            float inset=5f+3f*pulse;
            DrawRectOutline(new Rect(rect.x-inset,rect.y-inset,
                rect.width+2*inset,rect.height+2*inset),3f+2f*pulse,
                new Color(.84f,.28f,.10f,.92f));
        }
    }
    private void DrawMeleeDesignationHighlights()
    {
        if(!PlayerMeleePhase())return;
        var attackers=SelectedUnits();
        float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*6f);
        if(attackers.Count==0)
        {
            foreach(var unit in game.Living(PlayerSide()).Where(unit=>
                !UnitTypes.Get(unit).leader && unit.status==Status.Ready && !unit.engaged &&
                game.Living(GameEngine.Opposite(PlayerSide())).Any(enemy=>
                    !enemy.engaged && game.CanMelee(unit,enemy))))
            {
                var rect=CounterRect(unit,SpreadFor(unit.hex));
                DrawRectOutline(new Rect(rect.x-4,rect.y-4,rect.width+8,rect.height+8),
                    2f+2f*pulse,new Color(.91f,.62f,.14f,.72f));
            }
            return;
        }
        foreach(var target in game.Living(GameEngine.Opposite(PlayerSide())).Where(target=>
            !target.engaged && attackers.All(attacker=>!attacker.engaged &&
                game.CanMelee(attacker,target))))
        {
            var rect=CounterRect(target,SpreadFor(target.hex));
            float inset=3f+3f*pulse;
            DrawRectOutline(new Rect(rect.x-inset,rect.y-inset,
                rect.width+2*inset,rect.height+2*inset),3f+2f*pulse,
                new Color(.76f,.16f,.10f,.92f));
            var badge=new Rect(rect.center.x-34f,rect.y-27f,68f,22f);
            Fill(badge,new Color(.48f,.10f,.07f,.94f));
            missileMapResult.fontSize=11;
            GUI.Label(badge,"MELEE",missileMapResult);
        }
        foreach(var target in game.state.units.Where(unit=>selectedTargets.Contains(unit.id) &&
            unit.status!=Status.Eliminated))
        {
            var rect=CounterRect(target,SpreadFor(target.hex));
            DrawRectOutline(new Rect(rect.x-8,rect.y-8,rect.width+16,rect.height+16),5f,
                new Color(1f,.75f,.17f,.98f));
        }
    }
    private void DrawMeleePaths()
    {
        if(meleeResult==null || Time.unscaledTime>meleeEffectUntil)return;
        var targets=meleeResult.defenderHexes.Where(board.Has).Select(id=>board.Hex(id)).
            Select(hex=>MapPoint(hex.x,hex.y)).ToArray();
        if(targets.Length==0)return;
        float elapsed=Time.unscaledTime-meleeEffectStarted;
        float fade=1f-Mathf.Clamp01((elapsed-3.2f)/1.1f);
        float travel=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.8f));
        foreach(var hexId in meleeResult.attackerHexes)
        {
            if(!board.Has(hexId))continue;
            var hex=board.Hex(hexId);var origin=MapPoint(hex.x,hex.y);
            var target=targets.OrderBy(point=>(point-origin).sqrMagnitude).First();
            DrawLine(origin,target,Mathf.Clamp(8f*scale,5f,12f),
                new Color(.35f,.06f,.04f,.72f*fade));
            DrawLine(origin,target,Mathf.Clamp(2.5f*scale,2f,5f),
                new Color(1f,.67f,.18f,.94f*fade));
            var marker=Vector2.Lerp(origin,target,travel);
            float size=Mathf.Clamp(15f*scale,10f,22f);
            Fill(new Rect(marker.x-size/2,marker.y-size/2,size,size),
                new Color(.92f,.22f,.11f,fade));
        }
    }
    private void DrawMeleeImpact(Rect region)
    {
        if(meleeResult==null || Time.unscaledTime>meleeEffectUntil)return;
        var targets=meleeResult.defenderHexes.Where(board.Has).Select(id=>board.Hex(id)).
            Select(hex=>MapPoint(hex.x,hex.y)).ToArray();
        if(targets.Length==0)return;
        float elapsed=Time.unscaledTime-meleeEffectStarted;
        float fade=1f-Mathf.Clamp01((elapsed-3.2f)/1.1f);
        float impact=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-.45f)/.25f));
        float pulse=.5f+.5f*Mathf.Sin(elapsed*11f);
        foreach(var target in targets)
        {
            float radius=Mathf.Max(32f,(42f+9f*pulse)*scale);
            DrawRectOutline(new Rect(target.x-radius,target.y-radius,radius*2,radius*2),
                Mathf.Clamp(4f*scale,3f,8f),new Color(.78f,.14f,.08f,.90f*fade));
            float blade=radius*.62f;
            DrawLine(target-new Vector2(blade,blade),target+new Vector2(blade,blade),
                Mathf.Clamp(4f*scale,3f,7f),new Color(1f,.78f,.24f,impact*fade));
            DrawLine(target+new Vector2(-blade,blade),target+new Vector2(blade,-blade),
                Mathf.Clamp(4f*scale,3f,7f),new Color(1f,.78f,.24f,impact*fade));
        }
        var center=new Vector2(targets.Average(point=>point.x),targets.Average(point=>point.y));
        const float width=260f,height=72f;
        float x=Mathf.Clamp(center.x-width/2f,8f,region.width-width-8f);
        float y=Mathf.Clamp(center.y-120f,8f,region.height-height-8f);
        var callout=new Rect(x,y,width,height);
        Fill(new Rect(callout.x-3,callout.y-3,callout.width+6,callout.height+6),
            new Color(.20f,.08f,.05f,.94f*impact*fade));
        Fill(new Rect(callout.x,callout.y,callout.width,41),new Color(.58f,.16f,.11f,.98f*impact*fade));
        Fill(new Rect(callout.x,callout.y+41,callout.width,31),new Color(.96f,.91f,.81f,.98f*impact*fade));
        var oldColor=GUI.color;GUI.color=new Color(1,1,1,impact*fade);
        missileMapResult.fontSize=18;missileMapDetail.fontSize=12;
        GUI.Label(new Rect(callout.x+6,callout.y+2,callout.width-12,37),
            MeleeOutcome(meleeResult),missileMapResult);
        GUI.Label(new Rect(callout.x+6,callout.y+43,callout.width-12,27),
            meleeResult.attack+" attack · "+meleeResult.defense+" defense · Roll "+meleeResult.roll,
            missileMapDetail);
        GUI.color=oldColor;
    }
    private void DrawMissilePaths()
    {
        if(missileResult==null || Time.unscaledTime>missileEffectUntil ||
           !board.Has(missileResult.targetHex))return;
        float elapsed=Time.unscaledTime-missileEffectStarted;
        float fade=1f-Mathf.Clamp01((elapsed-2.5f)/1.1f);
        var targetHex=board.Hex(missileResult.targetHex);
        var target=MapPoint(targetHex.x,targetHex.y);
        float travel=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.65f));
        foreach(var shooterHexId in missileResult.shooterHexes)
        {
            if(!board.Has(shooterHexId))continue;
            var shooterHex=board.Hex(shooterHexId);
            var origin=MapPoint(shooterHex.x,shooterHex.y);
            DrawLine(origin,target,Mathf.Clamp(2.5f*scale,2f,5f),
                new Color(1f,.82f,.24f,.48f*fade));
            var projectile=Vector2.Lerp(origin,target,travel);
            float size=Mathf.Clamp(10*scale,7f,16f);
            Fill(new Rect(projectile.x-size/2,projectile.y-size/2,size,size),
                new Color(1f,.95f,.62f,fade));
        }
    }
    private void DrawMissileImpact(Rect region)
    {
        if(missileResult==null || Time.unscaledTime>missileEffectUntil ||
           !board.Has(missileResult.targetHex))return;
        float elapsed=Time.unscaledTime-missileEffectStarted;
        float fade=1f-Mathf.Clamp01((elapsed-2.5f)/1.1f);
        var hex=board.Hex(missileResult.targetHex);
        var target=MapPoint(hex.x,hex.y);
        float pulse=.5f+.5f*Mathf.Sin(elapsed*10f);
        float targetSize=Mathf.Max(58f,(76+12*pulse)*scale);
        DrawRectOutline(new Rect(target.x-targetSize/2,target.y-targetSize/2,targetSize,targetSize),
            Mathf.Clamp(4*scale,3f,7f),new Color(1f,.77f,.18f,.88f*fade));

        float width=220f,height=70f;
        float x=Mathf.Clamp(target.x-width/2f,8f,region.width-width-8f);
        float y=Mathf.Clamp(target.y-targetSize/2f-height-12f,8f,region.height-height-8f);
        var callout=new Rect(x,y,width,height);
        Fill(new Rect(callout.x-3,callout.y-3,callout.width+6,callout.height+6),
            new Color(.24f,.13f,.09f,.92f*fade));
        Fill(new Rect(callout.x,callout.y,callout.width,40),new Color(.61f,.22f,.16f,.98f*fade));
        Fill(new Rect(callout.x,callout.y+40,callout.width,30),new Color(.96f,.91f,.81f,.98f*fade));
        missileMapResult.fontSize=18;
        missileMapDetail.fontSize=12;
        GUI.Label(new Rect(callout.x+5,callout.y+2,callout.width-10,36),
            MissileOutcome(missileResult),missileMapResult);
        string detail=missileResult.roll==0?"Odds below 1:4":
            missileResult.strength+" attack · "+missileResult.defense+" defense · Roll "+missileResult.roll;
        GUI.Label(new Rect(callout.x+5,callout.y+41,callout.width-10,27),detail,missileMapDetail);
    }
    private static void DrawLine(Vector2 from,Vector2 to,float width,Color color)
    {
        Vector2 delta=to-from;
        float length=delta.magnitude;
        if(length<=.1f)return;
        var old=GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,from);
        Fill(new Rect(from.x,from.y-width/2f,length,width),color);
        GUI.matrix=old;
    }
    private static void DrawRectOutline(Rect rect,float width,Color color)
    {
        Fill(new Rect(rect.x,rect.y,rect.width,width),color);
        Fill(new Rect(rect.x,rect.yMax-width,rect.width,width),color);
        Fill(new Rect(rect.x,rect.y,width,rect.height),color);
        Fill(new Rect(rect.xMax-width,rect.y,width,rect.height),color);
    }
    private static string MissileOutcome(MissileFireResult result)
    {
        if(result.targetStatusAfter==Status.Eliminated)return "ELIMINATED";
        if(!result.targetReducedBefore && result.targetReducedAfter)return "STEP LOSS";
        if(result.targetStatusAfter==Status.Routed)return "ROUTED";
        if(result.targetStatusAfter==Status.Disrupted)return "DISRUPTED";
        if(result.tableResult!=null && result.tableResult.Contains("M"))return "MORALE HOLDS";
        return "NO EFFECT";
    }
    private static string MissileOutcomeDetail(MissileFireResult result)
    {
        switch(MissileOutcome(result))
        {
            case "ELIMINATED":return "The target was removed from play.";
            case "STEP LOSS":return "The target counter was reduced.";
            case "ROUTED":return "The target routed and retreated.";
            case "DISRUPTED":return "The target is disrupted.";
            case "MORALE HOLDS":return "The target passed its morale check.";
            default:return "The fire caused no damage or morale effect.";
        }
    }
    private static bool ForceChanged(Status[] before,Status[] after,bool[] reducedBefore,
        bool[] reducedAfter)
    {
        return before.Where((status,index)=>after[index]!=status).Any() ||
            reducedBefore.Where((reduced,index)=>reducedAfter[index]!=reduced).Any();
    }
    private static string MeleeOutcome(MeleeCombatResult result)
    {
        bool attackersChanged=ForceChanged(result.attackerStatusBefore,result.attackerStatusAfter,
            result.attackerReducedBefore,result.attackerReducedAfter);
        bool defendersChanged=ForceChanged(result.defenderStatusBefore,result.defenderStatusAfter,
            result.defenderReducedBefore,result.defenderReducedAfter);
        if(attackersChanged && defendersChanged)return "BOTH SIDES HIT";
        if(defendersChanged)
        {
            if(result.defenderStatusAfter.Contains(Status.Eliminated))return "DEFENDER ELIMINATED";
            if(result.defenderStatusAfter.Contains(Status.Routed))return "DEFENDER ROUTED";
            if(result.defenderReducedBefore.Where((value,index)=>
                !value && result.defenderReducedAfter[index]).Any())return "DEFENDER STEP LOSS";
            return "DEFENDER DISRUPTED";
        }
        if(attackersChanged)return "ATTACKERS REPULSED";
        if(result.tableResult.Contains("M"))return "MORALE HOLDS";
        return "NO EFFECT";
    }
    private static string ForceSummary(Status[] before,Status[] after,bool[] reducedBefore,
        bool[] reducedAfter,string tablePart)
    {
        if(after.Contains(Status.Eliminated))return "eliminated";
        if(after.Contains(Status.Routed) && !before.Contains(Status.Routed))return "routed";
        if(reducedBefore.Where((value,index)=>!value && reducedAfter[index]).Any())return "step loss";
        if(after.Contains(Status.Disrupted) && !before.Contains(Status.Disrupted))return "disrupted";
        return tablePart.Contains("M")?"morale held":"held";
    }
    private static string MeleeOutcomeDetail(MeleeCombatResult result)
    {
        var parts=result.tableResult.Split('/');
        return "Attackers "+ForceSummary(result.attackerStatusBefore,result.attackerStatusAfter,
            result.attackerReducedBefore,result.attackerReducedAfter,parts[0])+"; defenders "+
            ForceSummary(result.defenderStatusBefore,result.defenderStatusAfter,
                result.defenderReducedBefore,result.defenderReducedAfter,parts[1])+".";
    }
    private void DrawHexNumbers(Rect region)
    {
        int fontSize=Mathf.Max(1,Mathf.RoundToInt(16*scale));
        hexNumber.fontSize=fontSize;
        hexNumberShadow.fontSize=fontSize;
        // The printed board places each four-digit coordinate vertically at the right of its hex.
        // Leave enough unrotated width for all four digits before rotating the label.
        float width=54.4f*scale,height=16*scale;
        float margin=Mathf.Max(60,50*scale);
        foreach(var h in board.data.hexes)
        {
            var center=MapPoint(h.x,h.y);
            float x=center.x,y=center.y;
            if(x<-margin||x>region.width+margin||y<-margin||y>region.height+margin)continue;
            var pivot=new Vector2(x+(SaxonView()?-42:42)*scale,y);
            var label=new Rect(pivot.x-width*.5f,pivot.y-height*.5f,width,height);
            var old=GUI.matrix;
            GUIUtility.RotateAroundPivot(SaxonView()?-90:90,pivot);
            GUI.Label(new Rect(label.x+scale,label.y+scale,label.width,label.height),h.id,hexNumberShadow);
            GUI.Label(label,h.id,hexNumber);
            GUI.matrix=old;
        }
    }
    private Texture2D CounterTexture(UnitState unit)
    {
        var type=UnitTypes.Get(unit);
        string side=unit.side==Side.Norman?"Normans":"Saxons";
        string suffix=unit.reduced || (type.leader && unit.leaderCondition>0)?"_Ineffective":"";
        string path="Art/Counters/"+side+"/"+type.art+suffix;
        Texture2D tex;
        if(!counters.TryGetValue(path,out tex))
        {
            tex=Resources.Load<Texture2D>(path);
            if(tex==null)tex=Resources.Load<Texture2D>("Art/Counters/"+side+"/"+type.art);
            counters[path]=tex;
        }
        return tex;
    }
    private void DrawPanel(Rect region)
    {
        float p=panelScale;
        Fill(region,new Color(.90f,.86f,.76f));
        Fill(new Rect(region.x,region.y,5*p,region.height),new Color(.59f,.22f,.17f));
        GUILayout.BeginArea(new Rect(region.x+20*p,16*p,region.width-40*p,region.height-32*p));
        GUILayout.Label("HASTINGS 1066",panelTitle,GUILayout.Height(36*p));
        var s=game.state;
        bool optionsPending=game.OptionsPending();
        var unitLabels=UnitDisplayNames.Build(s);
        GUILayout.Label(s.phase==Phase.GameOver?s.result:
            $"ASSAULT {s.period}   ·   TURN {s.turn}",panelStatus,GUILayout.Height(28*p));
        GUILayout.Space(8*p);
        float controlGap=6*p;
        float controlWidth=(region.width-40*p-controlGap)/2f;
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Menu",panelNavButton,GUILayout.Width(controlWidth),GUILayout.Height(42*p)))
        {showMenu=true;menuPage="main";chart="";}
        GUILayout.Space(controlGap);
        if(GUILayout.Button(showUnits?"Hide units":"Show units",panelNavButton,
            GUILayout.Width(controlWidth),GUILayout.Height(42*p)))
        {showUnits=!showUnits;selected.Clear();selectedTargets.Clear();}
        GUILayout.EndHorizontal();
        GUILayout.Space(5*p);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Focus battle",panelNavButton,GUILayout.Width(controlWidth),GUILayout.Height(42*p)))
        {fullMapMode=false;lastMapWidth=0;scale=0;}
        GUILayout.Space(controlGap);
        if(GUILayout.Button("Fit map",panelNavButton,GUILayout.Width(controlWidth),GUILayout.Height(42*p)))
        {fullMapMode=true;lastMapWidth=0;}
        GUILayout.EndHorizontal();
        GUILayout.Space(8*p);
        GUILayout.Label(HoverDescription(),panelStatus,GUILayout.Height(28*p));
        GUILayout.Space(7*p);
        panelScroll=GUILayout.BeginScrollView(panelScroll);
        GUILayout.BeginVertical(panelCard);
        GUILayout.Label("YOUR NEXT ACTION",panelSection);
        bool waitingForOrders=optionsPending && s.phase==Phase.NormanFire;
        GUILayout.Label(waitingForOrders?"Complete battle orders":PhaseLabel(s.phase),panelValue);
        GUILayout.Label(waitingForOrders?
            "Choose each optional order below. Missile fire begins after every section has an order.":
            PhasePrompt(s.phase),panelMuted);
        if(s.phase!=Phase.Orders && s.orderResults!=null && s.orderResults.Count>0)
        {
            GUILayout.Space(5*p);
            if(GUILayout.Button("Review battle orders",panelLink,GUILayout.Height(38*p)))
            {
                orderReviewMode=!game.OptionsPending();orderReviewTab=0;
                showOrderResults=true;orderScroll=Vector2.zero;
            }
        }
        if(s.phase==Phase.Orders)
        {
            GUILayout.Space(5*p);
            var strategyGroups=PlayerSide()==Side.Norman?
                new[]{"Breton","Norman","Franco-Flemish"}:new[]{"Left","Center","Right"};
            foreach(var id in strategyGroups)
            {
                var group=s.groups.First(g=>g.id==id);
                var content=new GUIContent(id+"  ·  "+group.strategy,StrategyOverview(group.strategy));
                if(GUILayout.Button(content,FactionButton(id),GUILayout.Height(40*p)))
                    game.SetStrategy(id,(Strategy)(((int)group.strategy+1)%4));
            }
            GUILayout.Space(4*p);
            GUILayout.Label(string.IsNullOrEmpty(GUI.tooltip)?
                "Click a contingent to cycle its strategy. Hover for a short overview.":GUI.tooltip,
                panelMuted,GUILayout.MinHeight(40*p));
        }
        if(optionsPending)
        {
            GUILayout.Space(6*p);
            GUILayout.Label("OPTIONAL ORDERS",panelSection);
            foreach(var g in s.groups.Where(g=>g.footOptional||g.knightOptional))
            {
                if(g.footOptional)
                {
                    GUILayout.Label(g.id+" foot",panelMuted);GUILayout.BeginHorizontal();
                    if(GUILayout.Button("Wall",panelButton))ChooseOptionalOrder(g.id,false,Order.ShieldWall);
                    if(PlayerSide()==Side.Saxon)
                    {
                        if(GUILayout.Button("Attack",panelButton))
                            ChooseOptionalOrder(g.id,false,Order.AttackPursue);
                    }
                    else if(GUILayout.Button("Fire",panelButton))
                        ChooseOptionalOrder(g.id,false,Order.FireInPlace);
                    if(GUILayout.Button("Advance",panelButton))ChooseOptionalOrder(g.id,false,Order.Advance);
                    GUILayout.EndHorizontal();
                }
                if(g.knightOptional)
                {
                    GUILayout.Label(g.id+" knights",panelMuted);GUILayout.BeginHorizontal();
                    if(GUILayout.Button("Hold",panelButton))ChooseOptionalOrder(g.id,true,Order.Hold);
                    if(GUILayout.Button("Advance",panelButton))ChooseOptionalOrder(g.id,true,Order.Advance);
                    if(GUILayout.Button("Charge",panelButton))ChooseOptionalOrder(g.id,true,Order.Charge);
                    GUILayout.EndHorizontal();
                }
            }
        }
        if(PlayerFirePhase() && !optionsPending)
        {
            DrawHighTrajectoryResponse(p);
            DrawBowFireProgress(p);
        }
        if(PlayerMeleePhase())DrawMeleeGuide(p);
        if(notice!="")GUILayout.Label(notice,panelBody);
        if(s.phase!=Phase.GameOver)
        {
            string caption=s.phase==Phase.Setup?"Begin battle":s.phase==Phase.Orders?"Roll orders":
                PlayerSide()==Side.Saxon&&s.phase==Phase.NormanFire&&!optionsPending?"Resolve Norman opening":
                s.phase==Phase.Reform?"Finish reform":optionsPending?"Choose optional orders above":"Finish segment";
            GUILayout.Space(9*p);
            GUI.enabled=!optionsPending;
            if(GUILayout.Button(caption,panelPrimary,GUILayout.Height(54*p)))Advance();
            GUI.enabled=true;
        }
        GUILayout.EndVertical();
        if(missileResult!=null)DrawMissileResultCard(unitLabels);
        if(meleeResult!=null)DrawMeleeResultCard(unitLabels);
        var units=SelectedUnits();
        if(units.Count>0 || selectedTargets.Count>0)
        {
            GUILayout.BeginVertical(panelCard);
            GUILayout.Label(units.Count==1?"SELECTED UNIT":"SELECTED UNITS",panelSection);
            if(units.Count==1)
            {
                var unit=units[0];
                GUILayout.BeginHorizontal(GUILayout.MinHeight(82*p));
                var texture=CounterTexture(unit);
                if(texture!=null)
                {
                    GUILayout.Label(texture,GUILayout.Width(78*p),GUILayout.Height(78*p));
                    GUILayout.Space(10*p);
                }
                GUILayout.BeginVertical(GUILayout.MinHeight(78*p));
                GUILayout.Label(unitLabels[unit.id],panelBody);
                GUILayout.Label($"Hex {unit.hex} · {unit.status}",panelMuted);
                GUILayout.Label($"{(unit.reduced?"Reduced":"Full")} · {game.OrderFor(unit)}",panelMuted);
                var statusCause=UnitStatusCause(unit,unitLabels);
                if(statusCause!="")GUILayout.Label(statusCause,panelMuted);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            else if(units.Count>1)
            {
                GUILayout.BeginHorizontal(GUILayout.Height(56*p));
                foreach(var unit in units.Take(4))
                {
                    var texture=CounterTexture(unit);
                    if(texture!=null)GUILayout.Label(texture,GUILayout.Width(52*p),GUILayout.Height(52*p));
                    GUILayout.Space(5*p);
                }
                if(units.Count>4)GUILayout.Label("+"+(units.Count-4),panelValue,
                    GUILayout.Width(44*p),GUILayout.Height(52*p));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Label(string.Join(", ",units.Select(u=>unitLabels[u.id]).ToArray()),panelBody);
                foreach(var unit in units.Take(3))
                    GUILayout.Label($"{unitLabels[unit.id]} · Hex {unit.hex} · {unit.status} · "+
                        $"{(unit.reduced?"Reduced":"Full")} · {game.OrderFor(unit)}",panelMuted);
            }
            if(units.Count==1 && game.CanFace(units[0]) && !UnitTypes.Get(units[0]).leader)
            {
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Turn left  ·  Q",panelButton))game.Face(units[0],units[0].facing-1);
                if(GUILayout.Button("Turn right  ·  E",panelButton))game.Face(units[0],units[0].facing+1);
                GUILayout.EndHorizontal();
            }
            if(PlayerMeleePhase() && selectedTargets.Count>0)
            {
                GUILayout.Label("Targets: "+string.Join(", ",selectedTargets.Select(id=>unitLabels[id]).ToArray()),panelMuted);
                if(GUILayout.Button("Resolve selected melee",panelPrimary,GUILayout.Height(45*p)))
                {
                    var targets=s.units.Where(u=>selectedTargets.Contains(u.id)).ToList();
                    if(!game.Melee(units,targets))notice="Illegal melee group or targets.";
                    else ShowMeleeResult();
                }
                if(GUILayout.Button("Clear targets",panelLink))selectedTargets.Clear();
            }
            GUILayout.EndVertical();
        }
        GUILayout.BeginVertical(panelCard);
        GUILayout.Label("REFERENCE",panelSection);
        float referenceWidth=Mathf.Max(120f,(region.width-85*p-controlGap)/2f);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Melee",panelNavButton,GUILayout.Width(referenceWidth),GUILayout.Height(42*p)))OpenChart("melee");
        GUILayout.Space(controlGap);
        if(GUILayout.Button("Missile",panelNavButton,GUILayout.Width(referenceWidth),GUILayout.Height(42*p)))OpenChart("missile");
        GUILayout.EndHorizontal();
        GUILayout.Space(5*p);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Morale",panelNavButton,GUILayout.Width(referenceWidth),GUILayout.Height(42*p)))OpenChart("morale");
        GUILayout.Space(controlGap);
        if(GUILayout.Button("Terrain",panelNavButton,GUILayout.Width(referenceWidth),GUILayout.Height(42*p)))OpenChart("terrain");
        GUILayout.EndHorizontal();
        GUILayout.Space(5*p);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Rulebook PDF",panelLink,GUILayout.Width(referenceWidth),GUILayout.Height(38*p)))
            Application.OpenURL(new Uri(Path.Combine(Application.streamingAssetsPath,"Hastings_1066.pdf")).AbsoluteUri);
        GUILayout.Space(controlGap);
        if(GUILayout.Button(showHelp?"Hide guide":"Controls",panelLink,
            GUILayout.Width(referenceWidth),GUILayout.Height(38*p)))showHelp=!showHelp;
        GUILayout.EndHorizontal();
        if(showHelp)DrawControlsGuide();
        GUILayout.EndVertical();
        GUILayout.BeginVertical(panelCard);
        GUILayout.Label("CASUALTIES",panelSection);
        GUILayout.BeginHorizontal();
        GUILayout.Label("NORMAN  "+s.normanCasualties,panelValue);
        GUILayout.Label("SAXON  "+s.saxonCasualties,panelValue);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.BeginVertical(panelCard);
        GUILayout.Label("RECENT EVENTS",panelSection);
        foreach(var line in s.log.Skip(Math.Max(0,s.log.Count-25)).Reverse())
        {
            GUILayout.Label("•  "+UnitDisplayNames.InEvent(line,unitLabels),panelMuted);
            GUILayout.Space(4);
        }
        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
    private UnitState PendingHighTrajectoryTarget()
    {
        if(game==null || string.IsNullOrEmpty(highTrajectoryTargetId))return null;
        return game.state.units.FirstOrDefault(unit=>unit.id==highTrajectoryTargetId &&
            unit.status!=Status.Eliminated);
    }
    private void DrawHighTrajectoryResponse(float p)
    {
        var target=PendingHighTrajectoryTarget();
        if(target==null)return;
        var shooters=SelectedUnits();
        if(shooters.Count==0 || shooters.Any(shooter=>!game.CanFire(shooter,target,true)))
        {
            highTrajectoryTargetId="";
            return;
        }
        GUILayout.Space(5*p);
        GUILayout.BeginVertical(controlRow);
        GUILayout.Label("DIRECT FIRE BLOCKED",controlHeading);
        GUILayout.Label("The selected bows can fire over the intervening units using high trajectory. This shifts the attack one column left.",controlAction);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Fire high trajectory",fireModeSelected,GUILayout.Height(38*p)))
        {
            if(game.Fire(shooters,target,true))ShowMissileResult();
            else notice="High trajectory fire is no longer legal for that target.";
        }
        if(GUILayout.Button("Cancel",fireModeButton,GUILayout.Height(38*p)))
            highTrajectoryTargetId="";
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
    private void DrawBowFireProgress(float p)
    {
        var missiles=game.Living(PlayerSide()).Where(unit=>
            UnitTypes.Get(unit).missile!="" && unit.status==Status.Ready).ToList();
        int fired=missiles.Count(unit=>unit.fired),total=missiles.Count;
        bool complete=total>0 && fired==total;
        string detail=total==0?"No ready missile units are available.":complete?
            "Every ready missile unit has fired this segment.":
            (total-fired)+" ready missile unit"+(total-fired==1?" remains.":"s remain.");
        string badge=total==0?"NONE":complete?"COMPLETE":fired+" / "+total;
        GUILayout.Space(5*p);
        GUILayout.BeginHorizontal(controlRow);
        GUILayout.BeginVertical();
        GUILayout.Label("MISSILE FIRE",controlHeading);
        GUILayout.Label(detail,controlAction);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.Label(badge,complete?fireCompleteBadge:controlBadge,
            GUILayout.Width(104*p),GUILayout.Height(34*p));
        GUILayout.EndHorizontal();
    }
    private void DrawMeleeGuide(float p)
    {
        var units=SelectedUnits();
        int legalTargets=units.Count==0?0:game.Living(GameEngine.Opposite(PlayerSide())).Count(target=>
            !target.engaged && units.All(attacker=>!attacker.engaged && game.CanMelee(attacker,target)));
        bool leader=units.Any(unit=>UnitTypes.Get(unit).leader);
        bool disrupted=units.Any(unit=>unit.status==Status.Disrupted);
        bool routed=units.Any(unit=>unit.status==Status.Routed);
        bool engaged=units.Any(unit=>unit.engaged);
        bool ready=units.Count>0 && !leader && !disrupted && !routed && !engaged;
        string detail;
        if(units.Count==0)
            detail="Gold outlines mark ready units with an enemy in their frontal hexes.";
        else if(leader)detail="Leaders support a stacked combat unit but cannot attack by themselves.";
        else if(disrupted)detail="A selected unit is disrupted and cannot attack until it rallies.";
        else if(routed)detail="A selected unit is routed and cannot attack until it rallies.";
        else if(engaged)detail="A selected unit has already fought during this melee segment.";
        else if(legalTargets==0)detail=units.Count==1?
            "No enemy is in this unit's two frontal hexes. Change its facing during movement.":
            "No defender is in every selected unit's frontal hexes. Adjust the selection.";
        else detail="Red pulsing markers show legal defenders. Click one to resolve the attack.";
        string badge=units.Count==0?"SELECT":!ready?"BLOCKED":legalTargets+" TARGET"+
            (legalTargets==1?"":"S");
        GUILayout.Space(5*p);
        GUILayout.BeginHorizontal(controlRow);
        GUILayout.BeginVertical();
        GUILayout.Label("MELEE COMBAT",controlHeading);
        GUILayout.Label(detail,controlAction);
        GUILayout.Label("D = disrupted  ·  R = routed  ·  neither can attack until rallied.",controlAction);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.Label(badge,ready&&legalTargets>0?fireCompleteBadge:controlBadge,
            GUILayout.Width(104*p),GUILayout.Height(34*p));
        GUILayout.EndHorizontal();
    }
    private void DrawMissileResultCard(Dictionary<string,string> unitLabels)
    {
        float p=panelScale;
        string targetName;
        if(!unitLabels.TryGetValue(missileResult.targetId,out targetName))
            targetName=missileResult.targetId;
        GUILayout.BeginVertical(panelCard);
        GUILayout.BeginHorizontal();
        GUILayout.Label("MISSILE FIRE RESULT",panelSection);
        GUILayout.FlexibleSpace();
        if(GUILayout.Button("Dismiss",panelLink,GUILayout.Width(78*p),GUILayout.Height(28*p)))
        {missileResult=null;GUILayout.EndHorizontal();GUILayout.EndVertical();return;}
        GUILayout.EndHorizontal();
        GUILayout.Label(MissileOutcome(missileResult),panelValue);
        GUILayout.Label(missileResult.shooterIds.Length+" firing unit"+
            (missileResult.shooterIds.Length==1?"":"s")+" → "+targetName,panelBody);
        GUILayout.Label(missileResult.roll==0?
            missileResult.strength+" attack · "+missileResult.defense+" defense · below 1:4":
            missileResult.strength+" attack · "+missileResult.defense+" defense · roll "+missileResult.roll,
            panelMuted);
        GUILayout.Label(MissileOutcomeDetail(missileResult),panelMuted);
        GUILayout.EndVertical();
    }
    private void DrawMeleeResultCard(Dictionary<string,string> unitLabels)
    {
        float p=panelScale;
        var defenders=meleeResult.defenderIds.Select(id=>
            unitLabels.ContainsKey(id)?unitLabels[id]:id).ToArray();
        GUILayout.BeginVertical(panelCard);
        GUILayout.BeginHorizontal();
        GUILayout.Label("MELEE RESULT",panelSection);
        GUILayout.FlexibleSpace();
        if(GUILayout.Button("Dismiss",panelLink,GUILayout.Width(78*p),GUILayout.Height(28*p)))
        {meleeResult=null;GUILayout.EndHorizontal();GUILayout.EndVertical();return;}
        GUILayout.EndHorizontal();
        GUILayout.Label(MeleeOutcome(meleeResult),panelValue);
        GUILayout.Label(meleeResult.attackerIds.Length+" attacker"+
            (meleeResult.attackerIds.Length==1?"":"s")+" → "+string.Join(", ",defenders),panelBody);
        GUILayout.Label(meleeResult.attack+" attack · "+meleeResult.defense+" defense · differential "+
            (meleeResult.difference>0?"+":"")+meleeResult.difference+" · roll "+meleeResult.roll,
            panelMuted);
        GUILayout.Label(MeleeOutcomeDetail(meleeResult),panelMuted);
        GUILayout.EndVertical();
    }
    private void DrawStrategyEffectsTrack(Rect region)
    {
        Fill(region,new Color(.91f,.87f,.77f));
        Fill(new Rect(region.x,region.y,region.width,3),new Color(.59f,.22f,.17f));
        float headerHeight=showStrategyTrack?48f:region.height;
        string toggleText=showStrategyTrack?
            "STRATEGY EFFECTS     ▲  HIDE":
            "STRATEGY EFFECTS     ▼  SHOW     "+StrategySummary(game.state);
        if(GUI.Button(new Rect(region.x+8,region.y+7,region.width-16,headerHeight-12),
            toggleText,strategyToggle))
        {
            showStrategyTrack=!showStrategyTrack;
            return;
        }
        if(!showStrategyTrack)return;

        float u=Mathf.Clamp(region.height/260f,.88f,1.16f);
        strategyHeading.fontSize=Mathf.RoundToInt(18*u);
        strategyScale.fontSize=Mathf.RoundToInt(18*u);
        strategyMarker.fontSize=Mathf.RoundToInt(18*u);
        strategyLegend.fontSize=Mathf.RoundToInt(16*u);
        strategyBand.fontSize=Mathf.RoundToInt(14*u);
        strategyEffectNote.fontSize=Mathf.RoundToInt(13*u);
        float informationWidth=Mathf.Clamp(region.width*.16f,240*u,330*u);
        float contentTop=region.y+headerHeight+10*u;
        GUI.Label(new Rect(region.x+18*u,contentTop,informationWidth-28*u,27*u),
            "NORMAN",strategyHeading);
        GUI.Label(new Rect(region.x+18*u,contentTop+30*u,informationWidth-28*u,24*u),
            "B  Breton   ·   N  Norman",strategyLegend);
        GUI.Label(new Rect(region.x+18*u,contentTop+53*u,informationWidth-28*u,24*u),
            "F  Franco-Flemish",strategyLegend);
        GUI.Label(new Rect(region.x+18*u,contentTop+83*u,informationWidth-28*u,27*u),
            "SAXON",strategyHeading);
        GUI.Label(new Rect(region.x+18*u,contentTop+113*u,informationWidth-28*u,24*u),
            "L  Left   ·   C  Center   ·   R  Right",strategyLegend);
        GUI.Label(new Rect(region.x+18*u,contentTop+151*u,informationWidth-28*u,22*u),
            "Resets before Assault II",strategyLegend);

        float availableX=region.x+informationWidth;
        float availableWidth=region.width-informationWidth-22*u;
        float trackWidth=Mathf.Min(availableWidth,27*68*u);
        float trackX=availableX+(availableWidth-trackWidth)/2f;
        float trackY=contentTop+88*u;
        float explanationY=region.yMax-54*u;
        float trackHeight=Mathf.Clamp(explanationY-trackY-49*u,35*u,66*u);
        var track=new Rect(trackX,trackY,trackWidth,trackHeight);
        float cellWidth=track.width/27f;
        Fill(track,new Color(.28f,.25f,.20f));
        for(int index=0;index<27;index++)
        {
            int value=index-11;
            var cell=new Rect(track.x+index*cellWidth+1,track.y+1,
                Mathf.Max(1,cellWidth-2),track.height-2);
            Fill(cell,StrategyBandColor(value));
            GUI.Label(cell,Signed(value),strategyScale);
        }
        DrawStrategyBandLabel(track,-11,-9,"ALL: B",u);
        DrawStrategyBandLabel(track,-8,-7,"FOOT: A\nKNIGHTS: B",u);
        DrawStrategyBandLabel(track,-6,-4,"KNIGHTS: A",u);
        DrawStrategyBandLabel(track,5,8,"FOOT: C",u);
        DrawStrategyBandLabel(track,9,12,"FOOT: D\nKNIGHTS: C",u);
        DrawStrategyBandLabel(track,13,15,"ALL: D",u);
        DrawStrategyMarkerSide(track,game.state,new[]{"Breton","Norman","Franco-Flemish"},true,u);
        DrawStrategyMarkerSide(track,game.state,new[]{"Left","Center","Right"},false,u);
        DrawStrategyEffectNotes(track,explanationY,u);
    }
    private void DrawStrategyBandLabel(Rect track,int first,int last,string text,float u)
    {
        float cellWidth=track.width/27f;
        var rect=new Rect(track.x+(first+11)*cellWidth,track.y-93*u,
            (last-first+1)*cellWidth,43*u);
        GUI.Label(rect,text,strategyBand);
    }
    private void DrawStrategyEffectNotes(Rect track,float y,float u)
    {
        string[] notes={
            "<b>B  MORALE GONE</b>\nMorale −1 level; rolls +1",
            "<b>A  MORALE DETERIORATING</b>\nMorale rolls +1",
            "<b>C  FATIGUE SETTING IN</b>\nMovement −1 MP",
            "<b>D  UNITS EXHAUSTED</b>\nMovement −1 MP; combat shifts one column"
        };
        float columnWidth=track.width/4f;
        for(int index=0;index<notes.Length;index++)
            GUI.Label(new Rect(track.x+index*columnWidth+5*u,y,
                columnWidth-10*u,52*u),notes[index],strategyEffectNote);
    }
    private void DrawStrategyMarkerSide(Rect track,GameState state,string[] groupIds,bool above,float u)
    {
        float cellWidth=track.width/27f;
        float badgeWidth=Mathf.Clamp(cellWidth*.72f,30*u,46*u),badgeHeight=36*u;
        var groups=groupIds.Select(id=>state.groups.First(group=>group.id==id)).ToArray();
        for(int index=0;index<groups.Length;index++)
        {
            var group=groups[index];
            int value=Mathf.Clamp(group.effect,-11,15);
            var tied=groups.Where(other=>Mathf.Clamp(other.effect,-11,15)==value).ToArray();
            int tiedIndex=Array.IndexOf(tied,group);
            float spread=Mathf.Min(cellWidth*.80f,badgeWidth*.96f);
            float offset=(tiedIndex-(tied.Length-1)/2f)*spread;
            float targetX=track.x+(value+11+.5f)*cellWidth;
            float badgeY=above?track.y-badgeHeight-8*u:track.yMax+8*u;
            var badge=new Rect(targetX-badgeWidth/2f+offset,badgeY,badgeWidth,badgeHeight);
            float connectorY=above?badge.yMax:track.yMax;
            float connectorHeight=above?track.y-connectorY:badge.y-track.yMax;
            Fill(new Rect(targetX-.7f*u,connectorY,1.4f*u,connectorHeight),
                new Color(.25f,.18f,.13f,.72f));
            var side=above?Side.Norman:Side.Saxon;
            Fill(new Rect(badge.x-1.2f*u,badge.y-1.2f*u,badge.width+2.4f*u,badge.height+2.4f*u),
                new Color(.25f,.15f,.10f));
            Fill(badge,FactionColor(group.id,side));
            GUI.Label(badge,StrategyMarkerLabel(group.id),strategyMarker);
        }
    }
    private static string StrategyMarkerLabel(string group)
    {
        if(group=="Franco-Flemish")return "F";
        return group.Substring(0,1).ToUpperInvariant();
    }
    private static Color StrategyBandColor(int value)
    {
        if(value<=-9)return new Color(.69f,.60f,.60f);
        if(value<=-7)return new Color(.78f,.67f,.62f);
        if(value<=-4)return new Color(.82f,.74f,.62f);
        if(value>=13)return new Color(.69f,.58f,.55f);
        if(value>=9)return new Color(.73f,.65f,.55f);
        if(value>=5)return new Color(.78f,.74f,.60f);
        return new Color(.72f,.84f,.86f);
    }
    private static string StrategySummary(GameState state)
    {
        string[] ids={"Breton","Norman","Franco-Flemish","Left","Center","Right"};
        return "NORMAN  "+StrategyMarkerLabel(ids[0])+" "+Signed(state.groups.First(g=>g.id==ids[0]).effect)+
            "  ·  "+StrategyMarkerLabel(ids[1])+" "+Signed(state.groups.First(g=>g.id==ids[1]).effect)+
            "  ·  "+StrategyMarkerLabel(ids[2])+" "+Signed(state.groups.First(g=>g.id==ids[2]).effect)+
            "        SAXON  "+StrategyMarkerLabel(ids[3])+" "+Signed(state.groups.First(g=>g.id==ids[3]).effect)+
            "  ·  "+StrategyMarkerLabel(ids[4])+" "+Signed(state.groups.First(g=>g.id==ids[4]).effect)+
            "  ·  "+StrategyMarkerLabel(ids[5])+" "+Signed(state.groups.First(g=>g.id==ids[5]).effect);
    }
    private void DrawControlsGuide()
    {
        GUILayout.Label("MAP VIEW",controlHeading);
        DrawControlRow("W A S D", "Pan the map");
        DrawControlRow("RIGHT DRAG", "Pan freely");
        DrawControlRow("WHEEL", "Zoom in or out");
        DrawControlRow("HIDE UNITS", "Inspect terrain beneath counters");

        GUILayout.Label("COMMAND UNITS",controlHeading);
        DrawControlRow("CLICK", "Select · move to a highlight · attack an enemy");
        DrawControlRow("SHIFT + CLICK", "Add units or melee targets");
        DrawControlRow("HOVER STACK", "Spread counters in the same hex");
        DrawControlRow("Q  /  E", "Turn the selected unit");

        GUILayout.Label("GAME FLOW",controlHeading);
        DrawControlRow("ESC", "Open or close the game menu");
    }
    private void DrawControlRow(string control,string action)
    {
        float p=panelScale;
        GUILayout.BeginHorizontal(controlRow,GUILayout.MinHeight(42*p));
        GUILayout.Label(control,controlBadge,GUILayout.Width(118*p),GUILayout.Height(34*p));
        GUILayout.Space(7*p);
        GUILayout.Label(action,controlAction,GUILayout.MinHeight(34*p));
        GUILayout.EndHorizontal();
    }
    private string HoverDescription()
    {
        if(string.IsNullOrEmpty(hoveredHex) || !board.Has(hoveredHex))
            return "Hover over the map for hex details";
        var h=board.Hex(hoveredHex);
        return "HEX "+hoveredHex+"  ·  LEVEL "+h.level+
            (h.road?"  ·  ROAD":"")+
            (h.woods?"  ·  WOODS":"")+
            (h.marsh?"  ·  MARSH":"");
    }
    private string PhaseLabel(Phase phase)
    {
        switch(phase)
        {
            case Phase.Setup:return "Setup";
            case Phase.Orders:return "Choose orders";
            case Phase.NormanMove:return "Norman movement";
            case Phase.NormanFire:return PlayerSide()==Side.Saxon?"Norman AI opening":"Norman missile fire";
            case Phase.NormanDefenseFire:return "Norman defensive fire";
            case Phase.NormanMelee:return "Norman melee";
            case Phase.NormanReaction:return "Norman reaction";
            case Phase.SaxonReaction:return "Saxon reaction";
            case Phase.SaxonDefenseFire:return "Saxon defensive fire";
            case Phase.SaxonFire:return "Saxon missile fire";
            case Phase.SaxonMove:return "Saxon movement";
            case Phase.SaxonMelee:return "Saxon melee";
            case Phase.Reform:return "Reform";
            case Phase.GameOver:return "Battle ended";
            default:return "Battle phase";
        }
    }
    private string PhasePrompt(Phase phase)
    {
        switch(phase)
        {
            case Phase.Setup:return PlayerSide()==Side.Norman?
                "Set Norman facings on the map, then begin the battle.":
                "The Norman setup is ready. Begin when you are prepared to defend Senlac Hill.";
            case Phase.Orders:return "Choose a strategy for each "+
                (PlayerSide()==Side.Norman?"Norman contingent.":"Saxon wing.");
            case Phase.NormanMove:return "Select a Norman unit and click a highlighted destination.";
            case Phase.NormanFire:return PlayerSide()==Side.Saxon?
                "Continue to resolve Norman missile fire and movement.":
                "Gold outlines mark missile units that can fire. Select them, then click an amber Saxon target.";
            case Phase.NormanDefenseFire:return "Gold outlines mark missile units that can fire. Select them, then click an amber Saxon target.";
            case Phase.NormanMelee:return "Select attackers, then click a Saxon defender.";
            case Phase.NormanReaction:return "Select a Norman unit and click a highlighted reaction destination.";
            case Phase.SaxonReaction:return "Select a Saxon unit and click a highlighted reaction destination.";
            case Phase.SaxonDefenseFire:return "Gold outlines mark missile units that can fire. Select them, then click an amber Norman target.";
            case Phase.SaxonFire:return "Gold outlines mark missile units that can fire. Select them, then click an amber Norman target.";
            case Phase.SaxonMove:return "Select a Saxon unit and click a highlighted destination.";
            case Phase.SaxonMelee:return "Select Saxon attackers, then click a Norman defender.";
            case Phase.Reform:return "Move each Norman unit to a legal reform hex.";
            case Phase.GameOver:return "The battle is over.";
            default:return "Resolve any available actions, then finish this segment.";
        }
    }
    private GUIStyle FactionButton(string group)
    {
        if(group=="Breton")return panelBretonButton;
        if(group=="Franco-Flemish")return panelFlemishButton;
        return panelNormanButton;
    }
    private static string StrategyOverview(Strategy strategy)
    {
        switch(strategy)
        {
            case Strategy.Defensive:
                return "Defensive favors holding ground, Shield Wall, and Fire in Place results.";
            case Strategy.Cautious:
                return "Cautious favors controlled advances while retaining a good chance of stationary orders.";
            case Strategy.Moderate:
                return "Moderate balances Advance results with a chance of defensive or aggressive orders.";
            default:
                return "Aggressive makes Charge and other forward-driving orders more likely, increasing fatigue risk over time.";
        }
    }
    private void OpenChart(string name)
    {
        chart=name;
        chartScroll=Vector2.zero;
    }
    private void Advance()
    {
        if(game==null)return;
        var priorPhase=game.state.phase;
        notice="";
        switch(game.state.phase)
        {
            case Phase.Setup:game.Begin();break;
            case Phase.Orders:
                game.ResolveOrders();
                orderReviewMode=false;
                showOrderResults=game.state.orderResults!=null && game.state.orderResults.Count>0;
                orderScroll=Vector2.zero;
                break;
            case Phase.Reform:if(!game.FinishReform())notice="Move every Norman unit to a legal reform hex first.";break;
            default:game.Advance();break;
        }
        selected.Clear();
        selectedTargets.Clear();
        highTrajectoryTargetId="";
        if(game.state.phase!=priorPhase && !PlayerFirePhase())missileResult=null;
        if(game.state.phase!=priorPhase && !PlayerMeleePhase())meleeResult=null;
    }
    private string MeleeFailure(List<UnitState> attackers,UnitState defender)
    {
        if(attackers==null || attackers.Count==0)return "Select one or more attackers first.";
        if(attackers.Any(unit=>UnitTypes.Get(unit).leader))return "Leaders cannot attack by themselves.";
        if(attackers.Any(unit=>unit.status==Status.Disrupted))
            return "D marks a disrupted unit. It cannot attack until it rallies.";
        if(attackers.Any(unit=>unit.status==Status.Routed))
            return "R marks a routed unit. It cannot attack until it rallies.";
        if(attackers.Any(unit=>unit.engaged))return "A selected unit has already fought this segment.";
        if(defender!=null && defender.engaged)return "That defender has already fought this segment.";
        return "That defender is outside one or more attackers' two frontal hexes.";
    }
    private string UnitStatusCause(UnitState unit,Dictionary<string,string> unitLabels)
    {
        if(unit.status!=Status.Disrupted && unit.status!=Status.Routed)return "";
        string statusEvent=unit.id+(unit.status==Status.Routed?" routed":" disrupted");
        int index=game.state.log.FindLastIndex(line=>line.Contains(statusEvent));
        if(index<0)return "";
        for(int prior=index-1;prior>=Math.Max(0,index-3);prior--)
        {
            string line=game.state.log[prior];
            if(line.Contains(unit.id) && line.Contains(" morale "))
                return "Cause · "+UnitDisplayNames.InEvent(line,unitLabels);
            if(line.Contains("fire on "+unit.id+" "))
                return "Cause · "+UnitDisplayNames.InEvent(line,unitLabels);
        }
        return unit.status==Status.Routed?
            "Cause · combat result or rout shock; see Recent Events.":
            "Cause · combat result or failed morale check; see Recent Events.";
    }
    private void ShowMeleeResult()
    {
        meleeResult=game.lastMeleeResult;
        meleeEffectStarted=Time.unscaledTime;
        meleeEffectUntil=meleeEffectStarted+4.3f;
        missileResult=null;notice="";
        selected.Clear();selectedTargets.Clear();
    }
    private void ShowMissileResult()
    {
        missileResult=game.lastFireResult;
        missileEffectStarted=Time.unscaledTime;
        missileEffectUntil=missileEffectStarted+3.6f;
        meleeResult=null;notice="";highTrajectoryTargetId="";
        selected.Clear();selectedTargets.Clear();
    }
    private void ChooseOptionalOrder(string group,bool knight,Order order)
    {
        if(game.SetOptionalOrder(group,knight,order))notice="";
    }
    private void DrawMenu()
    {
        if(game!=null)
        {
            GUI.color=new Color(.04f,.03f,.02f,.72f);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
            GUI.color=Color.white;
        }
        float width=Mathf.Clamp(Screen.width*.47f,620f,1250f);
        float preferredHeight=game==null && menuPage=="main"?
            Mathf.Clamp(Screen.height*.45f,540f,760f):
            Mathf.Clamp(Screen.height*.62f,650f,1060f);
        float height=Mathf.Min(Screen.height-30f,preferredHeight);
        var rect=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
        GUI.color=new Color(.22f,.10f,.07f,.98f);
        GUI.DrawTexture(rect,Texture2D.whiteTexture);
        GUI.color=new Color(.94f,.87f,.72f,.96f);
        GUI.DrawTexture(new Rect(rect.x+5,rect.y+5,rect.width-10,rect.height-10),Texture2D.whiteTexture);
        GUI.color=Color.white;
        float inset=Mathf.Clamp(width*.08f,35,80);
        float buttonHeight=Mathf.Clamp(Screen.height*.055f,54,96);
        GUILayout.BeginArea(new Rect(rect.x+inset,rect.y+25,rect.width-2*inset,rect.height-50));
        GUILayout.Label("HASTINGS 1066",menuTitle,GUILayout.Height(Mathf.Clamp(Screen.height*.10f,75,115)));
        GUILayout.Label(menuPage=="main"?"The Battle for Senlac Hill":
            menuPage=="load"?"Load a game":menuPage=="save"?"Save your battle":
            menuPage=="newSide"?"Choose your army":"Start a new battle?",
            menuSubtitle,GUILayout.Height(42));
        GUILayout.Space(28);
        if(menuPage=="main")
        {
            if(game!=null)
            {
                if(GUILayout.Button("Resume Battle",menuPrimary,GUILayout.Height(buttonHeight)))showMenu=false;
                GUILayout.Space(12);
            }
            if(GUILayout.Button("New Game",game==null?menuPrimary:menuButton,GUILayout.Height(buttonHeight)))
            {menuPage=game==null?"newSide":"newConfirm";}
            GUILayout.Space(12);
            if(game!=null)
            {
                if(GUILayout.Button("Save Game",menuButton,GUILayout.Height(buttonHeight)))menuPage="save";
                GUILayout.Space(12);
            }
            if(GUILayout.Button("Load Game",menuButton,GUILayout.Height(buttonHeight)))menuPage="load";
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("Quit",menuButton,GUILayout.Height(buttonHeight)))Application.Quit();
        }
        else if(menuPage=="load")
        {
            var slots=GameStorage.Slots();
            if(slots.Length==0)GUILayout.Label("No saved games yet.",menuSubtitle);
            menuScroll=GUILayout.BeginScrollView(menuScroll);
            foreach(var slot in slots)
            {
                if(GUILayout.Button(slot,menuButton,GUILayout.Height(buttonHeight*.82f)))
                {
                    try
                    {
                        game=new GameEngine(board,GameStorage.Load(slot));saveSlot=slot;
                        selected.Clear();selectedTargets.Clear();showMenu=false;menuPage="main";notice="";
                        showUnits=true;chart="";showOrderResults=false;orderReviewMode=false;
                        highTrajectoryTargetId="";
                        stackSpread.Clear();hoveredHex="";
                        missileResult=null;meleeResult=null;
                        lastMapWidth=0;scale=0;fullMapMode=false;
                    }
                    catch(Exception ex){notice="Load failed: "+ex.Message;}
                }
                GUILayout.Space(9);
            }
            GUILayout.EndScrollView();
            if(GUILayout.Button("Back",menuButton,GUILayout.Height(buttonHeight)))menuPage="main";
        }
        else if(menuPage=="save")
        {
            GUILayout.Label("Name this save:",menuSubtitle);
            saveSlot=GUILayout.TextField(saveSlot,menuTextField,GUILayout.Height(buttonHeight));
            GUILayout.Space(12);
            if(GUILayout.Button("Save Game",menuButton,GUILayout.Height(buttonHeight)))
            {
                try{GameStorage.Save(saveSlot,game.state);notice="Saved as " +saveSlot;}
                catch(Exception ex){notice="Save failed: "+ex.Message;}
            }
            GUILayout.Space(12);
            GUILayout.Label("Choose an existing slot to overwrite:",menuSubtitle);
            menuScroll=GUILayout.BeginScrollView(menuScroll);
            foreach(var slot in GameStorage.Slots())
                if(GUILayout.Button(slot,menuButton,GUILayout.Height(buttonHeight*.72f)))saveSlot=slot;
            GUILayout.EndScrollView();
            if(GUILayout.Button("Back",menuButton,GUILayout.Height(buttonHeight)))menuPage="main";
        }
        else if(menuPage=="newConfirm")
        {
            GUILayout.Label("Unsaved progress in the current battle will be lost.",menuSubtitle);
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("Choose a Side",menuPrimary,GUILayout.Height(buttonHeight)))menuPage="newSide";
            GUILayout.Space(12);
            if(GUILayout.Button("Cancel",menuButton,GUILayout.Height(buttonHeight)))menuPage="main";
        }
        else if(menuPage=="newSide")
        {
            const string normanDescription=
                "Attack Senlac Hill with the Breton, Norman, and Franco-Flemish contingents.";
            const string saxonDescription=
                "Defend the ridge with the left, center, and right wings. The battlefield rotates to your viewpoint.";
            float descriptionWidth=rect.width-2*inset;
            GUILayout.Label("The opposing army will be controlled by the AI.",menuSubtitle,
                GUILayout.Height(42));
            GUILayout.Space(18);
            if(GUILayout.Button("Play as the Normans",menuPrimary,GUILayout.Height(buttonHeight)))
                StartNewGame(Side.Norman);
            GUILayout.Label(normanDescription,menuDescription,GUILayout.Height(
                menuDescription.CalcHeight(new GUIContent(normanDescription),descriptionWidth)+8f));
            GUILayout.Space(14);
            if(GUILayout.Button("Play as the Saxons",menuPrimary,GUILayout.Height(buttonHeight)))
                StartNewGame(Side.Saxon);
            GUILayout.Label(saxonDescription,menuDescription,GUILayout.Height(
                menuDescription.CalcHeight(new GUIContent(saxonDescription),descriptionWidth)+8f));
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("Back",menuButton,GUILayout.Height(buttonHeight)))menuPage="main";
        }
        if(notice!="")GUILayout.Label(notice,small);
        GUILayout.EndArea();
    }
    private static void RestoreWindowedDisplay()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        const int version=1;
        if(PlayerPrefs.GetInt(DisplayPrefsVersion,0)>=version)return;
        const float preferredWidth=1600f,preferredHeight=1000f;
        float fit=Mathf.Min(1f,Display.main.systemWidth*.85f/preferredWidth,
            Display.main.systemHeight*.82f/preferredHeight);
        Screen.SetResolution(Mathf.RoundToInt(preferredWidth*fit),
            Mathf.RoundToInt(preferredHeight*fit),FullScreenMode.Windowed);
        PlayerPrefs.SetInt(DisplayPrefsVersion,version);
        PlayerPrefs.Save();
#endif
    }
    private void StartNewGame(Side playerSide)
    {
        game=new GameEngine(board,Setup.New(board,(uint)DateTime.UtcNow.Ticks,playerSide));
        selected.Clear();selectedTargets.Clear();showMenu=false;menuPage="main";notice="";
        showUnits=true;chart="";showOrderResults=false;orderReviewMode=false;
        highTrajectoryTargetId="";
        stackSpread.Clear();hoveredHex="";
        missileResult=null;meleeResult=null;
        lastMapWidth=0;scale=0;fullMapMode=false;
    }
    private void DrawOrderResults()
    {
        if(game==null || game.state.orderResults==null || game.state.orderResults.Count==0)
        {showOrderResults=false;return;}
        if(orderReviewMode && !game.OptionsPending())
        {DrawOrderReview();return;}
        DrawOrderResolution();
    }
    private void DrawOrderResolution()
    {
        orderScale=Mathf.Clamp(Screen.height/1000f,1f,1.55f);
        float u=orderScale;
        orderTitle.fontSize=Mathf.RoundToInt(28*u);
        orderSubtitle.fontSize=Mathf.RoundToInt(15*u);
        orderSection.fontSize=Mathf.RoundToInt(17*u);
        orderCardTitle.fontSize=Mathf.RoundToInt(20*u);
        orderRoll.fontSize=Mathf.RoundToInt(13*u);
        orderType.fontSize=Mathf.RoundToInt(11*u);
        orderName.fontSize=Mathf.RoundToInt(17*u);
        orderText.fontSize=Mathf.RoundToInt(13*u);
        orderChoice.fontSize=Mathf.RoundToInt(13*u);
        orderEffectHeading.fontSize=Mathf.RoundToInt(12*u);
        orderEffectValue.fontSize=Mathf.RoundToInt(12*u);
        orderEffectDetail.fontSize=Mathf.RoundToInt(12*u);
        orderEffectWarning.fontSize=orderEffectDetail.fontSize;
        orderCard.padding=new RectOffset(Mathf.RoundToInt(18*u),Mathf.RoundToInt(18*u),
            Mathf.RoundToInt(14*u),Mathf.RoundToInt(16*u));
        orderCard.margin=new RectOffset(0,0,0,Mathf.RoundToInt(10*u));
        orderRoll.padding=new RectOffset(Mathf.RoundToInt(10*u),Mathf.RoundToInt(10*u),
            Mathf.RoundToInt(5*u),Mathf.RoundToInt(5*u));
        orderEffect.padding=new RectOffset(Mathf.RoundToInt(11*u),Mathf.RoundToInt(11*u),
            Mathf.RoundToInt(9*u),Mathf.RoundToInt(9*u));
        orderEffectValue.padding=new RectOffset(Mathf.RoundToInt(8*u),Mathf.RoundToInt(8*u),
            Mathf.RoundToInt(4*u),Mathf.RoundToInt(4*u));

        Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.12f,.09f,.06f,.68f));
        float width=Mathf.Min(Screen.width-40f,1240f*u);
        float height=Mathf.Min(Screen.height-40f,900f*u);
        var rect=new Rect((Screen.width-width)/2f,(Screen.height-height)/2f,width,height);
        Fill(rect,new Color(.94f,.90f,.81f));
        Fill(new Rect(rect.x,rect.y,rect.width,66*u),new Color(.60f,.21f,.16f));
        GUI.BeginGroup(rect);
        GUI.Label(new Rect(27*u,17*u,width-210*u,42*u),"BATTLE ORDERS",orderTitle);
        bool optionsPending=game.OptionsPending();
        GUI.enabled=!optionsPending;
        if(GUI.Button(new Rect(width-120*u,18*u,94*u,34*u),"Close  ×",panelLink))
        {showOrderResults=false;GUI.EndGroup();return;}
        GUI.enabled=true;
        GUI.Label(new Rect(30*u,76*u,width-60*u,48*u),
            "Each Norman foot and knight section rolls 2d6 separately. Their strategy effects are combined for the nationality. Saxon wings each use one roll.",
            orderSubtitle);

        var contentRect=new Rect(27*u,128*u,width-54*u,height-210*u);
        GUILayout.BeginArea(contentRect);
        orderScroll=GUILayout.BeginScrollView(orderScroll);
        DrawOrderSide(PlayerSide()==Side.Norman?"YOUR NORMAN ORDERS":"NORMAN ORDERS",
            Side.Norman,contentRect.width-22*u);
        GUILayout.Space(9*u);
        DrawOrderSide(PlayerSide()==Side.Saxon?"YOUR SAXON ORDERS":"SAXON ORDERS",
            Side.Saxon,contentRect.width-22*u);
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        string buttonText=optionsPending?"Choose the optional orders above":"Continue to battle";
        GUI.enabled=!optionsPending;
        if(GUI.Button(new Rect(27*u,height-68*u,width-54*u,48*u),buttonText,panelPrimary))
            showOrderResults=false;
        GUI.enabled=true;
        GUI.EndGroup();
    }
    private void DrawOrderReview()
    {
        float u=Mathf.Clamp(Screen.height/900f,.90f,1.28f);
        orderTitle.fontSize=Mathf.RoundToInt(27*u);
        orderSubtitle.fontSize=Mathf.RoundToInt(14*u);
        chartTab.fontSize=Mathf.RoundToInt(14*u);
        chartTabSelected.fontSize=Mathf.RoundToInt(15*u);
        chartHeader.fontSize=Mathf.RoundToInt(13*u);
        chartRowHeader.fontSize=Mathf.RoundToInt(15*u);
        chartCell.fontSize=Mathf.RoundToInt(13*u);
        chartMuted.fontSize=Mathf.RoundToInt(12*u);

        Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.12f,.09f,.06f,.68f));
        float width=Mathf.Min(Screen.width-50f,960f*u);
        float height=Mathf.Min(Screen.height-50f,520f*u);
        var rect=new Rect((Screen.width-width)/2f,(Screen.height-height)/2f,width,height);
        Fill(rect,new Color(.94f,.90f,.81f));
        Fill(new Rect(rect.x,rect.y,rect.width,64*u),new Color(.60f,.21f,.16f));
        GUI.BeginGroup(rect);
        GUI.Label(new Rect(27*u,15*u,width-205*u,42*u),"BATTLE ORDERS",orderTitle);
        if(GUI.Button(new Rect(width-120*u,16*u,94*u,34*u),"Close  ×",panelLink))
        {showOrderResults=false;GUI.EndGroup();return;}
        GUI.Label(new Rect(30*u,70*u,width-60*u,30*u),
            "Current orders for Assault "+game.state.period+", turn "+game.state.turn+".",
            orderSubtitle);

        string[] tabs={"BRETON","NORMAN","FRANCO-FLEMISH","SAXONS"};
        float tabsX=30*u,tabsY=102*u,tabsWidth=width-60*u,gap=6*u;
        float tabWidth=(tabsWidth-gap*3)/4f;
        for(int index=0;index<tabs.Length;index++)
        {
            var tabRect=new Rect(tabsX+index*(tabWidth+gap),tabsY,tabWidth,42*u);
            if(GUI.Button(tabRect,tabs[index],index==orderReviewTab?chartTabSelected:chartTab))
                orderReviewTab=index;
        }
        var tableRect=new Rect(40*u,156*u,width-80*u,height-181*u);
        if(orderReviewTab<3)
            DrawNormanOrderReview(tableRect,
                new[]{"Breton","Norman","Franco-Flemish"}[orderReviewTab],u);
        else DrawSaxonOrderReview(tableRect,u);
        GUI.EndGroup();
    }
    private void DrawNormanOrderReview(Rect rect,string groupId,float u)
    {
        var result=game.state.orderResults.LastOrDefault(item=>item.group==groupId &&
            item.side==Side.Norman);
        if(result==null)return;
        float headingHeight=40*u,gap=12*u;
        chartRowHeader.normal.textColor=FactionColor(groupId,Side.Norman);
        GUI.Label(new Rect(rect.x,rect.y,rect.width,headingHeight),
            groupId.ToUpperInvariant()+"  ·  "+result.strategy.ToString().ToUpperInvariant()+
            " STRATEGY  ·  EFFECT "+Signed(result.totalEffect),chartRowHeader);
        chartRowHeader.normal.textColor=new Color(.27f,.19f,.14f);
        float trackHeight=(rect.height-headingHeight-gap)/2f;
        DrawOrderTrack(new Rect(rect.x,rect.y+headingHeight,rect.width,trackHeight),
            "FOOT",new[]{"SHIELD\nWALL","ADVANCE","ADVANCE\n2 TURNS","FIRE IN\nPLACE"},
            OrderReviewColumn(result,false,false),result,false,
            FactionColor(groupId,Side.Norman),u);
        DrawOrderTrack(new Rect(rect.x,rect.y+headingHeight+trackHeight+gap,rect.width,trackHeight),
            "KNIGHTS",new[]{"HOLD","CHARGE","CHARGE\n2 TURNS","ADVANCE","ADVANCE\n2 TURNS"},
            OrderReviewColumn(result,true,false),result,true,
            FactionColor(groupId,Side.Norman),u);
    }
    private void DrawSaxonOrderReview(Rect rect,float u)
    {
        string[] columns={"SHIELD\nWALL","ATTACK &\nPURSUE","ATTACK & PURSUE\n2 TURNS",
            "ADVANCE","MELEE IN\nPLACE"};
        string[] groups={"Left","Center","Right"};
        float headingWidth=Mathf.Clamp(rect.width*.20f,165*u,220*u);
        float columnWidth=(rect.width-headingWidth)/columns.Length;
        float headerHeight=58*u,rowHeight=(rect.height-headerHeight)/3f;
        Color border=new Color(.35f,.27f,.20f),slot=new Color(.93f,.90f,.82f);
        Fill(rect,border);
        DrawOrderReviewCell(new Rect(rect.x+1,rect.y+1,headingWidth-2,headerHeight-2),
            new Color(.55f,.23f,.17f),"SAXON WING",chartHeader);
        for(int column=0;column<columns.Length;column++)
            DrawOrderReviewCell(new Rect(rect.x+headingWidth+column*columnWidth+1,rect.y+1,
                columnWidth-2,headerHeight-2),new Color(.55f,.23f,.17f),columns[column],chartHeader);
        foreach(var pair in groups.Select((group,index)=>new{group,index}))
        {
            var result=game.state.orderResults.LastOrDefault(item=>item.group==pair.group &&
                item.side==Side.Saxon);
            float y=rect.y+headerHeight+pair.index*rowHeight;
            var heading=new Rect(rect.x+1,y+1,headingWidth-2,rowHeight-2);
            DrawOrderRowHeading(heading,pair.group.ToUpperInvariant(),
                result==null?null:OrderCounterTexture(result,false),FactionColor(pair.group,Side.Saxon),
                result==null?"":result.strategy.ToString().ToUpperInvariant()+" · "+
                    Signed(result.totalEffect),u);
            int selected=result==null?-1:OrderReviewColumn(result,false,true);
            for(int column=0;column<columns.Length;column++)
            {
                var cell=new Rect(rect.x+headingWidth+column*columnWidth+1,y+1,
                    columnWidth-2,rowHeight-2);
                DrawOrderReviewCell(cell,slot,"",chartCell);
                if(column==selected)
                    DrawOrderMarker(cell,FactionColor(pair.group,Side.Saxon),
                        OrderReviewRoll(result,false),u);
            }
        }
    }
    private void DrawOrderTrack(Rect rect,string rowTitle,string[] columns,int selected,
        OrderRollResult result,bool knight,Color markerColor,float u)
    {
        float headingWidth=Mathf.Clamp(rect.width*.18f,150*u,205*u);
        float columnWidth=(rect.width-headingWidth)/columns.Length;
        float headerHeight=52*u,slotHeight=rect.height-headerHeight;
        Color border=new Color(.35f,.27f,.20f),slot=new Color(.93f,.90f,.82f);
        Fill(rect,border);
        DrawOrderRowHeading(new Rect(rect.x+1,rect.y+1,headingWidth-2,rect.height-2),
            rowTitle,OrderCounterTexture(result,knight),markerColor,"",u);
        for(int column=0;column<columns.Length;column++)
        {
            var header=new Rect(rect.x+headingWidth+column*columnWidth+1,rect.y+1,
                columnWidth-2,headerHeight-2);
            var cell=new Rect(header.x,rect.y+headerHeight+1,columnWidth-2,slotHeight-2);
            DrawOrderReviewCell(header,new Color(.55f,.23f,.17f),columns[column],chartHeader);
            DrawOrderReviewCell(cell,slot,"",chartCell);
            if(column==selected)DrawOrderMarker(cell,markerColor,OrderReviewRoll(result,knight),u);
        }
    }
    private void DrawOrderMarker(Rect slot,Color color,string label,float u)
    {
        float size=Mathf.Min(slot.width*.48f,slot.height*.66f);
        size=Mathf.Max(size,38*u);
        var marker=new Rect(slot.center.x-size/2,slot.center.y-size/2,size,size);
        Fill(new Rect(marker.x+3*u,marker.y+3*u,marker.width,marker.height),
            new Color(.18f,.12f,.08f,.28f));
        Fill(new Rect(marker.x-2*u,marker.y-2*u,marker.width+4*u,marker.height+4*u),
            new Color(.22f,.15f,.10f));
        Fill(marker,color);
        int oldFontSize=chartHeader.fontSize;
        chartHeader.fontSize=Mathf.RoundToInt(12*u);
        GUI.Label(marker,label,chartHeader);
        chartHeader.fontSize=oldFontSize;
    }
    private void DrawOrderRowHeading(Rect rect,string label,Texture2D texture,Color accent,
        string footer,float u)
    {
        Fill(rect,new Color(.88f,.84f,.75f));
        Fill(new Rect(rect.x,rect.y,8*u,rect.height),accent);
        float labelHeight=25*u,footerHeight=footer==""?0:19*u;
        GUI.Label(new Rect(rect.x+10*u,rect.y+3*u,rect.width-14*u,labelHeight),
            label,chartRowHeader);
        float availableHeight=rect.height-labelHeight-footerHeight-11*u;
        float imageSize=Mathf.Min(64*u,Mathf.Min(rect.width*.48f,availableHeight));
        if(texture!=null && imageSize>18*u)
            GUI.DrawTexture(new Rect(rect.center.x-imageSize/2,
                rect.y+labelHeight+5*u,imageSize,imageSize),texture,ScaleMode.ScaleToFit);
        if(footer!="")
            GUI.Label(new Rect(rect.x+10*u,rect.yMax-footerHeight-3*u,
                rect.width-14*u,footerHeight),footer,chartMuted);
    }
    private static void DrawOrderReviewCell(Rect rect,Color color,string text,GUIStyle style)
    {
        Fill(rect,color);
        if(text!="")GUI.Label(new Rect(rect.x+4,rect.y+3,rect.width-8,rect.height-6),text,style);
    }
    private static int OrderReviewColumn(OrderRollResult result,bool knight,bool saxon)
    {
        Order order=knight?result.knightOrder:result.footOrder;
        bool twoTurns=knight?
            result.knightDuration>1||result.knightContinued:
            result.footDuration>1||result.footContinued;
        if(knight)
        {
            if(order==Order.Hold)return 0;
            if(order==Order.Charge)return twoTurns?2:1;
            return twoTurns?4:3;
        }
        if(saxon)
        {
            if(order==Order.ShieldWall)return 0;
            if(order==Order.AttackPursue)return twoTurns?2:1;
            if(order==Order.Advance)return 3;
            return 4;
        }
        if(order==Order.ShieldWall)return 0;
        if(order==Order.Advance)return twoTurns?2:1;
        return 3;
    }
    private static string OrderReviewRoll(OrderRollResult result,bool knight)
    {
        bool continued=knight?result.knightContinued:result.footContinued;
        if(continued)return "CONT.";
        int roll=knight?result.knightRoll:result.footRoll;
        if(roll<=0)roll=result.roll;
        return roll>0?"2D6\n"+roll:"CHOSEN";
    }
    private void DrawOrderSide(string heading,Side side,float contentWidth)
    {
        float u=orderScale;
        var results=game.state.orderResults.Where(result=>result.side==side).ToList();
        if(results.Count==0)return;
        GUILayout.Label(heading,orderSection,GUILayout.Height(27*u));
        foreach(var result in results)DrawOrderResultCard(result,contentWidth);
    }
    private void DrawOrderResultCard(OrderRollResult result,float contentWidth)
    {
        float u=orderScale;
        var group=game.state.groups.First(g=>g.id==result.group);
        GUILayout.BeginVertical(orderCard,GUILayout.Width(contentWidth));
        GUILayout.BeginHorizontal();
        var oldTitleColor=orderCardTitle.normal.textColor;
        orderCardTitle.normal.textColor=FactionColor(result.group,result.side);
        GUILayout.Label(result.group.ToUpperInvariant(),orderCardTitle,GUILayout.Height(31*u));
        orderCardTitle.normal.textColor=oldTitleColor;
        GUILayout.FlexibleSpace();
        GUILayout.Label(result.strategy.ToString().ToUpperInvariant(),
            orderRoll,GUILayout.Height(31*u));
        GUILayout.EndHorizontal();
        GUILayout.Space(5*u);
        GUILayout.BeginHorizontal();
        float columnWidth=(contentWidth-58*u)/(result.hasKnights?2f:1f);
        DrawOrderColumn(result,"FOOT",false,group.footOptional,columnWidth);
        if(result.hasKnights)
        {
            GUILayout.Space(12*u);
            DrawOrderColumn(result,"KNIGHTS",true,group.knightOptional,columnWidth);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(8*u);
        GUILayout.BeginVertical(orderEffect);
        GUILayout.BeginHorizontal();
        GUILayout.Label("STRATEGY EFFECT",orderEffectHeading,GUILayout.Height(30*u));
        GUILayout.FlexibleSpace();
        GUILayout.Label(Signed(result.effectChange)+"  THIS TURN",orderEffectValue,
            GUILayout.Width(128*u),GUILayout.Height(30*u));
        GUILayout.Space(7*u);
        GUILayout.Label(Signed(result.totalEffect)+"  TOTAL",orderEffectValue,
            GUILayout.Width(108*u),GUILayout.Height(30*u));
        GUILayout.EndHorizontal();
        GUILayout.Space(5*u);
        bool penalty=StrategyEffects.Code(false,result.totalEffect)!='-' ||
            (result.hasKnights && StrategyEffects.Code(true,result.totalEffect)!='-');
        GUILayout.Label(EffectExplanation(result.totalEffect,result.hasKnights),
            penalty?orderEffectWarning:orderEffectDetail);
        GUILayout.EndVertical();
        GUILayout.EndVertical();
    }
    private void DrawOrderColumn(OrderRollResult result,string unitKind,bool knight,
        bool choicePending,float width)
    {
        float u=orderScale;
        bool optional=knight?result.knightOptional:result.footOptional;
        bool continued=knight?result.knightContinued:result.footContinued;
        int duration=knight?result.knightDuration:result.footDuration;
        Order order=knight?result.knightOrder:result.footOrder;
        GUILayout.BeginHorizontal(GUILayout.Width(width));
        var counter=OrderCounterTexture(result,knight);
        if(counter!=null)
        {
            GUILayout.Label(counter,GUILayout.Width(76*u),GUILayout.Height(76*u));
            GUILayout.Space(10*u);
        }
        GUILayout.BeginVertical();
        int sectionRoll=knight?result.knightRoll:result.footRoll;
        if(sectionRoll<=0 && !continued)sectionRoll=result.roll;
        GUILayout.Label(unitKind+"   ·   "+(continued?"CONTINUED ORDER":"2D6: "+sectionRoll),
            orderType,GUILayout.Height(18*u));
        GUILayout.Label(choicePending?"CHOOSE AN ORDER":OrderDisplay(order),orderName,
            GUILayout.Height(27*u));
        string timing=choicePending?"Optional result · your choice is required":
            optional?(result.side==PlayerSide()?"Optional result · your selected order":
                "Optional result · AI selected this order"):
            continued?"Continues from last turn · "+duration+" turn remaining":
            duration>1?"Remains in effect for "+duration+" turns":"Applies this turn";
        GUILayout.Label(timing,orderText);
        GUILayout.Space(3*u);
        if(choicePending)
        {
            GUILayout.Label("Choose this section's order:",orderText);
            GUILayout.BeginHorizontal();
            if(knight)
            {
                if(GUILayout.Button("Hold",orderChoice,GUILayout.Height(36*u)))
                    ChooseOptionalOrder(result.group,true,Order.Hold);
                if(GUILayout.Button("Advance",orderChoice,GUILayout.Height(36*u)))
                    ChooseOptionalOrder(result.group,true,Order.Advance);
                if(GUILayout.Button("Charge",orderChoice,GUILayout.Height(36*u)))
                    ChooseOptionalOrder(result.group,true,Order.Charge);
            }
            else
            {
                if(GUILayout.Button("Shield Wall",orderChoice,GUILayout.Height(36*u)))
                    ChooseOptionalOrder(result.group,false,Order.ShieldWall);
                if(result.side==Side.Saxon)
                {
                    if(GUILayout.Button("Attack & Pursue",orderChoice,GUILayout.Height(36*u)))
                        ChooseOptionalOrder(result.group,false,Order.AttackPursue);
                }
                else if(GUILayout.Button("Fire in Place",orderChoice,GUILayout.Height(36*u)))
                    ChooseOptionalOrder(result.group,false,Order.FireInPlace);
                if(GUILayout.Button("Advance",orderChoice,GUILayout.Height(36*u)))
                    ChooseOptionalOrder(result.group,false,Order.Advance);
            }
            GUILayout.EndHorizontal();
        }
        else GUILayout.Label(OrderDescription(order),orderText);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }
    private Texture2D OrderCounterTexture(OrderRollResult result,bool knight)
    {
        string path;
        if(result.side==Side.Norman)
            path="Art/Counters/Normans/"+result.group+"_"+(knight?"Cavalry":"Infantry");
        else
        {
            var unit=game.state.units.FirstOrDefault(candidate=>candidate.side==Side.Saxon &&
                candidate.group==result.group && candidate.status!=Status.Eliminated &&
                !UnitTypes.Get(candidate).leader);
            if(unit==null)return null;
            path="Art/Counters/Saxons/"+UnitTypes.Get(unit).art;
        }
        Texture2D texture;
        if(!counters.TryGetValue(path,out texture))
        {texture=Resources.Load<Texture2D>(path);counters[path]=texture;}
        return texture;
    }
    private static Color FactionColor(string group,Side side)
    {
        if(side==Side.Saxon)return new Color(.48f,.31f,.08f);
        if(group=="Breton")return new Color(.18f,.40f,.20f);
        if(group=="Franco-Flemish")return new Color(.13f,.34f,.52f);
        return new Color(.60f,.16f,.12f);
    }
    private static string OrderDisplay(Order order)
    {
        switch(order)
        {
            case Order.ShieldWall:return "SHIELD WALL";
            case Order.FireInPlace:return "FIRE IN PLACE";
            case Order.AttackPursue:return "ATTACK & PURSUE";
            default:return order.ToString().ToUpperInvariant();
        }
    }
    private static string OrderDescription(Order order)
    {
        switch(order)
        {
            case Order.ShieldWall:return "Uses shield-wall combat values; may retreat one hex; cannot react. Bowmen fire in place.";
            case Order.FireInPlace:return "May fire and fight normally; may advance or retreat one hex; reaction is allowed.";
            case Order.Advance:return "Normal movement and combat. Movement is voluntary.";
            case Order.AttackPursue:return "Must close with the enemy; cannot react; disruptions inflicted become routs; must pursue.";
            case Order.Hold:return "Uses normal knight combat strength; may retreat one hex; reaction is allowed.";
            case Order.Charge:return "6 MP and must close with the enemy. A legal charge gains +1 attack (+2 downhill), turns disruption into rout, and requires pursuit and a morale check.";
            default:return "";
        }
    }
    private static string EffectExplanation(int effect,bool hasKnights)
    {
        char foot=StrategyEffects.Code(false,effect);
        if(!hasKnights)return EffectCodeDescription(foot);
        char knights=StrategyEffects.Code(true,effect);
        if(foot==knights)return "All units: "+EffectCodeDescription(foot);
        return "Foot: "+EffectCodeDescription(foot)+"   Knights: "+EffectCodeDescription(knights);
    }
    private static string EffectCodeDescription(char code)
    {
        switch(code)
        {
            case 'A':return "morale rolls +1.";
            case 'B':return "morale rating −1 level; morale rolls +1.";
            case 'C':return "movement −1 MP.";
            case 'D':return "movement −1 MP; combat −1 column.";
            default:return "no penalty at this level.";
        }
    }
    private static string Signed(int value)
    {return value>0?"+"+value:value.ToString();}
    private void DrawChart()
    {
        chartScale=Mathf.Clamp(Screen.height/1080f,1f,1.8f);
        chartTitle.fontSize=Mathf.RoundToInt(28*chartScale);
        chartSection.fontSize=Mathf.RoundToInt(19*chartScale);
        chartHeader.fontSize=Mathf.RoundToInt(15*chartScale);
        chartRowHeader.fontSize=chartHeader.fontSize;
        chartCell.fontSize=Mathf.RoundToInt(16*chartScale);
        chartMuted.fontSize=chartCell.fontSize;
        chartNote.fontSize=chartCell.fontSize;
        chartTab.fontSize=Mathf.RoundToInt(15*chartScale);
        chartTabSelected.fontSize=Mathf.RoundToInt(18*chartScale);
        chartClose.fontSize=Mathf.RoundToInt(14*chartScale);
        float u=chartScale;
        Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.12f,.09f,.06f,.65f));
        float width=Mathf.Min(Screen.width-40f,1180f*u);
        float height=Mathf.Min(Screen.height-40f,720f*u);
        var rect=new Rect((Screen.width-width)/2f,(Screen.height-height)/2f,width,height);
        Fill(rect,new Color(.95f,.92f,.84f));
        Fill(new Rect(rect.x,rect.y,rect.width,56*u),new Color(.60f,.21f,.16f));
        GUI.BeginGroup(rect);
        GUI.Label(new Rect(25*u,18*u,width-180*u,38*u),"BATTLE REFERENCE",chartTitle);
        if(GUI.Button(new Rect(width-116*u,20*u,90*u,32*u),"Close  ×",chartClose))
        {chart="";GUI.EndGroup();return;}
        GUI.Label(new Rect(27*u,59*u,width-54*u,26*u),
            "Tables shown here match the results used by the game engine.",chartNote);
        string[] names={"melee","missile","morale","terrain"};
        for(int i=0;i<names.Length;i++)
        {
            float tabWidth=(width-52*u)/names.Length;
            var tab=new Rect(26*u+i*tabWidth,91*u,tabWidth-5*u,37*u);
            if(GUI.Button(tab,UpperFirst(names[i]),chart==names[i]?chartTabSelected:chartTab))
                OpenChart(names[i]);
        }
        float contentWidth=Mathf.Max(width-70*u,
            (chart=="melee"?900:chart=="missile"?830:chart=="terrain"?900:620)*u);
        float contentHeight=(chart=="melee"?540:chart=="missile"?820:
            chart=="terrain"?1120:720)*u;
        var viewport=new Rect(25*u,144*u,width-50*u,height-165*u);
        chartScroll=GUI.BeginScrollView(viewport,chartScroll,new Rect(0,0,contentWidth,contentHeight));
        switch(chart)
        {
            case "melee":DrawMeleeChart(contentWidth);break;
            case "missile":DrawMissileChart(contentWidth);break;
            case "morale":DrawMoraleChart(contentWidth);break;
            case "terrain":DrawTerrainChart(contentWidth);break;
        }
        GUI.EndScrollView();
        GUI.EndGroup();
    }
    private static string UpperFirst(string value)
    {return char.ToUpperInvariant(value[0])+value.Substring(1);}
    private float ChartTable(float y,float width,string title,string rowHeading,string[] columns,
        int rows,Func<int,string> rowName,Func<int,int,string> value)
    {
        float u=chartScale;
        GUI.Label(new Rect(0,y,width,32*u),title,chartSection);
        y+=38*u;
        float firstWidth=92*u,rowHeight=39*u,cellWidth=(width-firstWidth)/columns.Length;
        Fill(new Rect(0,y,width,rowHeight),new Color(.55f,.23f,.17f));
        GUI.Label(new Rect(0,y,firstWidth,rowHeight),rowHeading,chartHeader);
        for(int col=0;col<columns.Length;col++)
            GUI.Label(new Rect(firstWidth+col*cellWidth,y,cellWidth,rowHeight),columns[col],chartHeader);
        y+=rowHeight;
        for(int row=0;row<rows;row++)
        {
            Fill(new Rect(0,y,width,rowHeight),row%2==0?
                new Color(.99f,.975f,.93f):new Color(.92f,.89f,.81f));
            GUI.Label(new Rect(0,y,firstWidth,rowHeight),rowName(row),chartRowHeader);
            for(int col=0;col<columns.Length;col++)
            {
                string result=value(row,col);
                var cell=new Rect(firstWidth+col*cellWidth,y,cellWidth,rowHeight);
                if(result=="R")Fill(cell,new Color(.91f,.69f,.65f));
                else if(result=="D")Fill(cell,new Color(.94f,.82f,.57f));
                else if(result=="M")Fill(cell,new Color(.72f,.85f,.82f));
                GUI.Label(cell,result,result=="-"||result=="—"?chartMuted:chartCell);
            }
            y+=rowHeight;
        }
        return y+23*u;
    }
    private float ChartNote(float y,float width,string text)
    {
        float u=chartScale;
        float noteHeight=chartNote.CalcHeight(new GUIContent(text),width-28*u)+20*u;
        Fill(new Rect(0,y,width,noteHeight),new Color(.88f,.83f,.71f));
        GUI.Label(new Rect(14*u,y+10*u,width-28*u,noteHeight-20*u),text,chartNote);
        return y+noteHeight+16*u;
    }
    private void DrawMeleeChart(float width)
    {
        string[] differentials={"−6","−5","−4","−3","−2","−1","0","+1","+2–3","+4–5","≥+6"};
        float y=ChartTable(0,width,"MELEE COMBAT RESULTS","D6",differentials,6,
            row=>(row+1).ToString(),(row,col)=>RuleTables.Melee[row,col]);
        y=ChartNote(y,width,"Read each result as attacker / defender. Compare attack and defense to find the differential, then roll one die. Below −6, the result is 1/−; above +6, use the ≥+6 column.");
        ChartNote(y,width,"RESULT KEY   −  No effect     M  Morale check     D  Disrupted     1  Reduced or eliminated. A disrupted unit routs if it was charging or under Attack & Pursue orders.");
    }
    private void DrawMissileChart(float width)
    {
        float y=ChartTable(0,width,"MISSILE SUPPLY BY ASSAULT","PERIOD",
            new[]{"Norman bows","Saxon javelins"},2,
            row=>row==0?"First":"Second",
            (row,col)=>row==0?(col==0?"6":"4"):(col==0?"3":"2"));
        y=ChartTable(y,width,"WEAPON STRENGTH BY RANGE","WEAPON",
            new[]{"1 hex","2 hexes","3 hexes"},3,
            row=>new[]{"Bow","Javelin","Sling"}[row],
            (row,col)=>{
                int strength=RuleTables.MissileStrength(new[]{"B","J","S"}[row],col+1);
                return strength==0?"—":strength.ToString();
            });
        string[] odds={"1:4","1:3","1:2","1:1.5","1:1","1.5:1","2:1","3:1","4:1","5:1"};
        y=ChartTable(y,width,"MISSILE COMBAT RESULTS","D6",odds,6,
            row=>(row+1).ToString(),(row,col)=>RuleTables.Missile[row,col]);
        ChartNote(y,width,"Compare total missile strength with target defense and round down to a listed ratio. Below 1:4 has no effect. Results: − no effect, M morale check, D disrupted, 1 reduced or eliminated.");
    }
    private void DrawMoraleChart(float width)
    {
        float y=ChartTable(0,width,"RALLY CHECK","RATING",new[]{"Successful die roll"},5,
            row=>((char)('A'+row)).ToString(),
            (row,col)=>{
                int max=Enumerable.Range(1,6).Count(die=>RuleTables.Rally((char)('A'+row),die));
                return max==1?"1":"1–"+max;
            });
        y=ChartTable(y,width,"MORALE CHECK","D6",new[]{"A","B","C","D","E"},6,
            row=>(row+1).ToString(),
            (row,col)=>{
                var result=RuleTables.Morale((char)('A'+col),row+1);
                return result==Status.Ready?"—":result==Status.Disrupted?"D":"R";
            });
        ChartNote(y,width,"RESULT KEY   —  No effect     D  Disrupted     R  Routed. A successful rally removes disruption. A routed unit within a friendly leader's rally range automatically loses its rout marker during rally.");
    }
    private float TerrainRow(float y,float width,int index,string terrain,string swatch,
        string movement,string combat,string other,Color accent)
    {
        float u=chartScale,h=80*u;
        float terrainWidth=225*u,movementWidth=170*u,combatWidth=305*u;
        Fill(new Rect(0,y,width,h),index%2==0?
            new Color(.99f,.975f,.93f):new Color(.93f,.90f,.83f));
        Fill(new Rect(0,y,7*u,h),accent);
        foreach(float x in new[]{terrainWidth,terrainWidth+movementWidth,
                 terrainWidth+movementWidth+combatWidth})
            Fill(new Rect(x,y,1*u,h),new Color(.80f,.74f,.64f));
        Texture2D sample;
        if(terrainSwatches.TryGetValue(swatch,out sample) && sample!=null)
            GUI.DrawTexture(new Rect(16*u,y+9*u,72*u,62*u),sample,ScaleMode.ScaleToFit,true);
        GUI.Label(new Rect(94*u,y+8*u,terrainWidth-101*u,h-16*u),terrain,chartRowHeader);
        GUI.Label(new Rect(terrainWidth+9*u,y+8*u,movementWidth-18*u,h-16*u),movement,chartCell);
        GUI.Label(new Rect(terrainWidth+movementWidth+12*u,y+8*u,
            combatWidth-24*u,h-16*u),combat,chartNote);
        GUI.Label(new Rect(terrainWidth+movementWidth+combatWidth+12*u,y+8*u,
            width-terrainWidth-movementWidth-combatWidth-24*u,h-16*u),other,chartNote);
        return y+h;
    }
    private void DrawTerrainChart(float width)
    {
        float u=chartScale;
        GUI.Label(new Rect(0,0,width,36*u),"TERRAIN EFFECTS",chartSection);
        float y=42*u,terrainWidth=225*u,movementWidth=170*u,combatWidth=305*u;
        Fill(new Rect(0,y,width,46*u),new Color(.55f,.23f,.17f));
        GUI.Label(new Rect(0,y,terrainWidth,46*u),"TERRAIN",chartHeader);
        GUI.Label(new Rect(terrainWidth,y,movementWidth,46*u),"MOVEMENT",chartHeader);
        GUI.Label(new Rect(terrainWidth+movementWidth,y,combatWidth,46*u),"COMBAT",chartHeader);
        GUI.Label(new Rect(terrainWidth+movementWidth+combatWidth,y,
            width-terrainWidth-movementWidth-combatWidth,46*u),"OTHER EFFECT",chartHeader);
        y+=46*u;
        y=TerrainRow(y,width,0,"Clear","clear","1 MP","None","Normal terrain",
            new Color(.66f,.77f,.48f));
        y=TerrainRow(y,width,1,"Ridge uphill","ridge","No extra MP",
            "Foot −1 melee; knight −2 melee","Knight morale check on crossing",
            new Color(.47f,.43f,.30f));
        y=TerrainRow(y,width,2,"Ridge downhill","ridge","No extra MP",
            "Foot +1 melee; knight: no ridge modifier","Knight morale check on crossing",
            new Color(.63f,.53f,.35f));
        y=TerrainRow(y,width,3,"Moving downhill","ridge","No extra MP",
            "Foot: none; knight +1 melee","Downhill charge adds +2 more",
            new Color(.78f,.63f,.38f));
        y=TerrainRow(y,width,4,"Marsh","marsh","Foot 2 MP; knight 3 MP",
            "Defender −1","Knight morale check on entry",
            new Color(.33f,.59f,.55f));
        y=TerrainRow(y,width,5,"Stream","stream","+1 MP to cross","None",
            "Charges cannot cross streams",new Color(.18f,.57f,.68f));
        y=TerrainRow(y,width,6,"Woods","woods","Foot 2 MP; knight 3 MP",
            "Defender +2 in melee; +1 vs bow fire",
            "Fire into woods is allowed; fire through is blocked",
            new Color(.29f,.54f,.36f));
        y=TerrainRow(y,width,7,"Road","road","1 MP","None","Road control affects victory",
            new Color(.57f,.47f,.31f));
        y+=18*u;
        y=ChartNote(y,width,"KNIGHT MORALE   Crossing a ridge or entering marsh triggers a morale check. Treat a rout result as disruption. Ridge checks occur before crossing.");
        y=ChartNote(y,width,"CHARGE LIMITS   A charge cannot cross a ridge or stream, enter woods or marsh, or move uphill during its last two hexes. Reaction ignores terrain movement costs.");
        ChartNote(y,width,"FIRE & ELEVATION   Woods can be fired into, but not through. Higher intervening terrain also blocks fire. High trajectory does not bypass either obstruction.");
    }
}
