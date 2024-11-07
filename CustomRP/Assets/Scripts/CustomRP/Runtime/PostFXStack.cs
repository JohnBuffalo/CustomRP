using UnityEngine;
using UnityEngine.Rendering;

namespace MaltsHopDream
{
    public partial class PostFXStack
    {
        enum Pass
        {
            Copy
        }
        private const string buffName = "Post FX";
        int fxSourceId = Shader.PropertyToID("_PostFXSource");
        
        
        private CommandBuffer buffer = new CommandBuffer()
        {
            name = buffName
        };

        private ScriptableRenderContext context;

        private Camera camera;

        private PostFXSettings settings;

        public bool IsActive => settings != null;

        #region Bloom
        private const int maxBloomPyramidLevels = 16;
        private int bloomPyramidId;
        #endregion

        public PostFXStack()
        {
            bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
            for (int i = 1; i < maxBloomPyramidLevels; i++)
            {
                Shader.PropertyToID("_BloomPyramid" + i);//PropertyID 是递增的, 所以一次性申明. 后续可通过 bloomPyramidId + offset 获取
            }
        }

        public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings)
        {
            this.context = context;
            this.camera = camera;
            this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
            ApplySceneViewState();
        }

        public void Render(int sourceId)
        {
            DoBloom(sourceId);
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
        {
            buffer.SetGlobalTexture(fxSourceId, from);
            buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            
            buffer.DrawProcedural(
                Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
        }

        void DoBloom(int sourceId)
        {
            buffer.BeginSample("Bloom");
            PostFXSettings.BloomSettings bloom = settings.Bloom;
            int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
            RenderTextureFormat format = RenderTextureFormat.Default;
            int fromId = sourceId, toId = bloomPyramidId;
            int i;
            for (i = 0; i < bloom.maxIterations; i++)
            {
                if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
                {
                    break;
                }
                buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
                Draw(fromId, toId,Pass.Copy);
                fromId = toId;
                toId++;
                width /= 2;
                height /= 2;
            }
            Draw(fromId, BuiltinRenderTextureType.CameraTarget,Pass.Copy);
            for (i -= 1; i >= 0; i--)
            {
                buffer.ReleaseTemporaryRT(bloomPyramidId + i);
            }
            buffer.EndSample("Bloom");
        }
    }
}