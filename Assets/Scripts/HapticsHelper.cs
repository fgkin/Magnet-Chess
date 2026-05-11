using UnityEngine;

public static class HapticsHelper
{
    public static void LightImpact()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}