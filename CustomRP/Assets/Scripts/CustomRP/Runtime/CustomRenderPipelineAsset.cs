using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MaltsHopDream {
    [CreateAssetMenu(menuName = "Rendering/MaltsHopDream Render Pipeline")]
    public partial class CustomRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField]
        bool allowHDR = true;
        [SerializeField]
        bool useDynamicBating = true, useGPUInstancing = true, useSRPBatcher = true, useLightPerObject = true;
        [SerializeField]
        ShadowSettings shadows = default;
        [SerializeField]
        PostFXSettings postFXSettings = default;
        public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }
        [SerializeField]
        ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;
        protected override RenderPipeline CreatePipeline()
        {
            return new CustomRenderPipeline(
                allowHDR,useDynamicBating, useGPUInstancing, 
                useSRPBatcher, useLightPerObject, 
                shadows, postFXSettings, (int)colorLUTResolution);
        }
    }
}

