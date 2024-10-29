using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GIProbesRuntime
{
    [DisallowMultipleComponent]
    public class GICameraProperties : MonoBehaviour
    {
        [Tooltip("The GI Camera Shader material used by this camera.")]
        public Material gIImageEffect;
        [Tooltip("Is this a VR Camera?")]
        public bool vrCamera = false;
        // Marks this camera for use in the GI Probes Render Feature
    }
}
