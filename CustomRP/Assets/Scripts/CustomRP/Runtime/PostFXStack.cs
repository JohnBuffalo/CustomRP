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

        public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings)
        {
            this.context = context;
            this.camera = camera;
            this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
            ApplySceneViewState();
        }

        public void Render(int sourceId)
        {
            Draw(sourceId,BuiltinRenderTextureType.CameraTarget, Pass.Copy);
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
    }
}