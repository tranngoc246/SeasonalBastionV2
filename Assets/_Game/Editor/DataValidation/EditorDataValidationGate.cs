#if UNITY_EDITOR
using SeasonalBastion.Contracts;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SeasonalBastion.EditorTools
{
    /// <summary>
    /// Day 17.5/18: Editor gate for data validation.
    /// - Menu: Tools/Seasonal Bastion/Validate Data
    /// - Auto-gate: validate before entering Play Mode (can be toggled)
    /// </summary>
    [InitializeOnLoad]
    public static class EditorDataValidationGate
    {
        private const string PrefKey_AutoGate = "SeasonalBastion.DataValidation.AutoGateOnPlay";
        private const string MenuRoot = "Tools/Seasonal Bastion/";

        static EditorDataValidationGate()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EnsureMenuCheckState();
        }

        // -----------------------
        // Menu
        // -----------------------
        [MenuItem(MenuRoot + "Validate Data", priority = 10)]
        public static void MenuValidateData()
        {
            var report = Validate(out var usedCatalog);

            if (report.Ok)
            {
                EditorUtility.DisplayDialog(
                    "Validate Data",
                    $"OK ✅\nCatalog: {(usedCatalog != null ? usedCatalog.name : "null")}\nErrors: 0",
                    "OK");
                Debug.Log($"[DataGate] OK. Catalog={(usedCatalog != null ? usedCatalog.name : "null")}");
            }
            else
            {
                var msg = BuildDialogMessage(report.Errors, usedCatalog);
                EditorUtility.DisplayDialog("Validate Data", msg, "OK");
                Debug.LogError($"[DataGate] FAIL. Errors={report.Errors.Count}\n" + string.Join("\n", report.Errors));
            }
        }

        [MenuItem(MenuRoot + "Data Gate On Play/Enable", priority = 20)]
        public static void EnableGate()
        {
            EditorPrefs.SetBool(PrefKey_AutoGate, true);
            EnsureMenuCheckState();
        }

        [MenuItem(MenuRoot + "Data Gate On Play/Disable", priority = 21)]
        public static void DisableGate()
        {
            EditorPrefs.SetBool(PrefKey_AutoGate, false);
            EnsureMenuCheckState();
        }

        [MenuItem(MenuRoot + "Data Gate On Play/Enable", true)]
        public static bool EnableGate_Validate()
        {
            Menu.SetChecked(MenuRoot + "Data Gate On Play/Enable", IsGateEnabled());
            return true;
        }

        [MenuItem(MenuRoot + "Data Gate On Play/Disable", true)]
        public static bool DisableGate_Validate()
        {
            Menu.SetChecked(MenuRoot + "Data Gate On Play/Disable", !IsGateEnabled());
            return true;
        }

        private static void EnsureMenuCheckState()
        {
            // Force refresh of validate menu check states
            EnableGate_Validate();
            DisableGate_Validate();
        }

        private static bool IsGateEnabled()
        {
            return EditorPrefs.GetBool(PrefKey_AutoGate, true);
        }

        // -----------------------
        // Auto Gate on Play
        // -----------------------
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!IsGateEnabled()) return;

            // Fail early: right before leaving Edit Mode
            if (state != PlayModeStateChange.ExitingEditMode) return;

            var report = Validate(out var usedCatalog);
            if (report.Ok) return;

            // Cancel entering play mode
            EditorApplication.isPlaying = false;

            var msg = BuildDialogMessage(report.Errors, usedCatalog);
            EditorUtility.DisplayDialog("Data INVALID — Play Cancelled", msg, "OK");

            Debug.LogError($"[DataGate] Play cancelled (data invalid). Errors={report.Errors.Count}\n" +
                           string.Join("\n", report.Errors));
        }

        // -----------------------
        // Core Validation
        // -----------------------
        private struct Report
        {
            public bool Ok;
            public List<string> Errors;
        }

        private static Report Validate(out DefsCatalog usedCatalog)
        {
            usedCatalog = ResolveCatalog();

            var errors = new List<string>(64);

            if (usedCatalog == null)
            {
                errors.Add("DefsCatalog not found. Assign it in GameBootstrap or create an asset via Create -> SeasonalBastion -> DefsCatalog.");
                return new Report { Ok = false, Errors = errors };
            }

            // Build minimal pipeline for editor validation:
            // DataRegistry(catalog) + DataValidator.ValidateAll(reg, errors)
            IDataRegistry reg;
            IDataValidator validator;

            try
            {
                reg = new DataRegistry(usedCatalog);
            }
            catch (System.Exception e)
            {
                errors.Add("DataRegistry ctor failed: " + e.Message);
                return new Report { Ok = false, Errors = errors };
            }

            try
            {
                validator = new DataValidator();
            }
            catch (System.Exception e)
            {
                errors.Add("DataValidator ctor failed: " + e.Message);
                return new Report { Ok = false, Errors = errors };
            }

            bool ok;
            try
            {
                ok = validator.ValidateAll(reg, errors);
            }
            catch (System.Exception e)
            {
                errors.Add("ValidateAll threw: " + e.Message);
                ok = false;
            }

            // Dedup a bit for cleaner output
            if (errors.Count > 1)
                errors = errors.Distinct().ToList();

            return new Report { Ok = ok && errors.Count == 0, Errors = errors };
        }

        private static DefsCatalog ResolveCatalog()
        {
            // Priority 1: Use catalog from GameBootstrap in open scenes
            var boot = Object.FindObjectOfType<GameBootstrap>();
            if (boot != null)
            {
                // Try read serialized private field via SerializedObject (no reflection method invoke).
                var so = new SerializedObject(boot);
                var p = so.FindProperty("_defsCatalog");
                if (p != null && p.objectReferenceValue is DefsCatalog c1)
                    return c1;
            }

            // Priority 2: find any DefsCatalog asset in project (pick first)
            var guids = AssetDatabase.FindAssets("t:DefsCatalog");
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<DefsCatalog>(path);
            }

            return null;
        }

        private static string BuildDialogMessage(List<string> errors, DefsCatalog usedCatalog)
        {
            const int cap = 12;
            var lines = new List<string>(cap + 6);

            lines.Add($"Catalog: {(usedCatalog != null ? usedCatalog.name : "null")}");
            lines.Add($"Errors: {errors.Count}");
            lines.Add("");

            for (int i = 0; i < errors.Count && i < cap; i++)
                lines.Add($"- {errors[i]}");

            if (errors.Count > cap)
                lines.Add($"... (+{errors.Count - cap} more)");

            return string.Join("\n", lines);
        }
    }
}
#endif
