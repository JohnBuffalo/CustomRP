using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static MaltsHopDream.PostFXSettings;
using static MaltsHopDream.CameraBufferSettings;

namespace MaltsHopDream
{
    public partial class PostFXStack
    {
        enum Pass
        {
            BloomHorizontal,
            BloomVertical,
            BloomAdd,
            BloomScatter,
            BloomScatterFinal,
            BloomPrefilter,
            BloomPrefilterFireflies,
            ColorGradingNone,
            ColorGradingACES,
            ColorGradingNeutral,
            ColorGradingReinhard,
            Copy,
            ApplyColorGrading,
            FinalRescale,
            FXAA,
            FXAAWithLuma,
            ApplyColorGradingWithLuma,
        }

        private Vector2Int bufferSize;
        private const string buffName = "Post FX";
        static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);

        private int
            copyBicubicId = Shader.PropertyToID("_CopyBicubic"),
            colorGradingResultId = Shader.PropertyToID("_ColorGradingResult"),
            finalResultId = Shader.PropertyToID("_FinalResult"),
            bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
            bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
            colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments"),
            colorFilterId = Shader.PropertyToID("_ColorFilter"),
            bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
            bloomResultId = Shader.PropertyToID("_BloomResult"),
            bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
            fxSourceId = Shader.PropertyToID("_PostFXSource"),
            whiteBalanceId = Shader.PropertyToID("_WhiteBalance"),
            splitToningShadowsId = Shader.PropertyToID("_SplitToningShadows"),
            splitToningHighlightsId = Shader.PropertyToID("_SplitToningHighlights"),
            channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed"),
            channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen"),
            channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue"),
            smhShadowsId = Shader.PropertyToID("_SMHShadows"),
            smhMidtonesId = Shader.PropertyToID("_SMHMidtones"),
            smhHighlightsId = Shader.PropertyToID("_SMHHighlights"),
            smhRangeId = Shader.PropertyToID("_SMHRange"),
            colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT"),
            colorGradingLUTParametersId = Shader.PropertyToID("_ColorGradingLUTParameters"),
            colorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLogC"),
            fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
            finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend"),
            finalDstBlendId = Shader.PropertyToID("_FinalDstBlend"),
            fxaaConfigId = Shader.PropertyToID("_FXAAConfig");


        private CommandBuffer buffer = new CommandBuffer()
        {
            name = buffName
        };

        private ScriptableRenderContext context;

        private Camera camera;

        private PostFXSettings settings;

        private bool keepAlpha, useHDR;
        public bool IsActive => settings != null;

        #region Bloom

        private int bloomPyramidId;

        #endregion

        private int colorLUTResolution;

        private CameraSettings.FinalBlendMode finalBlendMode;

        private BicubicRescalingMode bicubicRescaling;

        private FXAA fxaa;

        public void Setup(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize,
            PostFXSettings settings, bool keepAlpha, bool useHDR, int colorLUTResolution,
            CameraSettings.FinalBlendMode finalBlendMode, BicubicRescalingMode bicubicRescaling, FXAA fxaa)
        {
            if (settings == null)
            {
                return;
            }

            this.fxaa = fxaa;
            this.bicubicRescaling = bicubicRescaling;
            this.context = context;
            this.camera = camera;
            this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
            this.useHDR = useHDR;
            this.keepAlpha = keepAlpha;
            this.colorLUTResolution = colorLUTResolution;
            this.finalBlendMode = finalBlendMode;
            this.bufferSize = bufferSize;
            bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
            for (int i = 1; i < settings.Bloom.maxIterations * 2; i++)
            {
                Shader.PropertyToID("_BloomPyramid" + i); //PropertyID 是递增的, 所以一次性申明. 后续可通过 bloomPyramidId + offset 获取
            }

            ApplySceneViewState();
        }

