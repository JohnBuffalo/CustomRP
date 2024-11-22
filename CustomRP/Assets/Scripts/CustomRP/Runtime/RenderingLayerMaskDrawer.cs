using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MaltsHopDream
{
    [CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
    public class RenderingLayerMaskDrawer : PropertyDrawer
    {

        public static void Draw(SerializedProperty property, GUIContent label)
        {
            Draw(EditorGUILayout.GetControlRect(), property, label);
        }
        
        public static void Draw(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int mask = property.intValue;
            bool isUnit = property.type == "uint";
            if (isUnit && mask == int.MaxValue) {
                mask = -1;
            }
            mask = EditorGUI.MaskField(
                position, label, mask,
                GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames
            );
            if (EditorGUI.EndChangeCheck()) {
                property.intValue = isUnit && mask == -1 ? int.MaxValue : mask;
            }
            EditorGUI.showMixedValue = false;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // base.OnGUI(position, property, label);
            Draw(position, property, label);
        }
    }
}


