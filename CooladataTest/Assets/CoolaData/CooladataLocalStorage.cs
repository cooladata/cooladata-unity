using UnityEngine;

namespace com.cooladata.tracking.sdk.utils
{
    public class CooladataLocalStorage : MonoBehaviour
    {
        public static void SaveUserId(string userId)
        {
            try
            {
                PlayerPrefs.SetString("userId", userId);
            }
            // handle the error
            catch (System.Exception err)
            {
                Debug.LogError("Can not save user id: " + err);
            }
        }

        public static string LoadUserId()
        {
            string userId = "";

            try
            {
                userId = PlayerPrefs.GetString("userId");
            }
            // handle the error
            catch (System.Exception err)
            {
                Debug.LogError("Can not load user id: " + err);
            }

            return userId;
        }
    }
}
