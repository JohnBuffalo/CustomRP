using UnityEngine;
using UnityEngine.Rendering;

namespace MaltsHopDream
{
    public class PostFXStack
    {
        private const string buffName = "Post FX";

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
            this.settings = settings;
        }

        public void Render(int sourceId)
        {
            buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }
    }
}