using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Resources;
using Newtonsoft.Json;
using OpenTyping.Properties;

namespace OpenTyping.Resources.Lang
{
    public class JsonResourceManager : ResourceManager
    {
        private readonly Dictionary<string, Dictionary<string, string>> langCache = new Dictionary<string, Dictionary<string, string>>();
        private readonly ResourceManager fallbackManager;

        public JsonResourceManager()
        {
            fallbackManager = new ResourceManager("OpenTyping.Resources.Lang.LangStr", typeof(LangStr).Assembly);
        }

        public override string GetString(string name)
        {
            return GetString(name, CultureInfo.CurrentUICulture);
        }

        public override string GetString(string name, CultureInfo culture)
        {
            if (culture == null) culture = CultureInfo.CurrentUICulture;

            string currentLang = culture.TwoLetterISOLanguageName;
            if (string.IsNullOrEmpty(currentLang))
            {
                currentLang = "en";
            }

            var dict = GetLangDict(currentLang);
            if (dict != null && dict.TryGetValue(name, out string val))
            {
                return val;
            }

            // Fallback to English (en) JSON if not found in current language
            var defaultDict = GetLangDict("en");
            if (defaultDict != null && defaultDict.TryGetValue(name, out val))
            {
                return val;
            }

            // Fallback to standard resx just in case
            return fallbackManager.GetString(name, culture);
        }

        private Dictionary<string, string> GetLangDict(string lang)
        {
            lock (langCache)
            {
                if (langCache.TryGetValue(lang, out var cached))
                {
                    return cached;
                }

                var dict = LoadLangDict(lang);
                if (dict != null)
                {
                    langCache[lang] = dict;
                    return dict;
                }

                return null;
            }
        }

        private Dictionary<string, string> LoadLangDict(string lang)
        {
            // 1. Try to load from external file "lang/{lang}.json" next to the executable
            string extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lang", $"{lang}.json");
            if (File.Exists(extPath))
            {
                try
                {
                    string json = File.ReadAllText(extPath, System.Text.Encoding.UTF8);
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
                catch (Exception ex)
                {
                    App.logger.Error($"Failed to load external lang JSON for {lang}: {ex.Message}");
                }
            }

            // 2. Try to load from embedded resource "OpenTyping.Resources.Lang.{lang}.json"
            string resourceName = $"OpenTyping.Resources.Lang.{lang}.json";
            var assembly = typeof(LangStr).Assembly;
            
            var manifestNames = assembly.GetManifestResourceNames();
            bool resourceExists = false;
            foreach (var name in manifestNames)
            {
                if (name.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
                {
                    resourceExists = true;
                    break;
                }
            }

            if (resourceExists)
            {
                try
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            using (StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                            {
                                string json = reader.ReadToEnd();
                                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.logger.Error($"Failed to load embedded lang JSON for {lang}: {ex.Message}");
                }
            }

            return null;
        }
    }
}
