using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HopsInAMaltDream {
    public partial class CameraRenderer 
    {
        private ScriptableRenderContext context;
        private Camera camera;
        private const string bufferName = "Render Camera";
        private CommandBuffer buffer = new CommandBuffer {
            name = bufferName
        };

        CullingResults cullingResults;
        static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

        Lighting lighting = new Lighting();

        public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBating, bool useGPUInstancing, ShadowSettings shadowSettings) {
            this.context = context;
            this.camera = camera;
            PrepareForSceneWindow();
            PrepareBuffer();

            if (!Cull(shadowSettings.maxDistance)) {
                return;
            }

            buffer.BeginSample(SampleName);
            ExecuteBuffer();
            lighting.Setup(context, cullingResults, shadowSettings);
            buffer.EndSample(SampleName);
            Setup();
            DrawVisibleGeometry(useDynamicBating, useGPUInstancing);
            DrawUnsupportedShaders();
            DrawGizmos();
            lighting.Cleanup();
            Submit();

        }

        private void Setup() {
            context.SetupCameraProperties(camera);
            CameraClearFlags flags = camera.clearFlags;
            buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, 
            flags <= CameraClearFlags.Color, 
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
            buffer.BeginSample(SampleName);
            ExecuteBuffer();
        }

        private void DrawVisibleGeometry(bool useDynamicBating, bool useGPUInstancing) {
            // 创建排序设置，设置为常见的不透明物体排序标准
            var sortingSettings = new SortingSettings(camera){
                criteria = SortingCriteria.CommonOpaque
            };

            // 创建绘制设置，使用未照明着色器标签和之前创建的排序设置
            var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings){
                enableDynamicBatching = useDynamicBating,
                enableInstancing = useGPUInstancing,
                perObjectData = PerObjectData.Lightmaps | PerObjectData.LightProbe | PerObjectData.LightProbeProxyVolume
            };
            drawingSettings.SetShaderPassName(1, litShaderTagId);
            // 创建过滤设置，包含所有渲染队列
            var filteringSettings = new FilteringSettings(RenderQueueRange.all);

            // 使用上述设置绘制可见的几何体
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

            // 绘制天空盒
            context.DrawSkybox(camera);

            // 创建排序设置，设置为常见的透明物体排序标准
            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;

            // 使用上述设置绘制可见的透明物体
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        }

        private void Submit() {
            buffer.EndSample(SampleName);
            ExecuteBuffer();
            context.Submit();
        }

        private void ExecuteBuffer() {
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        bool Cull(float maxShadowDistance) {
            if (camera.TryGetCullingParameters(out ScriptableCullingParameters parameters))
            {
                parameters.shadowDistance = Mathf.Min(maxShadowDistance,camera.farClipPlane);
                cullingResults = context.Cull(ref parameters);
                return true;
            }
            return false;
        }

    }
}
