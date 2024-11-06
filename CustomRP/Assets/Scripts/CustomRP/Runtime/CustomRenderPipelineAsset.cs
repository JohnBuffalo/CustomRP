using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MaltsHopDream {
    [CreateAssetMenu(menuName = "Rendering/MaltsHopDream Render Pipeline")]
    public class CustomRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField]
        bool useDynamicBating = true, useGPUInstancing = true, useSRPBatcher = true, useLightPerObject = true;
        [SerializeField]
        ShadowSettings shadows = default;
        [SerializeField]
        PostFXSettings postFXSettings = default;
        protected override RenderPipeline CreatePipeline()
        {
            return new CustomRenderPipeline(useDynamicBating, useGPUInstancing, 
                useSRPBatcher, useLightPerObject, 
                shadows, postFXSettings);
        }
    }
}

