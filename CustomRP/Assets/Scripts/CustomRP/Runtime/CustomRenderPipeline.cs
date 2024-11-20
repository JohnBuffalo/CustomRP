using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;

namespace MaltsHopDream {
    public partial class CustomRenderPipeline : RenderPipeline
    {
        bool useDynamicBating, useGPUInstancing, useLightPerObject, allowHDR;
        private CameraRenderer renderer = new ();
        private ShadowSettings shadowSettings;
        private PostFXSettings postFXSettings;
        private int colorLUTResolution;
        public CustomRenderPipeline(bool allowHDR, bool useDynamicBating, bool useGPUInstancing, 
            bool useSRPBatcher, bool useLightsPerObject, 
            ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution)
        {
            this.useDynamicBating = useDynamicBating;
            this.useGPUInstancing = useGPUInstancing;
            this.shadowSettings = shadowSettings;
            this.useLightPerObject = useLightsPerObject;
            this.postFXSettings = postFXSettings;
            this.allowHDR = allowHDR;
            this.colorLUTResolution = colorLUTResolution;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
            
            InitializeForEditor();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras){}

        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            for (int i = 0; i < cameras.Count; i++) {
                renderer.Render(context, cameras[i], allowHDR, useDynamicBating, useGPUInstancing, useLightPerObject,shadowSettings,postFXSettings,colorLUTResolution);
            }
        }
    }
}

