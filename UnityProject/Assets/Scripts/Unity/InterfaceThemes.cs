using System;
using System.Collections.Generic;
using UnityEngine;

public enum InterfaceTheme
{
    Tapestry,
    Chronicle,
    CampaignChest
}

public sealed class InterfaceThemeSkin
{
    public InterfaceTheme id;
    public string displayName,description;
    public Color panel,card,subtle,ink,mutedInk,panelInk,accent,accentHover,accentText,
        secondary,frame,frameHighlight,overlay;
    public Texture2D panelTexture,cardTexture,subtleTexture,buttonTexture,
        buttonHoverTexture,buttonActiveTexture,primaryTexture,primaryHoverTexture,ornament,
        divider;
    public readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
    public RectOffset border;

    public Texture2D Icon(string name)
    {
        Texture2D result;
        return icons.TryGetValue(name,out result)?result:null;
    }
}

public static class InterfaceThemeCatalog
{
    public const string PreferenceKey="interface-theme";
    private static readonly Dictionary<InterfaceTheme,InterfaceThemeSkin> Cache=
        new Dictionary<InterfaceTheme,InterfaceThemeSkin>();

    public static readonly InterfaceTheme[] All={
        InterfaceTheme.Tapestry,InterfaceTheme.Chronicle,InterfaceTheme.CampaignChest
    };

    public static InterfaceTheme Load()
    {
        int value=PlayerPrefs.GetInt(PreferenceKey,(int)InterfaceTheme.Tapestry);
        return Enum.IsDefined(typeof(InterfaceTheme),value)?(InterfaceTheme)value:
            InterfaceTheme.Tapestry;
    }

    public static void Save(InterfaceTheme theme)
    {
        PlayerPrefs.SetInt(PreferenceKey,(int)theme);
        PlayerPrefs.Save();
    }

    public static InterfaceThemeSkin Get(InterfaceTheme theme)
    {
        InterfaceThemeSkin skin;
        if(Cache.TryGetValue(theme,out skin))return skin;
        skin=Create(theme);Cache[theme]=skin;return skin;
    }

    private static InterfaceThemeSkin Create(InterfaceTheme theme)
    {
        var skin=new InterfaceThemeSkin{id=theme,border=new RectOffset(8,8,8,8)};
        switch(theme)
        {
            case InterfaceTheme.Chronicle:
                skin.displayName="Illuminated Chronicle";
                skin.description="Vellum, rubricated headings, blue penwork, and restrained gold accents.";
                skin.panel=C(235,220,180);skin.card=C(247,238,207);skin.subtle=C(226,209,167);
                skin.ink=C(62,38,25);skin.mutedInk=C(103,76,51);skin.panelInk=skin.ink;
                skin.accent=C(145,39,28);skin.accentHover=C(176,52,37);skin.accentText=C(255,245,215);
                skin.secondary=C(39,75,105);skin.frame=C(92,42,27);skin.frameHighlight=C(180,126,42);
                break;
            case InterfaceTheme.CampaignChest:
                skin.displayName="Campaign Chest";
                skin.description="Vellum set into dark oak, tooled leather, iron corners, and heraldic accents.";
                skin.panel=C(69,39,23);skin.card=C(229,211,169);skin.subtle=C(207,181,133);
                skin.ink=C(53,35,23);skin.mutedInk=C(92,70,49);skin.panelInk=C(246,229,190);
                skin.accent=C(126,35,25);skin.accentHover=C(157,47,34);skin.accentText=C(255,240,205);
                skin.secondary=C(142,104,54);skin.frame=C(43,25,17);skin.frameHighlight=C(165,122,62);
                break;
            default:
                skin.displayName="Bayeux Tapestry";
                skin.description="Embroidered linen, muted dyes, and borders inspired by the Bayeux Tapestry.";
                skin.panel=C(205,187,144);skin.card=C(239,225,187);skin.subtle=C(220,199,153);
                skin.ink=C(55,32,20);skin.mutedInk=C(98,73,48);skin.panelInk=skin.ink;
                skin.accent=C(132,39,27);skin.accentHover=C(165,52,34);skin.accentText=C(255,239,202);
                skin.secondary=C(42,73,83);skin.frame=C(86,40,26);skin.frameHighlight=C(178,124,53);
                break;
        }
        skin.overlay=new Color(.08f,.045f,.025f,.70f);
        skin.panelTexture=Surface(theme,skin.panel,skin.frame,11,false);
        var paperTheme=theme==InterfaceTheme.CampaignChest?
            InterfaceTheme.Chronicle:theme;
        skin.cardTexture=Surface(paperTheme,skin.card,skin.frameHighlight,23,false);
        skin.subtleTexture=Surface(paperTheme,skin.subtle,skin.secondary,37,false);
        skin.buttonTexture=Button(theme,skin.card,skin.frame,skin.secondary,41,false);
        skin.buttonHoverTexture=Button(theme,Lighten(skin.card,.08f),skin.frame,
            skin.secondary,43,false);
        skin.buttonActiveTexture=Button(theme,Darken(skin.card,.08f),skin.frame,
            skin.secondary,47,false);
        skin.primaryTexture=Button(theme,skin.accent,skin.frame,skin.frameHighlight,53,true);
        skin.primaryHoverTexture=Button(theme,skin.accentHover,skin.frame,
            skin.frameHighlight,59,true);
        LoadArtwork(skin);
        return skin;
    }

