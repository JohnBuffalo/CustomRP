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

        [RenderingLayerMaskField]
        public int renderingLayerMask = -1;
        public bool maskLights = false;
        public bool overridePostFX = false;
        public PostFXSettings postFXSettings = default;
        public bool copyDepth = true;
        public bool copyColor = true;
    }
}