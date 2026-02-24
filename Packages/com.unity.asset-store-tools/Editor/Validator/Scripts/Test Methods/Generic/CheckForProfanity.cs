using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.Services.Validation;
using AssetStoreTools.Validator.TestDefinitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckForProfanity : ITestScript
    {
        private GenericTestConfig _config;
        private IAssetUtilityService _assetUtility;
        private IScriptUtilityService _scriptUtility;
        private Regex _regex;

        private static readonly string[] BANNED_WORDS = new string[]
            {
                "Fuck",
                "Penis",
                "Intercourse",
            };

        public CheckForProfanity(GenericTestConfig config, IAssetUtilityService assetUtility, IScriptUtilityService scriptUtility)
        {
            _config = config;
            _assetUtility = assetUtility;
            _scriptUtility = scriptUtility;
            SetupRegex();
        }

        private void SetupRegex()
        {
            if (BANNED_WORDS == null || BANNED_WORDS.Length == 0)
            {
                _regex = null;
                return;
            }

            var escaped = BANNED_WORDS
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(Regex.Escape);

            var pattern = $@"\b({string.Join("|", escaped)})\b";

            _regex = new Regex(
                pattern,
                RegexOptions.IgnoreCase |
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);
        }

        public TestResult Run()
        {
            var result = new TestResult() { Status = TestResultStatus.Undefined };

            #region Scripts

            var scripts = _assetUtility.GetObjectsFromAssets<MonoScript>(_config.ValidationPaths, AssetType.MonoScript).ToArray();
            var affectedScripts = new Dictionary<MonoScript, List<string>>();
            foreach (var script in scripts)
            {
                var bannedWords = FindBannedWords(script.text);
                if (bannedWords.Count() > 0)
                {
                    affectedScripts.Add(script, new List<string>() { GetBannedWordReport(script) });
                }
            }

            #endregion

            #region Precompiled Assemblies

            var assemblies = _assetUtility.GetObjectsFromAssets(_config.ValidationPaths, AssetType.PrecompiledAssembly).ToArray();
            var assemblyTypes = _scriptUtility.GetTypesFromAssemblies(assemblies);

            var affectedAssemblies = new Dictionary<UnityObject, List<string>>();
            foreach (var assembly in assemblies)
            {
                // var bannedWords = FindBannedWords(assembly.);
                // if (bannedWords.Count() > 0)
                // {
                //     affectedScripts.Add(script, new List<string>() { GetBannedWordReport(script) });
                // }
            }

            #endregion
            if (affectedScripts.Count > 0 || affectedAssemblies.Count > 0)
            {
                if (affectedScripts.Count > 0)
                {
                    result.Status = TestResultStatus.VariableSeverityIssue;
                    result.AddMessage("The following scripts contain possible profanity: ");
                    foreach (var kvp in affectedScripts)
                    {
                        var message = string.Empty;
                        foreach (var type in kvp.Value)
                            message += type + "\n";

                        message = message.Remove(message.Length - "\n".Length);
                        result.AddMessage(message, null, kvp.Key);
                    }
                }

                if (affectedAssemblies.Count > 0)
                {
                    result.Status = TestResultStatus.VariableSeverityIssue;
                    result.AddMessage("The following assemblies contain possible profanity:");
                    foreach (var kvp in affectedAssemblies)
                    {
                        var message = string.Empty;
                        foreach (var type in kvp.Value)
                            message += type + "\n";

                        message = message.Remove(message.Length - "\n".Length);
                        result.AddMessage(message, null, kvp.Key);
                    }
                }
            }
            else
            {
                result.Status = TestResultStatus.Pass;
                result.AddMessage("No profanity was found!");
            }

            return result;
        }

        public string[] FindBannedWords(string text)
        {
            if (_regex == null || string.IsNullOrEmpty(text))
                return Array.Empty<string>();

            var matches = _regex.Matches(text);

            if (matches.Count == 0)
                return Array.Empty<string>();

            // Return distinct words matched (case-insensitive)
            return matches
                .Select(m => m.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string GetBannedWordReport(MonoScript script)
        {
            string[] foundWords = FindBannedWords(script.text);
            if (foundWords.Length == 0)
                return null; // nothing found

            var sb = new StringBuilder();

            sb.AppendLine($"Script: {AssetDatabase.GetAssetPath(script)}");
            sb.AppendLine("Words potentially contained profanity found:");
            sb.AppendLine(string.Join("\n", foundWords));
            sb.AppendLine($"(Script '{script.name}')\n");
            return sb.ToString();
        }
    }

}
