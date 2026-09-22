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
    private Texture2D map, titleBackground;
    private readonly Dictionary<string,Texture2D> counters=new Dictionary<string,Texture2D>();
    private readonly List<string> selected=new List<string>();
    private readonly List<string> selectedTargets=new List<string>();
    private Vector2 pan;
    private float scale, lastMapWidth, lastMapHeight;
    private float panelScale=1f, chartScale=1f;
    private bool showMenu=true, showHigh, showHelp, fullMapMode;
    private string saveSlot="Game 1", notice="", chart="", menuPage="main";
    private Vector2 panelScroll, chartScroll, menuScroll;
    private GUIStyle small, hexNumber, hexNumberShadow,
        menuTitle, menuSubtitle, menuButton, menuPrimary, menuTextField,
        panelTitle, panelStatus, panelSection, panelBody, panelMuted, panelValue,
        panelCard, panelButton, panelPrimary, panelLink,
        chartTitle, chartSection, chartHeader, chartRowHeader, chartCell, chartMuted, chartNote,
        chartTab, chartTabSelected, chartClose;
    private string hoveredHex="";

    private void Awake()
    {
        var asset=Resources.Load<TextAsset>("Data/Map");
        if(asset==null){Debug.LogError("Map.json is missing");return;}
        board=new Board(JsonUtility.FromJson<MapData>(asset.text));
        map=Resources.Load<Texture2D>("Art/Map/hex_map");
        titleBackground=Resources.Load<Texture2D>("Art/Menu/title_tapestry");
    }
    private void Update()
    {
        if(game==null||showMenu||chart!=""||!Application.isFocused)return;
        var viewDirection=new Vector2(
            (Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),
            (Input.GetKey(KeyCode.S)?1:0)-(Input.GetKey(KeyCode.W)?1:0));
        if(viewDirection.sqrMagnitude==0)return;
        fullMapMode=false;
        pan-=viewDirection.normalized*Mathf.Max(360f,Screen.height*.65f)*Time.unscaledDeltaTime;
    }
    private static float PanelWidth() { return Mathf.Clamp(Screen.width*.24f,500f,900f); }
    private static float PanelUiScale() { return Mathf.Clamp(Screen.height/900f,1.2f,1.8f); }
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
            panelPrimary=ButtonStyle(18,new Color(.60f,.21f,.16f),new Color(.73f,.27f,.20f),
                new Color(.99f,.97f,.90f));
            panelPrimary.fontStyle=FontStyle.Bold;
            panelLink=ButtonStyle(14,new Color(.92f,.88f,.78f),new Color(.83f,.76f,.62f),
                new Color(.27f,.19f,.14f));
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
        }
        panelScale=PanelUiScale();
        UpdatePanelStyles();
        float menuScale=Mathf.Clamp(Screen.height/900f,1.1f,1.8f);
        menuTitle.fontSize=Mathf.RoundToInt(52*menuScale);
        menuSubtitle.fontSize=Mathf.RoundToInt(22*menuScale);
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
        Rect mapRect=new Rect(0,0,Mathf.Max(100,Screen.width-PanelWidth()),Screen.height);
        ResizeMapView(mapRect);
        HandleInput(mapRect);
        pan=BoardViewMath.ClampPan(pan,scale,mapRect.width,mapRect.height,
            board.data.width,board.data.height);
        DrawMap(mapRect);
        DrawPanel(new Rect(mapRect.xMax,0,PanelWidth(),Screen.height));
        if(showMenu)DrawMenu();
        if(chart!="")DrawChart();
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
        panelPrimary.fontSize=Mathf.RoundToInt(18*p);
        panelLink.fontSize=Mathf.RoundToInt(14*p);
        int side=Mathf.RoundToInt(15*p),top=Mathf.RoundToInt(13*p);
        panelCard.padding=new RectOffset(side,side,top,Mathf.RoundToInt(15*p));
        panelCard.margin=new RectOffset(0,0,0,Mathf.RoundToInt(12*p));
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
            if(e.keyCode==KeyCode.Escape)
            {if(chart!="")chart="";else if(showMenu && menuPage!="main")menuPage="main";
                else showMenu=!showMenu;e.Use();}
            else if(e.keyCode==KeyCode.Space && !showMenu){Advance();e.Use();}
            else if((e.keyCode==KeyCode.Q||e.keyCode==KeyCode.E) && game!=null && selected.Count==1)
            {
                var unit=SelectedUnits().FirstOrDefault();
                if(unit!=null)game.Face(unit,unit.facing+(e.keyCode==KeyCode.E?1:-1));e.Use();
            }
        }
        if(showMenu||chart!=""||!region.Contains(e.mousePosition))return;
        if(e.type==EventType.ScrollWheel)
        {
            fullMapMode=false;
            float old=scale;scale=Mathf.Clamp(scale*(e.delta.y>0?.88f:1.12f),.12f,4f);
            pan=e.mousePosition-(e.mousePosition-pan)*(scale/old);e.Use();
        }
        else if(e.type==EventType.MouseDrag && (e.button==1||e.button==2))
        {fullMapMode=false;pan+=e.delta;e.Use();}
        else if(e.type==EventType.MouseDown && e.button==0 && game!=null)
        {
            float x=(e.mousePosition.x-pan.x)/scale;
            float y=(e.mousePosition.y-pan.y)/scale;
            string hex=board.Nearest(x,y);
            if(hex!=null)ClickHex(hex,e.shift,e.alt);e.Use();
        }
    }
    private void ClickHex(string hex,bool add,bool preferLeader)
    {
        if(game==null)return;
        var friendly=game.UnitAt(hex,Side.Norman);
        var leader=game.UnitAt(hex,Side.Norman,true);
        var enemy=game.UnitAt(hex,Side.Saxon);
        var enemyLeader=game.UnitAt(hex,Side.Saxon,true);
        var selection=SelectedUnits();
        if(!preferLeader && game.state.phase==Phase.NormanMove && selection.Count==1 && game.Move(selection[0],hex))return;
        if(!preferLeader && game.state.phase==Phase.NormanReaction && selection.Count==1 && game.Move(selection[0],hex,true))return;
        if(!preferLeader && game.state.phase==Phase.Reform && selection.Count==1 && game.ReformMove(selection[0],hex))return;
        if((enemy!=null||enemyLeader!=null) && selection.Count>0)
        {
            if(game.state.phase==Phase.NormanFire || game.state.phase==Phase.NormanDefenseFire)
            {
                if(!game.Fire(selection,enemy??enemyLeader,showHigh))notice="Illegal fire target or missile supply exhausted.";
                return;
            }
            if(game.state.phase==Phase.NormanMelee && enemy!=null)
            {
                if(add){if(!selectedTargets.Contains(enemy.id))selectedTargets.Add(enemy.id);return;}
                if(!game.Melee(selection,new List<UnitState>{enemy}))notice="Illegal melee group or target.";
                return;
            }
        }
        if(friendly!=null || leader!=null)
        {
            var unit=preferLeader?(leader??friendly):(friendly??leader);
            if(add)
            {if(!selected.Contains(unit.id))selected.Add(unit.id);}
            else{selected.Clear();selected.Add(unit.id);}
        }
        else if(!add)selected.Clear();
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
        GUI.DrawTexture(new Rect(pan.x,pan.y,board.data.width*scale,board.data.height*scale),map,ScaleMode.StretchToFill);
        if(game!=null)
        {
            var unit=SelectedUnits().FirstOrDefault();
            if(unit!=null && (game.state.phase==Phase.NormanMove || game.state.phase==Phase.NormanReaction))
            {
                bool reaction=game.state.phase==Phase.NormanReaction;
                foreach(var move in game.LegalMoves(unit,reaction).Values)
                {
                    var h=board.Hex(move.destination);
                    float size=74*scale;
                    GUI.color=move.charge?new Color(1,.6f,.1f,.45f):new Color(.1f,1,.25f,.42f);
                    GUI.DrawTexture(new Rect(pan.x+h.x*scale-size/2,pan.y+h.y*scale-size/2,size,size),Texture2D.whiteTexture);
                }
                GUI.color=Color.white;
            }
            foreach(var u in game.state.units.Where(u=>u.status!=Status.Eliminated && board.Has(u.hex)))
            {
                var h=board.Hex(u.hex);var type=UnitTypes.Get(u);
                float size=(type.leader?45:65)*scale;
                float xx=pan.x+h.x*scale-size/2+(type.leader?26*scale:0);
                float yy=pan.y+h.y*scale-size/2+(type.leader?25*scale:0);
                var rect=new Rect(xx,yy,size,size);
                var texture=CounterTexture(u);
                if(texture!=null)
                {
                    var old=GUI.matrix;
                    if(!type.leader)GUIUtility.RotateAroundPivot((u.facing-1)*60,rect.center);
                    GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill);
                    GUI.matrix=old;
                }
                if(selected.Contains(u.id))
                {GUI.color=Color.yellow;GUI.Box(new Rect(xx-2,yy-2,size+4,size+4),GUIContent.none);GUI.color=Color.white;}
                if(u.status==Status.Disrupted||u.status==Status.Routed)
                {
                    GUI.color=u.status==Status.Routed?Color.red:Color.yellow;
                    GUI.Label(new Rect(xx+size-10,yy-5,22,20),u.status==Status.Routed?"R":"D");GUI.color=Color.white;
                }
            }
            DrawHexNumbers(region);
            var e=Event.current;
            if(region.Contains(e.mousePosition))
            {
                hoveredHex=board.Nearest((e.mousePosition.x-pan.x)/scale,(e.mousePosition.y-pan.y)/scale);
                if(hoveredHex!=null)
                {
                    var h=board.Hex(hoveredHex);
                    string label=hoveredHex+"  Level "+h.level+
                        (h.road?"  Road":"")+(h.woods?"  Woods":"")+(h.marsh?"  Marsh":"");
                    GUI.Box(new Rect(Mathf.Max(4,e.mousePosition.x-75),Mathf.Max(4,e.mousePosition.y-25),205,24),label);
                }
            }
        }
        GUI.EndGroup();
    }
    private void DrawHexNumbers(Rect region)
    {
        int fontSize=Mathf.Max(1,Mathf.RoundToInt(16*scale));
        hexNumber.fontSize=fontSize;
        hexNumberShadow.fontSize=fontSize;
        // The printed board places each four-digit coordinate vertically at the right of its hex.
        // Leave enough unrotated width for all four digits before rotating the label.
        float width=54.4f*scale,height=20*scale;
        float margin=Mathf.Max(60,50*scale);
        foreach(var h in board.data.hexes)
        {
            float x=pan.x+h.x*scale,y=pan.y+h.y*scale;
            if(x<-margin||x>region.width+margin||y<-margin||y>region.height+margin)continue;
            var pivot=new Vector2(x+36*scale,y);
            var label=new Rect(pivot.x-width*.5f,pivot.y-height*.5f,width,height);
            var old=GUI.matrix;
            GUIUtility.RotateAroundPivot(90,pivot);
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
        GUILayout.Label(s.phase==Phase.GameOver?s.result:
            $"ASSAULT {s.period}   ·   TURN {s.turn}",panelStatus,GUILayout.Height(28*p));
        GUILayout.Space(8*p);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Menu",panelButton,GUILayout.Height(42*p)))showMenu=true;
        if(GUILayout.Button("Focus battle",panelButton,GUILayout.Height(42*p)))
        {fullMapMode=false;lastMapWidth=0;scale=0;}
        if(GUILayout.Button("Fit map",panelButton,GUILayout.Height(42*p)))
        {fullMapMode=true;lastMapWidth=0;}
        GUILayout.EndHorizontal();
        GUILayout.Space(15*p);
        panelScroll=GUILayout.BeginScrollView(panelScroll);
        GUILayout.BeginVertical(panelCard);
        GUILayout.Label("YOUR NEXT ACTION",panelSection);
        GUILayout.Label(PhaseLabel(s.phase),panelValue);
        GUILayout.Label(PhasePrompt(s.phase),panelMuted);
        if(s.phase==Phase.Orders)
        {
            GUILayout.Space(5*p);
            foreach(var id in new[]{"Breton","Norman","Franco-Flemish"})
            {
                var group=s.groups.First(g=>g.id==id);
                if(GUILayout.Button(id+"  ·  "+group.strategy,panelButton,GUILayout.Height(40*p)))
                    game.SetStrategy(id,(Strategy)(((int)group.strategy+1)%4));
            }
        }
        if(game.OptionsPending())
        {
            GUILayout.Space(6*p);
            GUILayout.Label("OPTIONAL ORDERS",panelSection);
            foreach(var g in s.groups.Where(g=>g.footOptional||g.knightOptional))
            {
                if(g.footOptional)
                {
                    GUILayout.Label(g.id+" foot",panelMuted);GUILayout.BeginHorizontal();
                    if(GUILayout.Button("Wall",panelButton))game.SetOptionalOrder(g.id,false,Order.ShieldWall);
                    if(GUILayout.Button("Fire",panelButton))game.SetOptionalOrder(g.id,false,Order.FireInPlace);
                    if(GUILayout.Button("Advance",panelButton))game.SetOptionalOrder(g.id,false,Order.Advance);
                    GUILayout.EndHorizontal();
                }
                if(g.knightOptional)
                {
                    GUILayout.Label(g.id+" knights",panelMuted);GUILayout.BeginHorizontal();
                    if(GUILayout.Button("Hold",panelButton))game.SetOptionalOrder(g.id,true,Order.Hold);
                    if(GUILayout.Button("Advance",panelButton))game.SetOptionalOrder(g.id,true,Order.Advance);
                    if(GUILayout.Button("Charge",panelButton))game.SetOptionalOrder(g.id,true,Order.Charge);
                    GUILayout.EndHorizontal();
                }
            }
        }
        if(s.phase==Phase.NormanFire||s.phase==Phase.NormanDefenseFire)
            showHigh=GUILayout.Toggle(showHigh,"High trajectory bow fire (period II)");
        if(notice!="")GUILayout.Label(notice,panelBody);
        if(s.phase!=Phase.GameOver)
        {
            string caption=s.phase==Phase.Setup?"Begin battle":s.phase==Phase.Orders?"Roll orders":
                s.phase==Phase.Reform?"Finish reform":"Finish segment  ·  Space";
            GUILayout.Space(9*p);
            if(GUILayout.Button(caption,panelPrimary,GUILayout.Height(54*p)))Advance();
        }
        GUILayout.EndVertical();
        var units=SelectedUnits();
        if(units.Count>0 || selectedTargets.Count>0)
        {
            GUILayout.BeginVertical(panelCard);
            GUILayout.Label("SELECTED UNITS",panelSection);
            if(units.Count>0)GUILayout.Label(string.Join(", ",units.Select(u=>u.id).ToArray()),panelBody);
            foreach(var u in units.Take(3))
            {
                var t=UnitTypes.Get(u);
                GUILayout.Label($"{t.art}: {u.hex} · {u.status} · {(u.reduced?"Reduced":"Full")} · {game.OrderFor(u)}",panelMuted);
            }
            if(units.Count==1 && game.CanFace(units[0]) && !UnitTypes.Get(units[0]).leader)
            {
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Turn left  ·  Q",panelButton))game.Face(units[0],units[0].facing-1);
                if(GUILayout.Button("Turn right  ·  E",panelButton))game.Face(units[0],units[0].facing+1);
                GUILayout.EndHorizontal();
            }
            if(s.phase==Phase.NormanMelee && selectedTargets.Count>0)
            {
                GUILayout.Label("Targets: "+string.Join(", ",selectedTargets.ToArray()),panelMuted);
                if(GUILayout.Button("Resolve selected melee",panelPrimary,GUILayout.Height(45*p)))
                {
                    var targets=s.units.Where(u=>selectedTargets.Contains(u.id)).ToList();
                    if(!game.Melee(units,targets))notice="Illegal melee group or targets.";
                    else selectedTargets.Clear();
                }
                if(GUILayout.Button("Clear targets",panelLink))selectedTargets.Clear();
            }
            GUILayout.EndVertical();
        }
        GUILayout.BeginVertical(panelCard);
        GUILayout.Label("REFERENCE",panelSection);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Melee",panelButton,GUILayout.Height(42*p)))OpenChart("melee");
        if(GUILayout.Button("Missile",panelButton,GUILayout.Height(42*p)))OpenChart("missile");
        if(GUILayout.Button("Morale",panelButton,GUILayout.Height(42*p)))OpenChart("morale");
        GUILayout.EndHorizontal();
        GUILayout.Space(5*p);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Rulebook PDF",panelLink,GUILayout.Height(38*p)))
            Application.OpenURL(new Uri(Path.Combine(Application.streamingAssetsPath,"Hastings_1066.pdf")).AbsoluteUri);
        if(GUILayout.Button(showHelp?"Hide controls":"Controls",panelLink,GUILayout.Height(38*p)))showHelp=!showHelp;
        GUILayout.EndHorizontal();
        if(showHelp)GUILayout.Label("Select a Norman counter. Alt-click a stacked leader; Shift-click to add units or melee targets. Click a highlighted hex to move or an enemy to attack. WASD or right drag pans; the wheel zooms. Q/E changes facing. Space ends a segment.",panelMuted);
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
            GUILayout.Label("•  "+line,panelMuted);
            GUILayout.Space(4);
        }
        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
    private static string PhaseLabel(Phase phase)
    {
        switch(phase)
        {
            case Phase.Setup:return "Setup";
            case Phase.Orders:return "Choose orders";
            case Phase.NormanMove:return "Norman movement";
            case Phase.NormanFire:return "Norman missile fire";
            case Phase.NormanDefenseFire:return "Norman defensive fire";
            case Phase.NormanMelee:return "Norman melee";
            case Phase.Reform:return "Reform";
            case Phase.GameOver:return "Battle ended";
            default:return UpperFirst(phase.ToString());
        }
    }
    private static string PhasePrompt(Phase phase)
    {
        switch(phase)
        {
            case Phase.Setup:return "Set Norman facings on the map, then begin the battle.";
            case Phase.Orders:return "Choose a strategy for each Norman contingent.";
            case Phase.NormanMove:return "Select a Norman unit and click a highlighted destination.";
            case Phase.NormanFire:
            case Phase.NormanDefenseFire:return "Select missile units, then click a legal Saxon target.";
            case Phase.NormanMelee:return "Select attackers, then click a Saxon defender.";
            case Phase.Reform:return "Move each Norman unit to a legal reform hex.";
            case Phase.GameOver:return "The battle is over.";
            default:return "Resolve any available actions, then finish this segment.";
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
        notice="";
        switch(game.state.phase)
        {
            case Phase.Setup:game.Begin();break;
            case Phase.Orders:game.ResolveOrders();break;
            case Phase.Reform:if(!game.FinishReform())notice="Move every Norman unit to a legal reform hex first.";break;
            default:game.Advance();break;
        }
        selected.Clear();
        selectedTargets.Clear();
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
            menuPage=="load"?"Load a game":menuPage=="save"?"Save your battle":"Start a new battle?",
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
            {if(game==null)StartNewGame();else menuPage="newConfirm";}
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
            if(GUILayout.Button("Start New Game",menuPrimary,GUILayout.Height(buttonHeight)))StartNewGame();
            GUILayout.Space(12);
            if(GUILayout.Button("Cancel",menuButton,GUILayout.Height(buttonHeight)))menuPage="main";
        }
        if(notice!="")GUILayout.Label(notice,small);
        GUILayout.EndArea();
    }
    private void StartNewGame()
    {
        game=new GameEngine(board,Setup.New(board,(uint)DateTime.UtcNow.Ticks));
        selected.Clear();selectedTargets.Clear();showMenu=false;menuPage="main";notice="";
        lastMapWidth=0;scale=0;fullMapMode=false;
    }
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
        string[] names={"melee","missile","morale"};
        for(int i=0;i<names.Length;i++)
        {
            float tabWidth=(width-52*u)/3f;
            var tab=new Rect(26*u+i*tabWidth,91*u,tabWidth-5*u,37*u);
            if(GUI.Button(tab,UpperFirst(names[i]),chart==names[i]?chartTabSelected:chartTab))
                OpenChart(names[i]);
        }
        float contentWidth=Mathf.Max(width-70*u,(chart=="melee"?900:chart=="missile"?830:620)*u);
        float contentHeight=(chart=="melee"?540:chart=="missile"?820:720)*u;
        var viewport=new Rect(25*u,144*u,width-50*u,height-165*u);
        chartScroll=GUI.BeginScrollView(viewport,chartScroll,new Rect(0,0,contentWidth,contentHeight));
        switch(chart)
        {
            case "melee":DrawMeleeChart(contentWidth);break;
            case "missile":DrawMissileChart(contentWidth);break;
            case "morale":DrawMoraleChart(contentWidth);break;
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
}
