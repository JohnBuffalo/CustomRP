using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MaltsHopDream
{
    [Serializable]
    public class CameraSettings
    {
        [Serializable]
        public struct FinalBlendMode
        {
            public BlendMode source, destination;
        }

        public FinalBlendMode finalBlendMode = new FinalBlendMode()
        {
            source = BlendMode.One,
            destination = BlendMode.Zero
        };

        public enum RenderScaleMode
        {
            Inherit,
            Multiply,
            Override
        }

        [RenderingLayerMaskField] public int renderingLayerMask = -1;
        public bool maskLights = false;
        public bool overridePostFX = false;
        public PostFXSettings postFXSettings = default;
        public bool copyDepth = true;
        public bool copyColor = true;

        public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

        [Range(0.1f, 2f)] public float renderScale = 1f;
        public float GetRenderScale(float scale)
        {
            return 
                renderScaleMode == RenderScaleMode.Inherit ? scale :
                renderScaleMode == RenderScaleMode.Override ? renderScale :
                scale * renderScale;
        }
    }
}