using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace GIProbesRuntime
{
    [CustomEditor(typeof(GIProbeProperties)), CanEditMultipleObjects]
    public class GIProbeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GIProbeProperties myScript = (GIProbeProperties)target;
            if(GUILayout.Button(new GUIContent("Update This GI Probe", EditorGUIUtility.FindTexture( "d_PreMatSphere" ), "In Play Mode, tell the light baker this probe has changed. Cannot update multiple GI probes at the same time using this button in multi-edit.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if(myScript.transform.parent != null)
                    {
                        if(myScript.transform.parent.gameObject.GetComponent<GILightBaker>())
                        {
                            Undo.RegisterCompleteObjectUndo(myScript.transform.parent.gameObject.GetComponent<GILightBaker>(), "Before Updating GI Probe");
                            myScript.transform.parent.gameObject.GetComponent<GILightBaker>().
                            UpdateGIProbes(myScript.gameObject.transform.GetSiblingIndex());
                        }
                        else
                        {
                            Debug.Log("This probe has to be the child of a LightBaker.");
                        }
                    }
                    else
                    {
                        Debug.Log("This probe has to be the child of a LightBaker.");
                    }
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }
        }
    }
}

