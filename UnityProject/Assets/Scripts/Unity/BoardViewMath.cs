using UnityEngine;

public static class BoardViewMath
{
    public const float InitialFocusY = 1080f;
    // The printed reference strip begins at 2180. Its headings extend above
    // the otherwise empty gray band, so splitting at 2200 clipped their tops.
    public const float BattlefieldHeight = 2180f;

    public static Vector2 OrientBattlefield(Vector2 point,float boardWidth,bool rotated)
    {
        return rotated?new Vector2(boardWidth-point.x,BattlefieldHeight-point.y):point;
    }

    public static float FacingRotationDegrees(int facing,bool rotated)
    {
        int normalized=((facing%6)+6)%6;
        return (1-normalized)*60f+(rotated?180f:0f);
    }

    public static int TurnFacing(int facing,bool right)
    {
        return ((facing+(right?-1:1))%6+6)%6;
    }

    public static float FitWidth(float viewportWidth,float boardWidth)
    {
        return Mathf.Max(.12f,(viewportWidth-24f)/boardWidth);
    }

    public static float FitWhole(float viewportWidth,float viewportHeight,
        float boardWidth,float boardHeight)
    {
        return Mathf.Max(.12f,Mathf.Min((viewportWidth-24f)/boardWidth,
            (viewportHeight-24f)/boardHeight));
    }

    public static Vector2 ClampPan(Vector2 pan,float scale,float viewportWidth,
        float viewportHeight,float boardWidth,float boardHeight)
    {
        return new Vector2(
            ClampAxis(pan.x,viewportWidth,boardWidth*scale),
            ClampAxis(pan.y,viewportHeight,boardHeight*scale));
    }

    private static float ClampAxis(float offset,float viewportSize,float boardSize)
    {
        if(boardSize<=viewportSize)return (viewportSize-boardSize)/2f;
        return Mathf.Clamp(offset,viewportSize-boardSize,0);
    }

    public static void Resize(ref float scale,ref Vector2 pan,
        float oldWidth,float oldHeight,float newWidth,float newHeight,float boardWidth)
    {
        float fit=FitWidth(newWidth,boardWidth);
        if(oldWidth<=0 || oldHeight<=0 || scale<=0)
        {
            scale=fit;
            pan=new Vector2((newWidth-boardWidth*scale)/2f,
                newHeight/2f-InitialFocusY*scale);
            return;
        }

        // Keep the same world point centered and retain any player zoom when
        // the window changes size. The default view grows with its viewport.
        float worldX=(oldWidth/2f-pan.x)/scale;
        float worldY=(oldHeight/2f-pan.y)/scale;
        float zoom=scale/FitWidth(oldWidth,boardWidth);
        scale=Mathf.Clamp(fit*zoom,.12f,4f);
        pan=new Vector2(newWidth/2f-worldX*scale,newHeight/2f-worldY*scale);
    }
}
