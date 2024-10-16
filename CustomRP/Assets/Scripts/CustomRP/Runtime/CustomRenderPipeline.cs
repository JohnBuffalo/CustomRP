using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HopsInAMaltDream {
    public class CustomRenderPipeline : RenderPipeline
    {
        bool useDynamicBating, useGPUInstancing;
        private CameraRenderer renderer = new CameraRenderer();
        private ShadowSettings shadowSettings;

        public CustomRenderPipeline(bool useDynamicBating, bool useGPUInstancing, bool useSRPBatcher, ShadowSettings shadowSettings)
        {
            this.useDynamicBating = useDynamicBating;
            this.useGPUInstancing = useGPUInstancing;
            this.shadowSettings = shadowSettings;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras){}

        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            for (int i = 0; i < cameras.Count; i++) {
                renderer.Render(context, cameras[i], useDynamicBating, useGPUInstancing, shadowSettings);
            }
        }
    }
}

