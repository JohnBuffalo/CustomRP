using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MaltsHopDream.Examples
{
    [DisallowMultipleComponent]
    public class PerObjectMaterialProperties : MonoBehaviour
    {
        static MaterialPropertyBlock block;

        static int baseColorId = Shader.PropertyToID("_BaseColor");
        static int cutoffId = Shader.PropertyToID("_Cutoff");
        static int metallicId = Shader.PropertyToID("_Metallic");
        static int smoothnessId = Shader.PropertyToID("_Smoothness");
        static int emissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        Color baseColor = Color.white;
        [SerializeField, Range(0f, 1f)]
        float alphaCutoff = 0.5f;
        [SerializeField, Range(0f, 1f)]
        float metallic = 0f;
        [SerializeField, Range(0f, 1f)]
        float smoothness = 0.5f;
        [SerializeField, ColorUsage(false, true)]
        Color emissionColor = Color.black;

        public void UpdateMaterialProperties()
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            block.SetColor(baseColorId, baseColor);
            block.SetFloat(cutoffId, alphaCutoff);
            block.SetFloat(metallicId, metallic);
            block.SetFloat(smoothnessId, smoothness);
            block.SetColor(emissionColorId, emissionColor);
            GetComponent<Renderer>().SetPropertyBlock(block);
        }

        public void SetColor(Color color)
        {
            baseColor = color;
        }

        public void SetCutoff(float cutoff)
        {
            alphaCutoff = cutoff;
        }

        private void OnValidate()
        {
            UpdateMaterialProperties();
        }
    }
}
