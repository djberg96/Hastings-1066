using UnityEngine;

public static class CounterLayout
{
    public static Rect RectFor(Vector2 center,float scale,bool leader,bool splayed)
    {return RectFor(center,scale,leader,splayed?1f:0f);}

    public static Rect RectFor(Vector2 center,float scale,bool leader,float spread)
    {
        float size=(leader?45f:65f)*scale;
        spread=Mathf.Clamp01(spread);
        float eased=spread*spread*(3f-2f*spread);
        center+=new Vector2(leader?32f:-28f,leader?27f:-23f)*scale*eased;
        return new Rect(center.x-size/2f,center.y-size/2f,size,size);
    }
}
