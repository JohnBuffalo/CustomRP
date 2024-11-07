﻿using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MaltsHopDream
{
    public partial class PostFXStack
    {
        partial void ApplySceneViewState();

#if UNITY_EDITOR
        partial void ApplySceneViewState()
        {
            if (camera.cameraType == CameraType.SceneView &&
                !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
            {
                settings = null;
            }
        }
#endif
    }
}