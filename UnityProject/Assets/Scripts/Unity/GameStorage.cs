using System;
using System.IO;
using System.Linq;
using Hastings;
using UnityEngine;

public static class GameStorage
{
    private static string Folder
    {
        get
        {
            var path=Path.Combine(Application.persistentDataPath,"Saves");
            Directory.CreateDirectory(path);return path;
        }
    }
    private static string PathFor(string slot)
    {
        var safe=new string(slot.Where(c=>char.IsLetterOrDigit(c)||c==' '||c=='-'||c=='_').ToArray()).Trim();
        if(safe.Length==0)safe="Game";
        if(safe.Length>48)safe=safe.Substring(0,48);
        return Path.Combine(Folder,safe+".json");
    }
    public static string[] Slots()
    {
        return Directory.GetFiles(Folder,"*.json").OrderByDescending(File.GetLastWriteTimeUtc)
            .Select(Path.GetFileNameWithoutExtension).ToArray();
    }
    public static void Save(string slot,GameState state)
    {
        var path=PathFor(slot);var temp=path+".tmp";
        var json=JsonUtility.ToJson(state,true);
        File.WriteAllText(temp,json);
        if(File.Exists(path))File.Replace(temp,path,null);
        else File.Move(temp,path);
    }
    public static GameState Load(string slot)
    {
        var path=PathFor(slot);var state=JsonUtility.FromJson<GameState>(File.ReadAllText(path));
        if(state==null||state.saveVersion!=1)throw new InvalidDataException("Unsupported save version");
        if(state.orderResults==null)state.orderResults=new System.Collections.Generic.List<OrderRollResult>();
        return state;
    }
}
