using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MaltsHopDream
{
    public partial class CameraRenderer
    {
        private ScriptableRenderContext context;
        private Camera camera;
        private const string bufferName = "Render Camera";

        private CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };

        CullingResults cullingResults;
        static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");
        private static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");

        bool useHDR;
        Lighting lighting = new();
        PostFXStack postFXStack = new();

        public void Render(ScriptableRenderContext context, Camera camera, bool allowHDR, bool useDynamicBating, bool useGPUInstancing,
            bool useLightPerObject,
            ShadowSettings shadowSettings, PostFXSettings postFXSettings)
        {
            this.context = context;
            this.camera = camera;
            PrepareForSceneWindow();
            PrepareBuffer();

            if (!Cull(shadowSettings.maxDistance))
            {
                return;
            }
            useHDR = allowHDR && camera.allowHDR;
            buffer.BeginSample(SampleName);
            ExecuteBuffer();
            lighting.Setup(context, cullingResults, shadowSettings, useLightPerObject);
            postFXStack.Setup(context, camera, postFXSettings,useHDR);
            buffer.EndSample(SampleName);
            Setup();
            DrawVisibleGeometry(useDynamicBating, useGPUInstancing, useLightPerObject);
            DrawUnsupportedShaders();
            DrawGizmosBeforeFX();
            if (postFXStack.IsActive)
            {
                postFXStack.Render(frameBufferId);
            }
            DrawGizmosAfterFX();
            Cleanup();
            Submit();
        }

        private void Setup()
        {
            context.SetupCameraProperties(camera);
            CameraClearFlags flags = camera.clearFlags;

            if (postFXStack.IsActive)
            {
                if (flags > CameraClearFlags.Color)
                {
                    flags = CameraClearFlags.Color;
                }
                buffer.GetTemporaryRT(frameBufferId, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Bilinear,
                    useHDR?RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default);
                buffer.SetRenderTarget(frameBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            }

            buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth,
                flags <= CameraClearFlags.Color,
                flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
            buffer.BeginSample(SampleName);
            ExecuteBuffer();
        }

        private void DrawVisibleGeometry(bool useDynamicBating, bool useGPUInstancing, bool useLightsPerObject)
        {
            PerObjectData lightsPerObjectFlags = useLightsPerObject
                ? PerObjectData.LightData | PerObjectData.LightIndices
                : PerObjectData.None;

            // 创建排序设置，设置为常见的不透明物体排序标准
            var sortingSettings = new SortingSettings(camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };

            // 创建绘制设置，使用未照明着色器标签和之前创建的排序设置
            var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
            {
                enableDynamicBatching = useDynamicBating,
                enableInstancing = useGPUInstancing,
                perObjectData = PerObjectData.Lightmaps | PerObjectData.LightProbe |
                                PerObjectData.LightProbeProxyVolume | PerObjectData.ShadowMask |
                                PerObjectData.OcclusionProbe | PerObjectData.OcclusionProbeProxyVolume |
                                PerObjectData.ReflectionProbes | lightsPerObjectFlags
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

        private void Submit()
        {
            buffer.EndSample(SampleName);
            ExecuteBuffer();
            context.Submit();
        }

        private void ExecuteBuffer()
        {
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        private void Cleanup()
        {
            lighting.Cleanup();
            if (postFXStack.IsActive)
            {
                buffer.ReleaseTemporaryRT(frameBufferId);
            }
        }

        bool Cull(float maxShadowDistance)
        {
            if (camera.TryGetCullingParameters(out ScriptableCullingParameters parameters))
            {
                parameters.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
                cullingResults = context.Cull(ref parameters);
                return true;
            }

            return false;
        }
    }
}