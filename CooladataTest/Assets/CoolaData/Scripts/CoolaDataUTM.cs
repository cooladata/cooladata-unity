using UnityEngine;
using System.Collections;
using Boomlagoon.JSON;
using System.Collections.Generic;

public class CoolaDataUTM {
    public static string UTM_CAMPAIGN_KEY = "utm_campaign";
    public static string UTM_CONTENT_KEY = "utm_content";
    public static string UTM_MEDIUM_KEY = "utm_medium";
    public static string UTM_SOURCE_KEY = "utm_source";
    public static string UTM_TERM_KEY = "utm_term";

    // Is the UTM data available
    private bool mDataAvailable;

    public bool DataAvailable
    {
        get
        {
            return mDataAvailable;
        }
    }

    private Dictionary<string, JSONValue> mUTMData;

    public Dictionary<string, JSONValue> UTMData
    {
        get
        {
            return mUTMData;
        }
    }

    public CoolaDataUTM()
    {
        mUTMData = new Dictionary<string, JSONValue>();

        GetUTMValues();

        Debug.Log("UTM data abailable: " + DataAvailable);
    }

    private void GetUTMValues()
    {
#if UNITY_ANDROID
    #if UNITY_EDITOR

        Debug.Log("Preparing UTM fake data");

        // Preparing fake values 
        mUTMData[UTM_CAMPAIGN_KEY] = "utm_campaign_fake";
        mUTMData[UTM_CONTENT_KEY] = "utm_content_fake";
        mUTMData[UTM_MEDIUM_KEY] = "utm_medium_fake";
        mUTMData[UTM_SOURCE_KEY] = "utm_source_fake";
        mUTMData[UTM_TERM_KEY] = "utm_term_fake";

        mDataAvailable = true;
#else
        mDataAvailable = CooladataAndroidPluginScript.IsReffererAvailable();

        if (mDataAvailable) 
        {
            mUTMData[UTM_CAMPAIGN_KEY] = CooladataAndroidPluginScript.GetRefferer(UTM_CAMPAIGN_KEY);
            mUTMData[UTM_CONTENT_KEY] = CooladataAndroidPluginScript.GetRefferer(UTM_CONTENT_KEY);
            mUTMData[UTM_MEDIUM_KEY] = CooladataAndroidPluginScript.GetRefferer(UTM_MEDIUM_KEY);
            mUTMData[UTM_SOURCE_KEY] = CooladataAndroidPluginScript.GetRefferer(UTM_SOURCE_KEY);
            mUTMData[UTM_TERM_KEY] = CooladataAndroidPluginScript.GetRefferer(UTM_TERM_KEY);
        }
#endif
#else

#endif
    }
}
