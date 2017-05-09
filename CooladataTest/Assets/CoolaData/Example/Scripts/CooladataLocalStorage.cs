using UnityEngine;

namespace com.cooladata.tracking.sdk.utils
{
    public class CooladataLocalStorage : MonoBehaviour
    {
        public static void SaveParam(string paramName, string paramValue)
        {
            try
            {
                PlayerPrefs.SetString(paramName, paramValue);
            }
            // handle the error
            catch (System.Exception err)
            {
                Debug.LogError("Can not save param: " + err);
            }
        }

        public static void SaveUserId(string userId)
        {
            SaveParam("userId", userId);
        }

        public static string LoadParam(string paramName)
        {
            string paramValue = "";

            try
            {
                paramValue = PlayerPrefs.GetString(paramName);
            }
            // handle the error
            catch (System.Exception err)
            {
                Debug.LogError("Can not param: " + err);
            }

            return paramValue;
        }

        public static string LoadUserId()
        {
            return LoadParam("userId");
        }
    }
}
