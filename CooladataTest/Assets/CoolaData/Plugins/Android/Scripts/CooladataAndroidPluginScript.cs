using UnityEngine;
using System;
using System.Collections;

public class CooladataAndroidPluginScript
{
    public CooladataAndroidPluginScript()
    {
        
    }

    /// <summary>
    /// Returns the referer or empty string if not found (or in case of plugin error).
    /// </summary>
    /// <returns>The referer.</returns>
    public static string GetRefferer(string refName)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            using (var javaUnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                using (var currentActivity = javaUnityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    using (var androidPlugin = new AndroidJavaObject("com.cooladata.unityplugin.AndroidPlugin", currentActivity))
                    {
                        return androidPlugin.Call<string>("getReferrer", refName);
                    }
                }
            }
        }

        return "";
    }

    /// <summary>
    /// Returns the true if the data is available
    /// </summary>
    /// <returns>True/False.</returns>
    public static bool IsReffererAvailable()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            using (var javaUnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                using (var currentActivity = javaUnityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    using (var androidPlugin = new AndroidJavaObject("com.cooladata.unityplugin.AndroidPlugin", currentActivity))
                    {
                        return androidPlugin.Call<bool>("isReffererAvailable");
                    }
                }
            }
        }

        return false;
    }
    
}
