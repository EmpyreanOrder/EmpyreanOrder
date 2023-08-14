using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Impostors.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    internal class DisableAtRuntimeAttribute : PropertyAttribute
    {
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(DisableAtRuntimeAttribute))]
    public class DisableAtRuntimeAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var guiEnabled = GUI.enabled;
            GUI.enabled = !Application.isPlaying;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = guiEnabled;
            EditorGUI.EndProperty();
        }
    }
#endif
}