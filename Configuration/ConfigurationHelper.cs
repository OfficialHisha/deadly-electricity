using BepInEx.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace DeadlyElectricity.Configuration
{
    public static class ConfigurationHelper
    {
        public static bool FailOnStringNotFound { get; set; } = true;
        public static bool FailOnBoolNotFound { get; set; } = true;
        public static bool FailOnFloatNotFound { get; set; } = true;
        public static bool FailOnIntNotFound { get; set; } = true;
        public static string StringDefaultValue { get; set; } = "";
        public static bool BoolDefaultValue { get; set; } = false;
        public static float FloatDefaultValue { get; set; } = 0f;
        public static int IntDefaultValue { get; set; } = 0;

        static readonly List<ConfigEntry<string>> stringConfigs = [];
        static readonly List<ConfigEntry<bool>> boolConfigs = [];
        static readonly List<ConfigEntry<float>> floatConfigs = [];
        static readonly List<ConfigEntry<int>> intConfigs = [];

        public static void AddConfig<T>(this ConfigFile self, string category, string identifier, T defaultValue, string description)
        {
            switch (defaultValue)
            {
                case bool boolDefault:
                    boolConfigs.Add(self.Bind(category, identifier, boolDefault, description));
                    break;
                case float floatDefault:
                    floatConfigs.Add(self.Bind(category, identifier, floatDefault, description));
                    break;
                case int intDefault:
                    intConfigs.Add(self.Bind(category, identifier, intDefault, description));
                    break;
                case string stringDefault:
                    stringConfigs.Add(self.Bind(category, identifier, stringDefault, description));
                    break;
            }
        }

        public static string GetString(string identifier)
        {
            if (FailOnBoolNotFound)
            {
                return stringConfigs.Single(config => config.Definition.Key == identifier).Value;
            }

            string value = stringConfigs.Single(config => config.Definition.Key == identifier)?.Value;
            return value ?? StringDefaultValue;
        }

        public static bool GetBool(string identifier)
        {
            if (FailOnBoolNotFound)
            {
                return boolConfigs.Single(config => config.Definition.Key == identifier).Value;
            }
            else
            {
                bool? value = boolConfigs.Single(config => config.Definition.Key == identifier)?.Value;
                return value ?? BoolDefaultValue;
            }
        }

        public static float GetFloat(string identifier)
        {
            if (FailOnFloatNotFound)
            {
                return floatConfigs.Single(config => config.Definition.Key == identifier).Value;
            }
            else
            {
                float? value = floatConfigs.SingleOrDefault(config => config.Definition.Key == identifier)?.Value;
                return value ?? FloatDefaultValue;
            }
        }
        public static int GetInt(string identifier)
        {
            if (FailOnIntNotFound)
            {
                return intConfigs.Single(config => config.Definition.Key == identifier).Value;
            }
            else
            {
                int? value = intConfigs.SingleOrDefault(config => config.Definition.Key == identifier)?.Value;
                return value ?? IntDefaultValue;
            }
        }
    }
}