        public void Render(int sourceId)
        {
            if (DoBloom(sourceId))
            {
                DoFinal(bloomResultId);
                buffer.ReleaseTemporaryRT(bloomResultId);
            }
            else
            {
                DoFinal(sourceId);
            }

            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
        {
            buffer.SetGlobalTexture(fxSourceId, from);
            buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

            buffer.DrawProcedural(
                Matrix4x4.identity, settings.Material, (int) pass, MeshTopology.Triangles, 3);
        }

        void DrawFinal(RenderTargetIdentifier from, Pass pass)
        {
            buffer.SetGlobalFloat(finalSrcBlendId, (float) finalBlendMode.source);
            buffer.SetGlobalFloat(finalDstBlendId, (float) finalBlendMode.destination);

            buffer.SetGlobalTexture(fxSourceId, from);

            var loadAction = finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect
                ? RenderBufferLoadAction.DontCare
                : RenderBufferLoadAction.Load;

            buffer.SetRenderTarget(
                BuiltinRenderTextureType.CameraTarget,
                loadAction,
                RenderBufferStoreAction.Store);
            buffer.SetViewport(camera.pixelRect);
            buffer.DrawProcedural(
                Matrix4x4.identity, settings.Material, (int) pass, MeshTopology.Triangles, 3);
        }

        bool DoBloom(int sourceId)
        {
            BloomSettings bloom = settings.Bloom;
            int width, height;
            if (bloom.ignoreRenderScale)
            {
                width = camera.pixelWidth / 2;
                height = camera.pixelHeight / 2;
            }
            else
            {
                width = bufferSize.x / 2;
                height = bufferSize.y / 2;
            }

            if (bloom.maxIterations == 0 || bloom.bloomIntensity <= 0 || height < bloom.downscaleLimit * 2 ||
                width < bloom.downscaleLimit * 2)
            {
                return false;
            }

            buffer.BeginSample("Bloom");
            Vector4 threshold;
            threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
            threshold.y = threshold.x * bloom.thresholdKnee;
            threshold.z = 2f * threshold.y;
            threshold.w = 0.25f / (threshold.y + 0.00001f);
            threshold.y -= threshold.x;
            buffer.SetGlobalVector(bloomThresholdId, threshold);

            RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            buffer.GetTemporaryRT(
                bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format
            );
            Draw(sourceId, bloomPrefilterId, bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
            width /= 2;
            height /= 2;
            int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
            int i;
            for (i = 0; i < bloom.maxIterations; i++)
            {
                if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
                {
                    break;
                }

                int midId = toId - 1;
                buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
                buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
                Draw(fromId, midId, Pass.BloomHorizontal);
                Draw(midId, toId, Pass.BloomVertical);
                fromId = toId;
                toId += 2;
                width /= 2;
                height /= 2;
            }

            buffer.ReleaseTemporaryRT(bloomPrefilterId);
            buffer.SetGlobalFloat(bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);
            Pass combinePass, finalPass;
            float finalIntensity;
            if (bloom.mode == BloomSettings.Mode.Additive)
            {
                combinePass = finalPass = Pass.BloomAdd;
                buffer.SetGlobalFloat(bloomIntensityId, bloom.bloomIntensity);
                finalIntensity = bloom.bloomIntensity;
            }
            else
            {
                combinePass = Pass.BloomScatter;
                finalPass = Pass.BloomScatterFinal;
                buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
                finalIntensity = Mathf.Min(bloom.bloomIntensity, 0.95f);
            }

            if (i > 1)
            {
                buffer.ReleaseTemporaryRT(fromId - 1);
                toId -= 5;
                for (i -= 1; i > 0; i--)
                {
                    buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                    Draw(fromId, toId, combinePass);
                    buffer.ReleaseTemporaryRT(fromId);
                    buffer.ReleaseTemporaryRT(toId + 1);
                    fromId = toId;
                    toId -= 2;
                }
            }
            else
            {
                buffer.ReleaseTemporaryRT(bloomPyramidId);
            }

            buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
            buffer.SetGlobalTexture(fxSource2Id, sourceId);
            buffer.GetTemporaryRT(bloomResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);
            Draw(fromId, bloomResultId, finalPass);
            buffer.ReleaseTemporaryRT(fromId);
            buffer.EndSample("Bloom");
            return true;
        }

        void ConfigureColorAdjustments()
        {
            ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
            buffer.SetGlobalVector(colorAdjustmentsId, new Vector4(
                Mathf.Pow(2f, colorAdjustments.postExposure),
                colorAdjustments.contrast * 0.01f + 1f,
                colorAdjustments.hueShift * (1f / 360f),
                colorAdjustments.saturation * 0.01f + 1f
            ));
            buffer.SetGlobalColor(colorFilterId, colorAdjustments.colorFilter.linear);
        }

        void ConfigureWhiteBalance()
        {
            WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
            buffer.SetGlobalVector(whiteBalanceId,
                ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.temperature, whiteBalance.tint));
        }

        void ConfigureSplitToning()
        {
            SpliteToningSettings splitToning = settings.SplitToning;
            Color splitColor = splitToning.shadows;
            splitColor.a = splitToning.balance * 0.01f;
            buffer.SetGlobalColor(splitToningShadowsId, splitColor);
            buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlight);
        }

