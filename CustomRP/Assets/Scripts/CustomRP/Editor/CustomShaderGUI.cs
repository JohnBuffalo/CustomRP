using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HopsInAMaltDream.Editor
{
    public class CustomShaderGUI : ShaderGUI
    {
        MaterialEditor editor;
        Object[] materials;
        MaterialProperty[] properties;

        bool Clipping
        {
            set => SetProperty("_Clipping", "_CLIPPING", value);
        }

        bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");
        bool PremultiplyAlpha
        {
            set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
        }

        BlendMode SrcBlend
        {
            set => SetProperty("_SrcBlend", (float) value);
        }

        BlendMode DstBlend
        {
            set => SetProperty("_DstBlend", (float) value);
        }

        bool ZWrite
        {
            set => SetProperty("_ZWrite", value ? 1f : 0f);
        }

        RenderQueue RenderQueue
        {
            set
            {
                foreach (Material m in materials)
                {
                    m.renderQueue = (int) value;
                }
            }
        }

        private bool showPresets;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            base.OnGUI(materialEditor, properties);
            this.editor = materialEditor;
            this.materials = materialEditor.targets;
            this.properties = properties;

            EditorGUILayout.Space();
            showPresets = EditorGUILayout.Foldout(showPresets, "Presets ", true);
            if (showPresets)
            {
                OpaquePreset();
                ClipPreset();
                FadePreset();
                TransparentPreset();
            }
        }

        bool SetProperty(string name, float value)
        {
            MaterialProperty property = FindProperty(name, properties,false);
            if(property!=null){
                property.floatValue = value;
                return true;
            }
            return false;
        }

        void SetProperty(string name, string keyword, bool value)
        {
            if(SetProperty(name, value ? 1f : 0f)){
                SetKeyword(keyword, value);
            }
        }

        void SetKeyword(string keyword, bool enabled)
        {
            if (enabled)
            {
                foreach (Material material in materials)
                {
                    material.EnableKeyword(keyword);
                }
            }
            else
            {
                foreach (Material material in materials)
                {
                    material.DisableKeyword(keyword);
                }
            }
        }

        bool HasProperty(string name) => FindProperty(name, properties, false) != null;

        bool PresetButton(string name)
        {
            if (GUILayout.Button(name))
            {
                editor.RegisterPropertyChangeUndo(name);
                return true;
            }

            return false;
        }

        void OpaquePreset()
        {
            if (PresetButton("Opaque"))
            {
                Clipping = false;
                PremultiplyAlpha = false;
                SrcBlend = BlendMode.One;
                DstBlend = BlendMode.Zero;
                ZWrite = true;
                RenderQueue = RenderQueue.Geometry;
            }
        }

        void ClipPreset()
        {
            if (PresetButton("Clip"))
            {
                Clipping = true;
                PremultiplyAlpha = false;
                SrcBlend = BlendMode.One;
                DstBlend = BlendMode.Zero;
                ZWrite = true;
                RenderQueue = RenderQueue.AlphaTest;
            }
        }

        void FadePreset()
        {
            if (PresetButton("Fade"))
            {
                Clipping = false;
                PremultiplyAlpha = false;
                SrcBlend = BlendMode.SrcAlpha;
                DstBlend = BlendMode.OneMinusSrcAlpha;
                ZWrite = false;
                RenderQueue = RenderQueue.Transparent;
            }
        }

        void TransparentPreset()
        {
            if (HasPremultiplyAlpha && PresetButton("Transparent"))
            {
                Clipping = false;
                PremultiplyAlpha = true;
                SrcBlend = BlendMode.One;
                DstBlend = BlendMode.OneMinusSrcAlpha;
                ZWrite = false;
                RenderQueue = RenderQueue.Transparent;
            }
        }
    }
}