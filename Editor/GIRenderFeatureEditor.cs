using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;

namespace GIProbesRuntime
{
    [CustomEditor(typeof(GIRenderFeature))]
    public class GIRenderFeatureEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GIRenderFeature myScript = (GIRenderFeature)target;

            if(GUILayout.Button(new GUIContent("Update Bounding Boxes", EditorGUIUtility.FindTexture( "PreMatCube" ), "In Play Mode, if bounding boxes are added or removed from the scene, you will need to inform this Render Feature.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    myScript.UpdateBoundingBoxInfo();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Update GI Cameras", EditorGUIUtility.FindTexture( "SceneViewCamera" ), "In Play Mode, if cameras are added or removed from the scene with GI Camera Properties, you will need to inform this Render Feature.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    myScript.UpdateGICameraInfo();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }
        }
    }
}
