using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ProceduralMeshes
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SimpleProceduralMesh : MonoBehaviour
    {
        void OnEnable()
        {
            Mesh mesh = new Mesh(){
                name = "ProceduralMesh"
            };
            mesh.vertices = new Vector3[] {
                Vector3.zero,
                Vector3.right,
                Vector3.up, 
            };

            mesh.triangles = new int[] {
                0, 2, 1,
            };

            mesh.normals = new Vector3[] {
                Vector3.back,
                Vector3.back,
                Vector3.back,
            };
            GetComponent<MeshFilter>().mesh = mesh;
            
        }
    }
}

