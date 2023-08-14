#if IMPOSTORS_UNITY_PIPELINE_URP
using System.Collections.Generic;
using System.Linq;
using Impostors.URP;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Impostors.Editor
{
    [CustomEditor(typeof(UniversalRenderPipelineProxy))]
    public class UniversalRenderPipelineProxyEditor : UnityEditor.Editor
    {
        private SerializedProperty _ImpostorUpdateMode;

        private void OnEnable()
        {
            _ImpostorUpdateMode = serializedObject.FindProperty(nameof(UniversalRenderPipelineProxy.ImpostorUpdateMode));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_ImpostorUpdateMode);
            var t = target as UniversalRenderPipelineProxy;
            if (t == null)
                return;
            if (t.ImpostorUpdateMode == UniversalRenderPipelineProxy.ImpostorTextureUpdateMode.Scheduled)
            {
                EditorGUILayout.HelpBox("Scheduled rendering provides better performance, but might work incorrectly with VR projects.",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();

            if (t.ImpostorUpdateMode != UniversalRenderPipelineProxy.ImpostorTextureUpdateMode.Scheduled)
                return;

            var count = QualitySettings.names.Length;
            RenderPipelineAsset[] rpAssets = new RenderPipelineAsset[count];
            for (int i = 0; i < count; i++)
            {
                rpAssets[i] = QualitySettings.GetRenderPipelineAssetAt(i);
            }

            var hashSet = new HashSet<RenderPipelineAsset>(
                rpAssets.Where(x => x != null)
                .Append(GraphicsSettings.defaultRenderPipeline)
            );
            rpAssets = hashSet.ToArray();

            foreach (var rpAsset in rpAssets)
            {
                if (UrpUtility.TryGetImpostorsFeature(rpAsset, out var feature))
                    continue;

                if (feature == null)
                {
                    var rendererData = UrpUtility.GetScriptableRendererData(rpAsset);

                    EditorGUILayout.Space();
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox(
                        $"'{rendererData.name}' asset doesn't have {nameof(UpdateImpostorsTexturesFeature)} set up.\n" +
                        $"Please, manually add this feature. Refere to documentation for more details about URP setup.",
                        MessageType.Error);

                    if (GUILayout.Button($"Navigate to '{rendererData.name}' asset"))
                    {
                        Selection.SetActiveObjectWithContext(rendererData, rendererData);
                        EditorGUIUtility.PingObject(rendererData);
                    }
                }
            }
        }
    }
}
#endif