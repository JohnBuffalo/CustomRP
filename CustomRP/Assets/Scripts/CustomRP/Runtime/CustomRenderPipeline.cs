using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MaltsHopDream {
    public partial class CustomRenderPipeline : RenderPipeline
    {
        bool useDynamicBating, useGPUInstancing, useLightPerObject;
        private CameraRenderer renderer = new ();
        private ShadowSettings shadowSettings;
        private PostFXSettings postFXSettings;
        
        public CustomRenderPipeline(bool useDynamicBating, bool useGPUInstancing, 
            bool useSRPBatcher, bool useLightsPerObject, 
            ShadowSettings shadowSettings, PostFXSettings postFXSettings)
        {
            this.useDynamicBating = useDynamicBating;
            this.useGPUInstancing = useGPUInstancing;
            this.shadowSettings = shadowSettings;
            this.useLightPerObject = useLightsPerObject;
            this.postFXSettings = postFXSettings;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
            
            InitializeForEditor();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras){}

        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            for (int i = 0; i < cameras.Count; i++) {
                renderer.Render(context, cameras[i], useDynamicBating, useGPUInstancing, useLightPerObject,shadowSettings,postFXSettings);
            }
        }
    }
}

