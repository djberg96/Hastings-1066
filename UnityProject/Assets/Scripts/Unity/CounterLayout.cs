using UnityEngine;

public static class CounterLayout
{
    public static Rect RectFor(Vector2 center,float scale,bool leader,bool splayed)
    {
        float size=(leader?45f:65f)*scale;
        if(splayed)
            center+=new Vector2(leader?32f:-28f,leader?27f:-23f)*scale;
        return new Rect(center.x-size/2f,center.y-size/2f,size,size);
    }
}
