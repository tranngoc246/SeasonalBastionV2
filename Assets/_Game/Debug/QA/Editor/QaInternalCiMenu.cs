#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SeasonalBastion
{
    public static class QaInternalCiMenu
    {
        private static bool _pending;
        private static double _startTime;
        private static int _pollFrames;

        [MenuItem("QA/Run Internal CI (B - SaveLoad Matrix)")]
        public static void RunB_Menu()
        {
            _pending = true;
            _pollFrames = 0;
            _startTime = EditorApplication.timeSinceStartup;

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
            }
            else
            {
                EditorApplication.update += PollAndRun;
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange st)
        {
            if (!_pending) return;

            if (st == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.update += PollAndRun;
            }
            else if (st == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            }
        }

        private static void PollAndRun()
        {
            if (!_pending)
            {
                EditorApplication.update -= PollAndRun;
                return;
            }

            _pollFrames++;

            // Timeout safety (avoid infinite polling)
            if (EditorApplication.timeSinceStartup - _startTime > 15.0)
            {
                _pending = false;
                EditorApplication.update -= PollAndRun;
                EditorApplication.isPlaying = false;
                EditorUtility.DisplayDialog("QA-CI", "FAIL: Timeout waiting for GameBootstrap.Services", "OK");
                return;
            }

            var boot = Object.FindObjectOfType<GameBootstrap>();
            if (boot == null || boot.Services == null)
                return;

            // Run once
            _pending = false;
            EditorApplication.update -= PollAndRun;

            var rep = QaInternalCiRunner.RunB(boot.Services, writeReport: true);

            EditorUtility.DisplayDialog(
                "QA-CI (B)",
                (rep.passed ? "PASS\n\n" : "FAIL\n\n") + rep.summary + (string.IsNullOrEmpty(rep.reportPath) ? "" : ("\n\nReport:\n" + rep.reportPath)),
                "OK"
            );

            // Auto stop play mode
            EditorApplication.isPlaying = false;
        }
    }
}
#endif