        void ConfigureChannelMixer()
        {
            ChannelMixerSettings channelMixer = settings.ChannelMixer;
            buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
            buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
            buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
        }

        void ConfigureShadowsMidtonesHighlights()
        {
            ShadowsMidtonesHighlightsSettings smh = settings.ShadowsMidtonesHighlights;
            buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
            buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
            buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
            buffer.SetGlobalVector(smhRangeId, new Vector4(
                smh.shadowsStart, smh.shadowsEnd, smh.highlightsStart, smh.highLightsEnd
            ));
        }

        void DoFinal(int sourceId)
        {
            ConfigureColorAdjustments();
            ConfigureWhiteBalance();
            ConfigureSplitToning();
            ConfigureChannelMixer();
            ConfigureShadowsMidtonesHighlights();

            int lutHeight = colorLUTResolution;
            int lutWidth = lutHeight * lutHeight;
            buffer.GetTemporaryRT(colorGradingLUTId, lutWidth, lutHeight, 0, FilterMode.Bilinear,
                RenderTextureFormat.DefaultHDR);
            buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
                lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)
            ));
            ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
            Pass pass = mode < 0 ? Pass.Copy : Pass.ColorGradingNone + (int) mode;
            buffer.SetGlobalFloat(
                colorGradingLUTInLogId, useHDR && pass != Pass.ColorGradingNone ? 1f : 0f
            );
            Draw(sourceId, colorGradingLUTId, pass);
            buffer.SetGlobalVector(colorGradingLUTParametersId,
                new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f)
            );
            buffer.SetGlobalFloat(finalSrcBlendId, 1f);
            buffer.SetGlobalFloat(finalDstBlendId, 0f);
            if (fxaa.enabled)
            {
                buffer.SetGlobalVector(fxaaConfigId, new Vector4(fxaa.fixedThreshold, fxaa.relativeThreshold, fxaa.subpixelBlending));
                buffer.GetTemporaryRT(colorGradingResultId, bufferSize.x, bufferSize.y, 0,
                    FilterMode.Bilinear, RenderTextureFormat.Default);
                var usingPass = keepAlpha ? Pass.ApplyColorGrading : Pass.ApplyColorGradingWithLuma;
                Draw(sourceId, colorGradingResultId, usingPass);
            }

            if (bufferSize.x == camera.pixelWidth)
            {
                if (fxaa.enabled)
                {
                    var usingPass = keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma;
                    DrawFinal(colorGradingResultId, usingPass);
                    buffer.ReleaseTemporaryRT(colorGradingResultId);
                }
                else
                {
                    DrawFinal(sourceId, Pass.ApplyColorGrading);
                }
            }
            else
            {
                buffer.GetTemporaryRT(
                    finalResultId, bufferSize.x, bufferSize.y, 0,
                    FilterMode.Bilinear, RenderTextureFormat.Default
                );
                if (fxaa.enabled)
                {
                    var usingPass = keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma;
                    Draw(colorGradingResultId, finalResultId, usingPass);
                    buffer.ReleaseTemporaryRT(colorGradingResultId);
                }
                else
                {
                    Draw(sourceId, finalResultId, Pass.ApplyColorGrading);
                }

                bool bicubicSampling =
                    bicubicRescaling == BicubicRescalingMode.UpAndDown ||
                    bicubicRescaling == BicubicRescalingMode.UpAndDown ||
                    bicubicRescaling == BicubicRescalingMode.UpOnly &&
                    bufferSize.x < camera.pixelWidth;
                buffer.SetGlobalFloat(copyBicubicId, bicubicSampling ? 1f : 0f);
                DrawFinal(finalResultId, Pass.FinalRescale);
                buffer.ReleaseTemporaryRT(finalResultId);
            }

            buffer.ReleaseTemporaryRT(colorGradingLUTId);
        }
    }
}