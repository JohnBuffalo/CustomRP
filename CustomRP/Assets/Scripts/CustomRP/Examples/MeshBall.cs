using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HopsInAMaltDream.Examples
{
    public class MeshBall : MonoBehaviour
    {
        private static int baseColorId = Shader.PropertyToID("_BaseColor");
        private static int metallicId = Shader.PropertyToID("_Metallic");
        private static int smoothnessId = Shader.PropertyToID("_Smoothness");
        [SerializeField] private Mesh mesh = default;
        [SerializeField] private Material material = default;

        [SerializeField] private int instanceCount = 100;

        Matrix4x4[] matrices;
        Vector4[] baseColors;
        Matrix4x4[] curMatrices;
        float[] metallic;
        float[] smoothness;
        MaterialPropertyBlock block;

        private float percent;
        [SerializeField] private float speed = 1;
        private void Awake()
        {
            matrices = new Matrix4x4[instanceCount];
            baseColors = new Vector4[instanceCount];
            curMatrices = new Matrix4x4[instanceCount];
            metallic = new float[instanceCount];
            smoothness = new float[instanceCount];
            for (int i = 0; i < matrices.Length; i++)
            {
                matrices[i] = Matrix4x4.TRS(Random.insideUnitSphere * 10f, 
                Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f),
                 Vector3.one*Random.Range(0.5f,1.5f));
                baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.2f,1f));
                metallic[i] = Random.value < 0.25f ? 1f : 0f;
                smoothness[i] = Random.Range(0.025f, 0.975f);
            }
        }

        private void Update()
        {

            if (percent >= 100 || percent < 0)
            {
                speed = -speed;
            }
            percent += Time.deltaTime * speed;

            for (int i = 0; i < matrices.Length; i++)
            {
                var pos = Vector3.Lerp(matrices[i].GetPosition(), Vector3.zero, percent / 100f);
                curMatrices[i] = Matrix4x4.TRS(pos * 10f, matrices[i].rotation, matrices[i].lossyScale);
            }

            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            block.SetVectorArray(baseColorId, baseColors);
            block.SetFloatArray(metallicId, metallic);
            block.SetFloatArray(smoothnessId, smoothness);
            Graphics.DrawMeshInstanced(mesh, 0, material, curMatrices, instanceCount, block);
        }
    }
}