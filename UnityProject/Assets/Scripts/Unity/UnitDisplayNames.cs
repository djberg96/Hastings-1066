using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hastings;

public static class UnitDisplayNames
{
    private static readonly Regex UnitId=new Regex(@"\b[A-Za-z][A-Za-z0-9-]*-\d{3}\b");
    private static readonly Dictionary<string,string> TypeNames=new Dictionary<string,string>
    {
        {"BB","Breton Bowman"},{"BF","Breton Foot"},{"BK","Breton Knight"},
        {"NB","Norman Bowman"},{"NF","Norman Foot"},{"NK","Norman Knight"},
        {"FB","Flemish Bowman"},{"FF","Flemish Foot"},{"FK","Flemish Knight"},
        {"WG","William's Guard"},
        {"HC","Housecarl"},{"T","Thegn"},
        {"F1","Great Fyrd (3-3)"},{"F2","Great Fyrd (2-3)"},
        {"SB","Saxon Bowman"},{"SL","Slinger"},
        {"William","William"},{"Alan","Alan"},{"Odo","Odo"},
        {"Eustace","Eustace"},{"Harold","Harold"},
        {"Gyrth","Gyrth"},{"Leofwine","Leofwine"}
    };

    public static Dictionary<string,string> Build(GameState state)
    {
        var totals=new Dictionary<string,int>();
        foreach(var unit in state.units)
        {
            int count;
            totals.TryGetValue(unit.type,out count);
            totals[unit.type]=count+1;
        }
        var next=new Dictionary<string,int>();
        var labels=new Dictionary<string,string>();
        foreach(var unit in state.units)
        {
            int count;
            next.TryGetValue(unit.type,out count);
            next[unit.type]=++count;
            string name;
            if(!TypeNames.TryGetValue(unit.type,out name))name=unit.type;
            labels[unit.id]=totals[unit.type]>1?name+" "+count:name;
        }
        return labels;
    }

    public static string InEvent(string text,Dictionary<string,string> labels)
    {
        return UnitId.Replace(text,match=>
        {
            string label;
            return labels.TryGetValue(match.Value,out label)?label:match.Value;
        });
    }
}
