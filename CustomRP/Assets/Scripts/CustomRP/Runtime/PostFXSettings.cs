using System;
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
    }
}