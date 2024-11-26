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
        private Material material;
        private const string bufferName = "Render Camera";
        private static CameraSettings defaultCameraSettings = new CameraSettings();

        private CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };

        CullingResults cullingResults;
        static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

        private static int
            colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
            depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
            depthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
            sourceTextureId = Shader.PropertyToID("_SourceTexture");

        bool useHDR;
        private bool useDepthTexture, useIntermediateBuffer;
        Lighting lighting = new();
        PostFXStack postFXStack = new();

        public CameraRenderer(Shader shader)
        {
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        public void Render(ScriptableRenderContext context, Camera camera, bool allowHDR, bool useDynamicBating,
            bool useGPUInstancing,
            bool useLightPerObject,
            ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution)
        {
            this.context = context;
            this.camera = camera;

            var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
            CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;

            useDepthTexture = true;

            if (cameraSettings.overridePostFX)
            {
                postFXSettings = cameraSettings.postFXSettings;
            }

            PrepareForSceneWindow();
            PrepareBuffer();

            if (!Cull(shadowSettings.maxDistance))
            {
                return;
            }

            useHDR = allowHDR && camera.allowHDR;
            buffer.BeginSample(SampleName);
            ExecuteBuffer();
            lighting.Setup(context, cullingResults, shadowSettings, useLightPerObject,
                cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
            postFXStack.Setup(context, camera, postFXSettings, useHDR, colorLUTResolution,
                cameraSettings.finalBlendMode);
            buffer.EndSample(SampleName);
            Setup();
            DrawVisibleGeometry(useDynamicBating, useGPUInstancing, useLightPerObject,
                cameraSettings.renderingLayerMask);
            DrawUnsupportedShaders();
            DrawGizmosBeforeFX();
            if (postFXStack.IsActive)
            {
                postFXStack.Render(colorAttachmentId);
            }
            else if (useIntermediateBuffer)
            {
                Draw(colorAttachmentId, BuiltinRenderTextureType.CameraTarget);
                ExecuteBuffer();
            }

            DrawGizmosAfterFX();
            Cleanup();
            Submit();
        }

        private void Setup()
        {
            context.SetupCameraProperties(camera);
            CameraClearFlags flags = camera.clearFlags;

            useIntermediateBuffer = useDepthTexture || postFXStack.IsActive;
            if (useIntermediateBuffer)
            {
                if (flags > CameraClearFlags.Color)
                {
                    flags = CameraClearFlags.Color;
                }

                buffer.GetTemporaryRT(
                    colorAttachmentId, camera.pixelWidth, camera.pixelHeight,
                    0, FilterMode.Bilinear,
                    useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
                buffer.GetTemporaryRT(
                    depthAttachmentId, camera.pixelWidth, camera.pixelHeight,
                    32, FilterMode.Point, RenderTextureFormat.Depth
                );
                buffer.SetRenderTarget(colorAttachmentId, RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store,
                    depthAttachmentId,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            }

            buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth,
                flags <= CameraClearFlags.Color,
                flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
            buffer.BeginSample(SampleName);
            ExecuteBuffer();
        }

        private void DrawVisibleGeometry(bool useDynamicBating, bool useGPUInstancing, bool useLightsPerObject,
            int renderingLayerMask)
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
            var filteringSettings =
                new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: (uint) renderingLayerMask);

            // 使用上述设置绘制可见的几何体
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

            // 绘制天空盒
            context.DrawSkybox(camera);
            CopyAttachments();

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
            if (useIntermediateBuffer)
            {
                buffer.ReleaseTemporaryRT(depthAttachmentId);
                buffer.ReleaseTemporaryRT(colorAttachmentId);

                if (useDepthTexture)
                {
                    buffer.ReleaseTemporaryRT(depthTextureId);
                }
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

        void CopyAttachments()
        {
            if (useDepthTexture)
            {
                buffer.GetTemporaryRT(
                    depthTextureId, camera.pixelWidth, camera.pixelHeight,
                    32, FilterMode.Point, RenderTextureFormat.Depth
                );
                buffer.CopyTexture(depthAttachmentId, depthTextureId);
                ExecuteBuffer();
            }
        }

        void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to)
        {
            buffer.SetGlobalTexture(sourceTextureId, from);
            buffer.SetRenderTarget(
                to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            buffer.DrawProcedural(
                Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3
            );
        }

        public void Dispose()
        {
            CoreUtils.Destroy(material);
        }
    }
}