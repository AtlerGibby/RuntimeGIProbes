using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GIProbesRuntime
{
    [DisallowMultipleComponent]
    public class GIBoundingBox : MonoBehaviour
    {
        [Tooltip("The falloff or smoothing distance outside this bounding box where GI is visible.")]
        public float boundingBoxFalloff = 0.5f;

        [Tooltip("The strength of the GI inside this bounding box.")]
        [Range(0, 1)]
        public float boundingBoxStength = 1f;
    }
}
