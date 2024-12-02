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
        static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;
        public const float renderScaleMin = 0.1f, renderScaleMax = 2f;

        private CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };

        CullingResults cullingResults;
        static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

        private static int
            bufferSizeId = Shader.PropertyToID("_CameraBufferSize"),
            colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
            depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
            colorTextureId = Shader.PropertyToID("_CameraColorTexture"),
            depthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
            sourceTextureId = Shader.PropertyToID("_SourceTexture"),
            srcBlendId = Shader.PropertyToID("_CameraSrcBlend"),
            dstBlendId = Shader.PropertyToID("_CameraDstBlend");

        private bool useHDR, useScaledRendering;
        private bool useColorTexture, useDepthTexture, useIntermediateBuffer;
        Lighting lighting = new();
        PostFXStack postFXStack = new();
        Texture2D missingTexture;
        static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);
        Vector2Int bufferSize;

        public CameraRenderer(Shader shader)
        {
            material = CoreUtils.CreateEngineMaterial(shader);
            missingTexture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "Missing"
            };
            missingTexture.SetPixel(0, 0, Color.white * 0.5f);
            missingTexture.Apply(true, true);
        }

        public void Render(ScriptableRenderContext context, Camera camera,
            CameraBufferSettings bufferSettings, bool useDynamicBating,
            bool useGPUInstancing,
            bool useLightPerObject,
            ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution)
        {
            this.context = context;
            this.camera = camera;

            var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
            CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;

            if (camera.cameraType == CameraType.Reflection)
            {
                useColorTexture = bufferSettings.copyColorReflection;
                useDepthTexture = bufferSettings.copyDepthReflection;
            }
            else
            {
                useColorTexture = bufferSettings.copyColor && cameraSettings.copyColor;
                useDepthTexture = bufferSettings.copyDepth && cameraSettings.copyDepth;
            }

            if (cameraSettings.overridePostFX)
            {
                postFXSettings = cameraSettings.postFXSettings;
            }

            float renderScale = cameraSettings.GetRenderScale(bufferSettings.renderScale);
            useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;
            PrepareBuffer();
            PrepareForSceneWindow();

            if (!Cull(shadowSettings.maxDistance))
            {
                return;
            }

            useHDR = bufferSettings.allowHDR && camera.allowHDR;
            if (useScaledRendering)
            {
                renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax);
                bufferSize.x = (int) (camera.pixelWidth * renderScale);
                bufferSize.y = (int) (camera.pixelHeight * renderScale);
            }
            else
            {
                bufferSize.x = camera.pixelWidth;
                bufferSize.y = camera.pixelHeight;
            }

            buffer.BeginSample(SampleName);
            buffer.SetGlobalVector(bufferSizeId, new Vector4(
                1f / bufferSize.x, 1f / bufferSize.y,
                bufferSize.x, bufferSize.y
            ));
            ExecuteBuffer();
            lighting.Setup(context, cullingResults, shadowSettings, useLightPerObject,
                cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);

            bufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;
            postFXStack.Setup(
                context, camera, bufferSize, postFXSettings, useHDR, colorLUTResolution,
                cameraSettings.finalBlendMode, bufferSettings.bicubicRescaling, bufferSettings.fxaa);
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
                DrawFinal(cameraSettings.finalBlendMode);
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

            useIntermediateBuffer = useScaledRendering || useColorTexture || useDepthTexture || postFXStack.IsActive;
            if (useIntermediateBuffer)
            {
                if (flags > CameraClearFlags.Color)
                {
                    flags = CameraClearFlags.Color;
                }

                buffer.GetTemporaryRT(
                    colorAttachmentId, bufferSize.x, bufferSize.y,
                    0, FilterMode.Bilinear,
                    useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
                buffer.GetTemporaryRT(
                    depthAttachmentId, bufferSize.x, bufferSize.y,
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
            buffer.SetGlobalTexture(colorTextureId, missingTexture);
            buffer.SetGlobalTexture(depthTextureId, missingTexture);
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
            if (useColorTexture || useDepthTexture)
            {
                CopyAttachments();
            }

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
                if (useColorTexture)
                {
                    buffer.ReleaseTemporaryRT(colorTextureId);
                }

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
            if (useColorTexture)
            {
                buffer.GetTemporaryRT(
                    colorTextureId, bufferSize.x, bufferSize.y,
                    0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
                );
                if (copyTextureSupported)
                {
                    buffer.CopyTexture(colorAttachmentId, colorTextureId);
                }
                else
                {
                    Draw(colorAttachmentId, colorTextureId);
                }
            }

            if (useDepthTexture)
            {
                buffer.GetTemporaryRT(
                    depthTextureId, bufferSize.x, bufferSize.y,
                    32, FilterMode.Point, RenderTextureFormat.Depth
                );
                if (copyTextureSupported)
                {
                    buffer.CopyTexture(depthAttachmentId, depthTextureId);
                }
                else
                {
                    Draw(depthAttachmentId, depthTextureId, true);
                    buffer.SetRenderTarget(
                        colorAttachmentId,
                        RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                        depthAttachmentId,
                        RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
                    );
                }

                ExecuteBuffer();
            }
        }

        void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
        {
            buffer.SetGlobalTexture(sourceTextureId, from);
            buffer.SetRenderTarget(
                to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            buffer.DrawProcedural(
                Matrix4x4.identity, material, isDepth ? 1 : 0, MeshTopology.Triangles, 3
            );
        }

        void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
        {
            buffer.SetGlobalFloat(srcBlendId, (float) finalBlendMode.source);
            buffer.SetGlobalFloat(dstBlendId, (float) finalBlendMode.destination);
            buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
            buffer.SetRenderTarget(
                BuiltinRenderTextureType.CameraTarget,
                finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect
                    ? RenderBufferLoadAction.DontCare
                    : RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store
            );
            buffer.SetViewport(camera.pixelRect);
            buffer.DrawProcedural(
                Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3
            );
            buffer.SetGlobalFloat(srcBlendId, 1f);
            buffer.SetGlobalFloat(dstBlendId, 0f);
        }

        public void Dispose()
        {
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(missingTexture);
        }
    }
}