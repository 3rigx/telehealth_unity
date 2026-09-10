using Assets.Scripts.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Assets.Editor
{
    /// <summary>
    ///     Editor convenience commands for the modern menu. Run "Set Up Modern Menu" once
    ///     in the open Menu scene; it adds the self-building <see cref="TeleRehabMenu"/> and
    ///     removes the temporary F9 overlay.
    /// </summary>
    public static class TeleRehabMenuTools
    {
        [MenuItem("Tools/TeleRehab/Set Up Modern Menu")]
        public static void SetUp()
        {
            // Remove the temporary F9 overlay if present.
            foreach (var imm in Object.FindObjectsByType<SessionSetupImmediate>(FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(imm.gameObject);
                Debug.Log("Removed F9 overlay (SessionSetupImmediate).");
            }

            var existing = Object.FindFirstObjectByType<TeleRehabMenu>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("TeleRehabMenu already present in scene.");
            }
            else
            {
                var go = new GameObject("TeleRehabMenu");
                Undo.RegisterCreatedObjectUndo(go, "Create TeleRehabMenu");
                go.AddComponent<TeleRehabMenu>();
                Selection.activeGameObject = go;
                Debug.Log("Added TeleRehabMenu to the scene. Press Play to see it.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