    private static void LoadArtwork(InterfaceThemeSkin skin)
    {
        string folder=skin.id.ToString();
        string root="Art/UI/Themes/"+folder+"/";
        skin.ornament=Resources.Load<Texture2D>(root+"header");
        skin.divider=Resources.Load<Texture2D>(root+"divider");
        foreach(var name in new[]{"menu","hide","focus","fit","melee","missile",
            "morale","terrain","rulebook","controls"})
        {
            var icon=Resources.Load<Texture2D>(root+name);
            if(icon!=null)skin.icons[name]=icon;
        }
    }

    private static Color C(int r,int g,int b)
    {return new Color(r/255f,g/255f,b/255f,1f);}

    private static Color Lighten(Color color,float amount)
    {return Color.Lerp(color,Color.white,amount);}

    private static Color Darken(Color color,float amount)
    {return Color.Lerp(color,Color.black,amount);}

    private static float Noise(int x,int y,int seed)
    {
        uint value=(uint)(x*374761393+y*668265263+seed*1442695041);
        value=(value^(value>>13))*1274126177u;
        return ((value^(value>>16))&1023)/1023f-.5f;
    }

    private static Color Grain(Color color,float noise)
    {return Color.Lerp(color,noise>=0?Color.white:Color.black,Mathf.Abs(noise));}

    private static Texture2D Surface(InterfaceTheme theme,Color basis,Color detail,
        int seed,bool strong)
    {
        const int width=96,height=96;
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
        {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Repeat,
            hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            float n=Noise(x,y,seed)*(strong?.14f:.075f);
            Color color=Grain(basis,n);
            if(theme==InterfaceTheme.Tapestry)
            {
                if(x%4==0)color=Color.Lerp(color,detail,.035f);
                if(y%3==0)color=Color.Lerp(color,Color.white,.035f);
                if((x+y)%17==0)color=Color.Lerp(color,detail,.025f);
            }
            else if(theme==InterfaceTheme.Chronicle)
            {
                if((y+seed)%19==0)color=Color.Lerp(color,detail,.018f);
                if(Mathf.Abs(Noise(x/7,y/2,seed+9))>.47f)
                    color=Color.Lerp(color,detail,.04f);
            }
            else
            {
                float grain=Mathf.Sin((y+Noise(x/5,y/9,seed)*12f)*.31f)*.055f;
                color=Grain(color,grain);
                if(y%23==0)color=Color.Lerp(color,detail,.10f);
            }
            pixels[y*width+x]=color;
        }
        texture.SetPixels(pixels);texture.Apply();return texture;
    }

    private static Texture2D Button(InterfaceTheme theme,Color fill,Color edge,Color detail,
        int seed,bool primary)
    {
        const int width=80,height=48,border=7;
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
        {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,
            hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int d=Math.Min(Math.Min(x,width-1-x),Math.Min(y,height-1-y));
            Color color=Grain(fill,Noise(x,y,seed)*(primary?.12f:.07f));
            if(theme==InterfaceTheme.Tapestry)
            {
                if(d<2)color=edge;
                else if(d==4 && ((x+y)/3)%2==0)color=detail;
                else if(d<border)color=Color.Lerp(color,edge,.16f);
            }
            else if(theme==InterfaceTheme.Chronicle)
            {
                if(d==1||d==5)color=edge;
                else if(d==3)color=detail;
            }
            else
            {
                if(d<3)color=edge;
                else if(d<border)color=Color.Lerp(color,detail,.40f);
                int cx=x<width/2?7:width-8,cy=y<height/2?7:height-8;
                int dx=x-cx,dy=y-cy;
                if(dx*dx+dy*dy<=4)color=detail;
            }
            pixels[y*width+x]=color;
        }
        texture.SetPixels(pixels);texture.Apply();return texture;
    }
}
