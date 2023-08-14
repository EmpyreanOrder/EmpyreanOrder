using Impostors.Managers;
using Impostors.RenderPipelineProxy;
using UnityEditor;
using UnityEngine;

namespace Impostors.Editor
{
    [CustomEditor(typeof(CameraImpostorsManager))]
    public class CameraImpostorsManagerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var m = target as CameraImpostorsManager;
            if (m == null)
                return;

            var currentProxy = m._renderPipelineProxy;
            if (currentProxy == null && m.GetComponent<RenderPipelineProxyBase>() != null)
            {
                currentProxy = m.GetComponent<RenderPipelineProxyBase>();
                EditorUtility.SetDirty(m);
            }

            var currentProxyType = currentProxy?.GetType();
            var suggestedProxyType = RenderPipelineProxyTypeProvider.Get();

            if (currentProxy == null ||
                (RenderPipelineProxyTypeProvider.IsOneOfStandardProxy(currentProxyType) && currentProxyType != suggestedProxyType))
            {
                EditorGUILayout.Space();
                string error = "Looks like you are using wrong RenderPipelineProxy!\n" +
                               $"Current: '{(currentProxy != null ? currentProxyType.FullName : "None")}'.\n" +
                               $"Suggested: '{suggestedProxyType.FullName}'.\n\n" +
                               $"Look at the Impostors documentation about setup for render pipelines.";
                EditorGUILayout.HelpBox(error, MessageType.Error);
                if (suggestedProxyType != null && GUILayout.Button("Setup suggested RenderPipelineProxy"))
                {
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName("Setup RenderPipelineProxy");
                    var undoGroup = Undo.GetCurrentGroup();
                    Undo.RecordObject(m, "");
                    if (currentProxy != null)
                        Undo.DestroyObjectImmediate(currentProxy);

                    m._renderPipelineProxy = Undo.AddComponent(m.gameObject, suggestedProxyType) as RenderPipelineProxyBase;

                    Undo.CollapseUndoOperations(undoGroup);
                }
            }
        }
    }
}