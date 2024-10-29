using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace GIProbesRuntime
{
    [CustomEditor(typeof(GILightBaker))]
    public class GILightBakerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GILightBaker myScript = (GILightBaker)target;
            if(GUILayout.Button(new GUIContent("Full Light Bake Restart", EditorGUIUtility.FindTexture( "AutoLightbakingOn" ), "In Play Mode, recapture all GI information from all GI Probes.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Undo.RecordObject(myScript, "Full Light Bake Restart");
                    myScript.FullLightBakeRestart();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Notify GI Render Feature", EditorGUIUtility.FindTexture( "console.warnicon.sml" ), "In Play Mode, if the lightmap changes aren't visible in the scene, you can force the GI Render Feature to update.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    myScript.NotifyGIRenderFeatureFunction();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }
        }
    }
}
