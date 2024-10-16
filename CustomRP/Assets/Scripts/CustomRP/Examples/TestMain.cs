using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace HopsInAMaltDream.Examples
{
    public class TestMain : MonoBehaviour
    {
        public List<GameObject> Cubes;
        public int length;
        public int width;
        public int gap;

        public Color startColor;
        public Color endColor;

        void Awake()
        {
            CreateCubes();
        }

        void CreateCubes()
        {
            float index = 0;
            float max = length * width;
            for (int y = 0; y < length; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int cubeIndex = (int)index % Cubes.Count;
                    var ins = GameObject.Instantiate(Cubes[cubeIndex]);
                    ins.name = index.ToString();
                    var properties = ins.GetComponent<PerObjectMaterialProperties>();
                    properties.SetColor(Color.Lerp(startColor,endColor,index++/max));
                    properties.SetCutoff(Mathf.Sin(index));
                    properties.UpdateMaterialProperties();
                    ins.transform.SetParent(transform);
                    ins.transform.localPosition = new Vector3(x *gap, 0, y *gap);
                    gameObject.name = $"root {index}";
                }
            }
        }
    }
}

