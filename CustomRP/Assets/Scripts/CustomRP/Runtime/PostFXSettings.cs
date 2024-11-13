﻿using System;
using UnityEngine;

namespace MaltsHopDream
{
    [CreateAssetMenu(menuName = "Rendering/MaltsHopDream Post FX Settings")]
    public class PostFXSettings : ScriptableObject
    {
        [SerializeField]
        private Shader shader = default;

        [NonSerialized]
        private Material material;

        public Material Material
        {
            get
            {
                if (material == null && shader != null)
                {
                    material = new Material(shader);
                    material.hideFlags = HideFlags.HideAndDontSave;
                }

                return material;
            }
        }
                
        [Serializable]
        public struct BloomSettings
        {
            [Range(0f,16f)]
            public int maxIterations;

            [Min(1f)]
            public int downscaleLimit;

            public bool bicubicUpsampling;
                    
            [Min(0f)]
            public float threshold;

            [Range(0f, 1f)]
            public float thresholdKnee;

            [Min(0f)]
            public float bloomIntensity;

            public bool fadeFireflies;
        }
        [SerializeField]
        private BloomSettings bloom = default;

        public BloomSettings Bloom => bloom;

    }
}