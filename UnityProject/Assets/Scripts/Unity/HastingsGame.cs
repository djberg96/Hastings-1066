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
    private bool showMenu=true, showHigh, showHelp, fullMapMode;
    private string saveSlot="Game 1", notice="", chart="", menuPage="main";
    private Vector2 logScroll, chartScroll, menuScroll;
    private GUIStyle heading, small, hexNumber, hexNumberShadow,
        menuTitle, menuSubtitle, menuButton, menuTextField;
    private string hoveredHex="";

    private void Awake()
    {
        var asset=Resources.Load<TextAsset>("Data/Map");
        if(asset==null){Debug.LogError("Map.json is missing");return;}
        board=new Board(JsonUtility.FromJson<MapData>(asset.text));
        map=Resources.Load<Texture2D>("Art/Map/hex_map");
        titleBackground=Resources.Load<Texture2D>("Art/Menu/title_tapestry");
    }
    private static float PanelWidth() { return Mathf.Clamp(Screen.width*.21f,390f,600f); }
    private void OnGUI()
    {
        if(board==null||map==null){GUI.Label(new Rect(20,20,700,40),"Hastings assets are missing. Run Tools/generate_assets.py.");return;}
        int body=Mathf.Clamp(Mathf.RoundToInt(Screen.height/65f),17,23);
        GUI.skin.label.fontSize=body;
        GUI.skin.button.fontSize=body;
        GUI.skin.button.padding=new RectOffset(10,10,8,8);
        GUI.skin.toggle.fontSize=body;
        GUI.skin.textField.fontSize=body;
        if(heading==null)
        {
            heading=new GUIStyle(GUI.skin.label){fontSize=body+8,fontStyle=FontStyle.Bold,wordWrap=true};
            small=new GUIStyle(GUI.skin.label){fontSize=body-3,wordWrap=true};
            hexNumber=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,
                fontStyle=FontStyle.Bold,clipping=TextClipping.Overflow};
            hexNumber.normal.textColor=new Color(.20f,.13f,.09f,.95f);
            hexNumberShadow=new GUIStyle(hexNumber);
            hexNumberShadow.normal.textColor=new Color(.99f,.95f,.80f,.95f);
            menuTitle=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(Mathf.RoundToInt(Screen.height*.055f),42,76),
                fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            menuSubtitle=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(Mathf.RoundToInt(Screen.height*.024f),21,34),
                alignment=TextAnchor.MiddleCenter};
            menuTitle.normal.textColor=new Color(.25f,.12f,.08f);
            menuSubtitle.normal.textColor=new Color(.35f,.21f,.13f);
            menuButton=new GUIStyle(GUI.skin.button){fontSize=Mathf.Clamp(Mathf.RoundToInt(Screen.height*.026f),23,37),
                fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,
                padding=new RectOffset(18,18,10,10)};
            menuButton.normal.background=SolidTexture(new Color(.27f,.12f,.09f));
            menuButton.hover.background=SolidTexture(new Color(.43f,.20f,.13f));
            menuButton.active.background=SolidTexture(new Color(.17f,.08f,.06f));
            menuButton.normal.textColor=new Color(.99f,.93f,.77f);
            menuButton.hover.textColor=Color.white;
            menuButton.active.textColor=Color.white;
            menuTextField=new GUIStyle(GUI.skin.textField){fontSize=menuButton.fontSize,
                alignment=TextAnchor.MiddleCenter};
        }
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
            {if(showMenu && menuPage!="main")menuPage="main";else showMenu=!showMenu;e.Use();}
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
        int fontSize=Mathf.Clamp(Mathf.RoundToInt(24*scale),12,24);
        hexNumber.fontSize=fontSize;
        hexNumberShadow.fontSize=fontSize;
        float width=Mathf.Max(34,53*scale),height=fontSize+3;
        foreach(var h in board.data.hexes)
        {
            float x=pan.x+h.x*scale,y=pan.y+h.y*scale;
            if(x<-60||x>region.width+60||y<-60||y>region.height+60)continue;
            var label=new Rect(x-width/2,y-32.5f*scale-height-2,width,height);
            GUI.Label(new Rect(label.x+1,label.y+1,label.width,label.height),h.id,hexNumberShadow);
            GUI.Label(label,h.id,hexNumber);
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
        GUI.color=new Color(.13f,.17f,.18f);
        GUI.DrawTexture(region,Texture2D.whiteTexture);
        GUI.color=Color.white;
        GUILayout.BeginArea(new Rect(region.x+14,14,region.width-28,region.height-28));
        GUILayout.Label("HASTINGS 1066",heading);
        if(game==null){GUILayout.Label("Choose New Game or Load Game from the menu.");
            if(GUILayout.Button("Menu"))showMenu=true;GUILayout.EndArea();return;}
        var s=game.state;
        GUILayout.Label(s.phase==Phase.GameOver?s.result:$"Assault {s.period} · Turn {s.turn} · {s.phase}",heading);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Menu"))showMenu=true;
        if(GUILayout.Button("Battle view"))
        {fullMapMode=false;lastMapWidth=0;scale=0;}
        if(GUILayout.Button("Full map"))
        {fullMapMode=true;lastMapWidth=0;}
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
        if(s.phase==Phase.Orders)
        {
            GUILayout.Label("Choose a strategy for each Norman nationality:");
            foreach(var id in new[]{"Breton","Norman","Franco-Flemish"})
            {
                var group=s.groups.First(g=>g.id==id);
                if(GUILayout.Button(id+": "+group.strategy))
                    game.SetStrategy(id,(Strategy)(((int)group.strategy+1)%4));
            }
        }
        if(game.OptionsPending())
        {
            GUILayout.Label("Choose orders for Optional results:");
            foreach(var g in s.groups.Where(g=>g.footOptional||g.knightOptional))
            {
                if(g.footOptional)
                {
                    GUILayout.Label(g.id+" foot");GUILayout.BeginHorizontal();
                    if(GUILayout.Button("Wall"))game.SetOptionalOrder(g.id,false,Order.ShieldWall);
                    if(GUILayout.Button("Fire"))game.SetOptionalOrder(g.id,false,Order.FireInPlace);
                    if(GUILayout.Button("Advance"))game.SetOptionalOrder(g.id,false,Order.Advance);
                    GUILayout.EndHorizontal();
                }
                if(g.knightOptional)
                {
                    GUILayout.Label(g.id+" knights");GUILayout.BeginHorizontal();
                    if(GUILayout.Button("Hold"))game.SetOptionalOrder(g.id,true,Order.Hold);
                    if(GUILayout.Button("Advance"))game.SetOptionalOrder(g.id,true,Order.Advance);
                    if(GUILayout.Button("Charge"))game.SetOptionalOrder(g.id,true,Order.Charge);
                    GUILayout.EndHorizontal();
                }
            }
        }
        if(s.phase==Phase.NormanFire||s.phase==Phase.NormanDefenseFire)
            showHigh=GUILayout.Toggle(showHigh,"High trajectory bow fire (period II)");
        var units=SelectedUnits();
        if(units.Count>0)
        {
            GUILayout.Space(6);GUILayout.Label("Selected: "+string.Join(", ",units.Select(u=>u.id).ToArray()));
            foreach(var u in units.Take(3))
            {
                var t=UnitTypes.Get(u);
                GUILayout.Label($"{t.art}: {u.hex} · {u.status} · {(u.reduced?"Reduced":"Full")} · {game.OrderFor(u)}",small);
            }
            if(units.Count==1 && game.CanFace(units[0]) && !UnitTypes.Get(units[0]).leader)
            {
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Face left (Q)"))game.Face(units[0],units[0].facing-1);
                if(GUILayout.Button("Face right (E)"))game.Face(units[0],units[0].facing+1);
                GUILayout.EndHorizontal();
            }
        }
        if(s.phase==Phase.NormanMelee && selectedTargets.Count>0)
        {
            GUILayout.Label("Targets: "+string.Join(", ",selectedTargets.ToArray()));
            if(GUILayout.Button("Resolve selected melee"))
            {
                var targets=s.units.Where(u=>selectedTargets.Contains(u.id)).ToList();
                if(!game.Melee(units,targets))notice="Illegal melee group or targets.";
                else selectedTargets.Clear();
            }
            if(GUILayout.Button("Clear targets"))selectedTargets.Clear();
        }
        if(notice!="")GUILayout.Label(notice,small);
        GUILayout.Space(8);
        if(s.phase!=Phase.GameOver)
        {
            string caption=s.phase==Phase.Setup?"Begin Battle":s.phase==Phase.Orders?"Roll Orders":
                s.phase==Phase.Reform?"Finish Reform":"Finish Segment (Space)";
            if(GUILayout.Button(caption,GUILayout.Height(34)))Advance();
        }
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Melee chart"))chart="melee";
        if(GUILayout.Button("Missile chart"))chart="missile";
        if(GUILayout.Button("Morale chart"))chart="morale";
        GUILayout.EndHorizontal();
        if(GUILayout.Button("Open rulebook PDF"))
            Application.OpenURL(new Uri(Path.Combine(Application.streamingAssetsPath,"Hastings_1066.pdf")).AbsoluteUri);
        if(GUILayout.Button(showHelp?"Hide controls":"Controls"))showHelp=!showHelp;
        if(showHelp)GUILayout.Label("Click a Norman counter to select it. Alt-click a stacked leader. Shift-click to add units or melee targets. Click a highlighted hex to move or an enemy to attack. Right drag to pan; wheel to zoom. Battle view refocuses the armies; Full map shows the whole board.",small);
        GUILayout.Space(8);
        GUILayout.Label($"Casualties: Norman {s.normanCasualties} · Saxon {s.saxonCasualties}");
        GUILayout.Label("Recent events",heading);
        logScroll=GUILayout.BeginScrollView(logScroll);
        foreach(var line in s.log.Skip(Math.Max(0,s.log.Count-35)).Reverse())GUILayout.Label(line,small);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
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
        float width=Mathf.Clamp(Screen.width*.50f,600f,920f);
        float height=Mathf.Clamp(Screen.height*.78f,570f,920f);
        var rect=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
        GUI.color=new Color(.22f,.10f,.07f,.98f);
        GUI.DrawTexture(rect,Texture2D.whiteTexture);
        GUI.color=new Color(.94f,.87f,.72f,.96f);
        GUI.DrawTexture(new Rect(rect.x+5,rect.y+5,rect.width-10,rect.height-10),Texture2D.whiteTexture);
        GUI.color=Color.white;
        float inset=Mathf.Clamp(width*.08f,35,80);
        float buttonHeight=Mathf.Clamp(Screen.height*.055f,50,78);
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
                if(GUILayout.Button("Resume Battle",menuButton,GUILayout.Height(buttonHeight)))showMenu=false;
                GUILayout.Space(12);
            }
            if(GUILayout.Button("New Game",menuButton,GUILayout.Height(buttonHeight)))
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
            if(GUILayout.Button("Start New Game",menuButton,GUILayout.Height(buttonHeight)))StartNewGame();
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
        Rect rect=new Rect(Screen.width*.1f,Screen.height*.1f,Screen.width*.8f,Screen.height*.8f);
        GUI.Box(rect,chart.ToUpperInvariant()+" CHART");
        if(GUI.Button(new Rect(rect.xMax-90,rect.y+5,80,26),"Close"))chart="";
        var texture=Resources.Load<Texture2D>("Art/Charts/"+chart);
        if(texture==null)return;
        Rect content=new Rect(rect.x+15,rect.y+40,rect.width-30,rect.height-55);
        chartScroll=GUI.BeginScrollView(content,chartScroll,new Rect(0,0,
            Mathf.Max(content.width,texture.width),Mathf.Max(content.height,texture.height)));
        GUI.DrawTexture(new Rect(0,0,texture.width,texture.height),texture,ScaleMode.ScaleToFit);
        GUI.EndScrollView();
    }
}
