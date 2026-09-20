using UnityEngine;
using System;

namespace Core
{
    public class GrokAPIKeyManager : MonoBehaviour
    {
        private string inputKey = "";
        private bool isKeyLoaded = false;
        private bool showUI = false;

        private const string PREFS_KEY = "GrokAPIKey";

        void Start()
        {
            CheckAndLoadKey();
        }

        private const string ENCRYPTION_SALT = "BONE_GUARD_VOW"; 

        public void CheckAndLoadKey()
        {
            if (PlayerPrefs.HasKey(PREFS_KEY))
            {
                string rawSaved = PlayerPrefs.GetString(PREFS_KEY);
                string decrypted = DecryptKey(rawSaved);

                if (!string.IsNullOrEmpty(decrypted))
                {
                    isKeyLoaded = true;
                    showUI = false;
                }
            }
        }

        public static string GetAPIKey()
        {
            string raw = PlayerPrefs.GetString("GrokAPIKey", "");
            return DecryptStatic(raw);
        }

        public void ClearStoredKey()
        {
            PlayerPrefs.DeleteKey(PREFS_KEY);
            PlayerPrefs.Save();
            isKeyLoaded = false;
            showUI = true;
        }

        void OnGUI()
        {
            if (!showUI) return;

            float boxWidth = 450;
            float boxHeight = 180;
            float padding = 20;

            float startX = (Screen.width - boxWidth) / 2;
            float startY = (Screen.height - boxHeight) / 2;

            GUI.Box(new Rect(startX, startY, boxWidth, boxHeight), "Grok 'Bring Your Own Key' (BYOK) - SECURE");

            GUI.Label(new Rect(startX + padding, startY + 40, boxWidth - (padding * 2), 25), "Please enter your Grok API Key (Stored Encrypted):");

            inputKey = GUI.PasswordField(new Rect(startX + padding, startY + 65, boxWidth - (padding * 2), 30), inputKey, '*');

            if (GUI.Button(new Rect(startX + padding, startY + 110, boxWidth - (padding * 2), 40), "Save API Key Locally"))
            {
                if (!string.IsNullOrEmpty(inputKey) && inputKey.Length > 20)
                {
                    string encrypted = EncryptKey(inputKey);
                    PlayerPrefs.SetString(PREFS_KEY, encrypted);
                    PlayerPrefs.Save();
                    
                    isKeyLoaded = true;
                    showUI = false;
                }
            }
        }

        private string EncryptKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
            byte[] saltBytes = System.Text.Encoding.UTF8.GetBytes(ENCRYPTION_SALT);
            byte[] result = new byte[keyBytes.Length];
            for (int i = 0; i < keyBytes.Length; i++)
                result[i] = (byte)(keyBytes[i] ^ saltBytes[i % saltBytes.Length]);
            return System.Convert.ToBase64String(result);
        }

        private string DecryptKey(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return "";
            try {
                byte[] encryptedBytes = System.Convert.FromBase64String(encrypted);
                byte[] saltBytes = System.Text.Encoding.UTF8.GetBytes(ENCRYPTION_SALT);
                byte[] result = new byte[encryptedBytes.Length];
                for (int i = 0; i < encryptedBytes.Length; i++)
                    result[i] = (byte)(encryptedBytes[i] ^ saltBytes[i % saltBytes.Length]);
                return System.Text.Encoding.UTF8.GetString(result);
            } catch { return ""; }
        }

        private static string DecryptStatic(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return "";
            try {
                byte[] encryptedBytes = System.Convert.FromBase64String(encrypted);
                // Use the same salt as the instance version
                byte[] saltBytes = System.Text.Encoding.UTF8.GetBytes("BONE_GUARD_VOW"); 
                byte[] result = new byte[encryptedBytes.Length];
                for (int i = 0; i < encryptedBytes.Length; i++)
                    result[i] = (byte)(encryptedBytes[i] ^ saltBytes[i % saltBytes.Length]);
                return System.Text.Encoding.UTF8.GetString(result);
            } catch { return ""; }
        }
    }
}
