using Impostors.RenderPipelineProxy;
using UnityEditor;

namespace Impostors.Editor
{
    [CustomEditor(typeof(BuiltInRenderPipelineProxy))]
    public class BuiltInRenderPipelineProxyEditor : UnityEditor.Editor
    {
        private SerializedProperty _ImpostorRenderingType;
        
        private void OnEnable()
        {
            _ImpostorRenderingType = serializedObject.FindProperty(nameof(BuiltInRenderPipelineProxy.ImpostorUpdateMode));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_ImpostorRenderingType);
            var t = target as BuiltInRenderPipelineProxy;
            if (t == null)
                return;
            if (t.ImpostorUpdateMode == BuiltInRenderPipelineProxy.ImpostorTextureUpdateMode.Scheduled)
            {
                EditorGUILayout.HelpBox("Scheduled rendering provides better performance, but might work incorrectly with VR projects.", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}