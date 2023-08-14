using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Impostors.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    internal class LayerAttribute : PropertyAttribute
    {
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(LayerAttribute))]
    public class LayerAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            property.intValue = EditorGUI.LayerField(position, label, property.intValue);
            EditorGUI.EndProperty();
        }
    }
#endif
}