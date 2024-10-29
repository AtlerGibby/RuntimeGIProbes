using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GIProbesRuntime
{
    [DisallowMultipleComponent]
    public class GILightBaker : MonoBehaviour
    {
        [Tooltip("Assign the \"UniversalRendererData\" Asset containing the GI Render Feature to this Light Baker.")]
        public UniversalRendererData myURPData;

        [Header("Prefabs")]

        [Tooltip("Assign the Baker Camera Prefab. It is just a normal camera; the Light Baker does all the set up ounce instantiated.")]
        public Camera bakerCamPrefab;

        Camera bakerCamUp;
        Camera bakerCamDown;
        Camera bakerCamRight;
        Camera bakerCamLeft;
        Camera bakerCamFront;
        Camera bakerCamBack;

        [Header("Baking Results")]

        //[HideInInspector]
        [Tooltip("List cleared before light baking begins. For viewing the positions of all GI Probes that are children of this Light Baker.")]
        public List<Vector3> probePositions = new List<Vector3>();

        [HideInInspector]
        public List<Vector3> probeUpColor = new List<Vector3>();
        [HideInInspector]
        public List<Vector3> probeDownColor = new List<Vector3>();
        [HideInInspector]
        public List<Vector3> probeLeftColor = new List<Vector3>();
        [HideInInspector]
        public List<Vector3> probeRightColor = new List<Vector3>();
        [HideInInspector]
        public List<Vector3> probeFrontColor = new List<Vector3>();
        [HideInInspector]
        public List<Vector3> probeBackColor = new List<Vector3>();
        [HideInInspector]
        public List<float> probeMinDist = new List<float>();

        [Tooltip("List cleared before light baking begins. For viewing the results of the light bake in the +Y direction.")]
        public List<Color> displayProbeUpColor = new List<Color>();
        [Tooltip("List cleared before light baking begins. For viewing the results of the light bake in the -Y direction.")]
        public List<Color> displayProbeDownColor = new List<Color>();
        [Tooltip("List cleared before light baking begins. For viewing the results of the light bake in the +X direction.")]
        public List<Color> displayProbeLeftColor = new List<Color>();
        [Tooltip("List cleared before light baking begins. For viewing the results of the light bake in the -X direction.")]
        public List<Color> displayProbeRightColor = new List<Color>();
        [Tooltip("List cleared before light baking begins. For viewing the results of the light bake in the +Z direction.")]
        public List<Color> displayProbeFrontColor = new List<Color>();
        [Tooltip("List cleared before light baking begins. For viewing the results of the light bake in the -Z direction.")]
        public List<Color> displayProbeBackColor = new List<Color>();

        List<GameObject> probes = new List<GameObject>();

        [Header("Debugging")]

        [Tooltip("Enable or disable debug messages.")]
        public bool debugLogInfoToggle = true;

        [Tooltip("Toggle off Bounding Box Renderers when in Play Mode.")]
        public bool hideGIBoundingBoxesOnPlay = true;

        [Tooltip("Toggle off GI Probe Renderers when in Play Mode.")]
        public bool hideGIPorbesOnPlay = true;


        int currentProbe = -1;
        bool clearProbeInfoInitially;
        bool initialBakeComplete = false;

        public delegate void OnFullLightBakeComplete();
        public static event OnFullLightBakeComplete fullLightBakeComplete;

        public delegate void OnFullLightBakeRestart();
        public static event OnFullLightBakeRestart fullLightBakeRestart;


        void Start()
        {
            if(bakerCamPrefab == null)
            {
                Debug.Log("The Baker Camera Prefab needs to be assigned to the Light Baker before entering Play Mode.");
                this.enabled = false;
            }

            bakerCamUp = GameObject.Instantiate(bakerCamPrefab, transform.position, transform.rotation).GetComponent<Camera>();
            bakerCamDown = GameObject.Instantiate(bakerCamPrefab, transform.position, transform.rotation).GetComponent<Camera>();
            bakerCamRight = GameObject.Instantiate(bakerCamPrefab, transform.position, transform.rotation).GetComponent<Camera>();
            bakerCamLeft = GameObject.Instantiate(bakerCamPrefab, transform.position, transform.rotation).GetComponent<Camera>();
            bakerCamFront = GameObject.Instantiate(bakerCamPrefab, transform.position, transform.rotation).GetComponent<Camera>();
            bakerCamBack = GameObject.Instantiate(bakerCamPrefab, transform.position, transform.rotation).GetComponent<Camera>();

            bakerCamUp.targetTexture = new RenderTexture(4,4,1);
            bakerCamDown.targetTexture = new RenderTexture(4,4,1);
            bakerCamLeft.targetTexture = new RenderTexture(4,4,1);
            bakerCamRight.targetTexture = new RenderTexture(4,4,1);
            bakerCamFront.targetTexture = new RenderTexture(4,4,1);
            bakerCamBack.targetTexture = new RenderTexture(4,4,1);

            bakerCamUp.transform.position = transform.GetChild(0).position;
            bakerCamDown.transform.position = transform.GetChild(0).position;
            bakerCamLeft.transform.position = transform.GetChild(0).position;
            bakerCamRight.transform.position = transform.GetChild(0).position;
            bakerCamFront.transform.position = transform.GetChild(0).position;
            bakerCamBack.transform.position = transform.GetChild(0).position;

            bakerCamUp.transform.LookAt(bakerCamUp.transform.position + Vector3.up);
            bakerCamDown.transform.LookAt(bakerCamDown.transform.position + Vector3.down);
            bakerCamLeft.transform.LookAt(bakerCamLeft.transform.position + Vector3.left);
            bakerCamRight.transform.LookAt(bakerCamRight.transform.position + Vector3.right);
            bakerCamFront.transform.LookAt(bakerCamFront.transform.position + Vector3.forward);
            bakerCamBack.transform.LookAt(bakerCamBack.transform.position + Vector3.back);

            if(hideGIPorbesOnPlay)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    probes.Add(transform.GetChild(i).gameObject);
                    if(transform.GetChild(i).GetComponent<Renderer>())
                        transform.GetChild(i).GetComponent<Renderer>().enabled = false;
                }
            }

            GIBoundingBox[] bbs = GameObject.FindObjectsOfType<GIBoundingBox>();
            if(hideGIBoundingBoxesOnPlay)
            {
                for (int i = 0; i < bbs.Length; i++)
                {
                    if(bbs[i].GetComponent<Renderer>())
                        bbs[i].GetComponent<Renderer>().enabled = false;
                }
            }

            if(debugLogInfoToggle)
            {
                Debug.Log( transform.childCount.ToString() + " GI Probes found.");
                Debug.Log(bbs.Length.ToString() + " GI bounding Boxes found. (128 Maximum)");
            }
        }


        void Update()
        {
            if(!initialBakeComplete)
                XXray();
        }

        /// <summary>
        /// Gets the GI information from a single GI Probe.
        /// </summary>
        IEnumerator BakeLighting (int whichGIProbe)
        {
            if(currentProbe != 0)
                yield return new WaitForEndOfFrame ();

            Texture2D tex2D;
            tex2D = new Texture2D(4, 4);
            Color col;

            int gIProbeIndex = whichGIProbe;
            if(gIProbeIndex == -1)
                gIProbeIndex = currentProbe;
            
            int currentFrame = 0;

            //Grab properties from the GI Probe
            GIProbeProperties.GIInfluenceType giif = GIProbeProperties.GIInfluenceType.Default;
            Color gic = Color.white;
            if(transform.GetChild(gIProbeIndex).GetComponent<GIProbeProperties>())
            {
                giif = transform.GetChild(gIProbeIndex).GetComponent<GIProbeProperties>().gIInfluenceType;
                gic = transform.GetChild(gIProbeIndex).GetComponent<GIProbeProperties>().gIInfluenceColor;
            }

            bakerCamUp.Render();
            bakerCamDown.Render();
            bakerCamLeft.Render();
            bakerCamRight.Render();
            bakerCamFront.Render();
            bakerCamBack.Render();

            for (int i = 0; i < 6; i++)
            {
                if(currentFrame % 6 == 0 )
                    RenderTexture.active = bakerCamUp.activeTexture;
                if(currentFrame % 6 == 1 )
                    RenderTexture.active = bakerCamDown.activeTexture;
                if(currentFrame % 6 == 2 )
                    RenderTexture.active = bakerCamLeft.activeTexture;
                if(currentFrame % 6 == 3 )
                    RenderTexture.active = bakerCamRight.activeTexture;
                if(currentFrame % 6 == 4 )
                    RenderTexture.active = bakerCamFront.activeTexture;
                if(currentFrame % 6 == 5 )
                    RenderTexture.active = bakerCamBack.activeTexture;

                col = Color.clear;
                for (int j = 0; j < 4; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        if(currentFrame % 6 == 0 )
                          tex2D.ReadPixels(new Rect(0, 0, bakerCamUp.activeTexture.width, bakerCamUp.activeTexture.height), j, k, false);
                        if(currentFrame % 6 == 1 )
                            tex2D.ReadPixels(new Rect(0, 0, bakerCamDown.activeTexture.width, bakerCamDown.activeTexture.height), j, k, false);
                        if(currentFrame % 6 == 2 )
                            tex2D.ReadPixels(new Rect(0, 0, bakerCamLeft.activeTexture.width, bakerCamLeft.activeTexture.height), j, k, false);
                        if(currentFrame % 6 == 3 )
                            tex2D.ReadPixels(new Rect(0, 0, bakerCamRight.activeTexture.width, bakerCamRight.activeTexture.height), j, k, false);
                        if(currentFrame % 6 == 4 )
                            tex2D.ReadPixels(new Rect(0, 0, bakerCamFront.activeTexture.width, bakerCamFront.activeTexture.height), j, k, false);
                        if(currentFrame % 6 == 5 )
                            tex2D.ReadPixels(new Rect(0, 0, bakerCamBack.activeTexture.width, bakerCamBack.activeTexture.height), j, k, false);
                        col += tex2D.GetPixel(j,k);
                    }
                }

                col /= 16;
                if(giif == GIProbeProperties.GIInfluenceType.Overwrite)
                    col = col * (1 - gic.a) + (gic * gic.a);
                if(giif == GIProbeProperties.GIInfluenceType.Multiply)
                    col = col * (1 - gic.a) + ((col * gic) * gic.a);
                if(giif == GIProbeProperties.GIInfluenceType.Add)
                    col = col * (1 - gic.a) + ((col + gic) * gic.a);
                if(giif == GIProbeProperties.GIInfluenceType.Divide)
                    col = col * (1 - gic.a) + ((col * new Color(1/gic.r, 1/gic.g, 1/gic.b, 1)) * gic.a);
                if(giif == GIProbeProperties.GIInfluenceType.Subtract)
                    col = col * (1 - gic.a) + ((col - gic) * gic.a);
                if(giif == GIProbeProperties.GIInfluenceType.Lighten)
                    col = col * (1 - gic.a) + (new Color(Mathf.Max(col.r, gic.r), Mathf.Max(col.g, gic.g), Mathf.Max(col.b, gic.b),1) * gic.a);
                if(giif == GIProbeProperties.GIInfluenceType.Darken)
                    col = col * (1 - gic.a) + (new Color(Mathf.Min(col.r, gic.r), Mathf.Min(col.g, gic.g), Mathf.Min(col.b, gic.b),1) * gic.a);

                if(currentFrame % 6 == 0 && transform.GetChild(gIProbeIndex).gameObject.activeInHierarchy)
                {
                    if(whichGIProbe == -1)
                    {
                        probeUpColor.Add(new Vector3(col.r, col.g, col.b));
                        displayProbeUpColor.Add(col);
                    }
                    else
                    {
                        probeUpColor[gIProbeIndex] = new Vector3(col.r, col.g, col.b);
                        displayProbeUpColor[gIProbeIndex] = col;
                    }
                }
                else if(currentFrame % 6 == 1 && transform.GetChild(gIProbeIndex).gameObject.activeInHierarchy)
                {
                    if(whichGIProbe == -1)
                    {
                        probeDownColor.Add(new Vector3(col.r, col.g, col.b));
                        displayProbeDownColor.Add(col);
                    }
                    else
                    {
                        probeDownColor[gIProbeIndex] = new Vector3(col.r, col.g, col.b);
                        displayProbeDownColor[gIProbeIndex] = col;
                    }
                }
                else if(currentFrame % 6 == 2 && transform.GetChild(gIProbeIndex).gameObject.activeInHierarchy)
                {
                    if(whichGIProbe == -1)
                    {
                        probeLeftColor.Add(new Vector3(col.r, col.g, col.b));
                        displayProbeLeftColor.Add(col);
                    }
                    else
                    {
                        probeLeftColor[gIProbeIndex] = new Vector3(col.r, col.g, col.b);
                        displayProbeLeftColor[gIProbeIndex] = col;
                    }
                }
                else if(currentFrame % 6 == 3 && transform.GetChild(gIProbeIndex).gameObject.activeInHierarchy)
                {
                    if(whichGIProbe == -1)
                    {
                        probeRightColor.Add(new Vector3(col.r, col.g, col.b));
                        displayProbeRightColor.Add(col);
                    }
                    else
                    {
                        probeRightColor[gIProbeIndex] = new Vector3(col.r, col.g, col.b);
                        displayProbeRightColor[gIProbeIndex] = col;
                    }
                }
                else if(currentFrame % 6 == 4 && transform.GetChild(gIProbeIndex).gameObject.activeInHierarchy)
                {
                    if(whichGIProbe == -1)
                    {
                        probeFrontColor.Add(new Vector3(col.r, col.g, col.b));
                        displayProbeFrontColor.Add(col);
                    }
                    else
                    {
                        probeFrontColor[gIProbeIndex] = new Vector3(col.r, col.g, col.b);
                        displayProbeFrontColor[gIProbeIndex] = col;
                    }
                }
                else if(currentFrame % 6 == 5)
                {
                    if(transform.GetChild(gIProbeIndex).gameObject.activeInHierarchy)
                    {
                        if(whichGIProbe == -1)
                        {
                            probeBackColor.Add(new Vector3(col.r, col.g, col.b));
                            displayProbeBackColor.Add(col);
                        }
                        else
                        {
                            probeBackColor[gIProbeIndex] = new Vector3(col.r, col.g, col.b);
                            displayProbeBackColor[gIProbeIndex] = col;
                        }
                    }
                    if(whichGIProbe == -1)
                    {
                        if(transform.childCount > gIProbeIndex + 1)
                        {
                            bakerCamUp.transform.position = transform.GetChild(gIProbeIndex + 1).position;
                            bakerCamDown.transform.position = transform.GetChild(gIProbeIndex + 1).position;
                            bakerCamLeft.transform.position = transform.GetChild(gIProbeIndex + 1).position;
                            bakerCamRight.transform.position = transform.GetChild(gIProbeIndex + 1).position;
                            bakerCamFront.transform.position = transform.GetChild(gIProbeIndex + 1).position;
                            bakerCamBack.transform.position = transform.GetChild(gIProbeIndex + 1).position;
                        }
                        currentFrame = -1;
                    }
                }
                currentFrame += 1;
            }
        }

        /// <summary>
        /// Calls the "RecalculateLightMap" function in the GI Render Feature to update Light Maps.
        /// </summary>
        IEnumerator NotifyGIRenderFeature ()
        {
            yield return new WaitForEndOfFrame();
            for (int i = 0; i < myURPData.rendererFeatures.Count; i++)
            {
                if(myURPData.rendererFeatures[i] is GIRenderFeature && probePositions.Count > 0)
                {
                    (myURPData.rendererFeatures[i] as GIRenderFeature).RecalculateLightMap();
                    break;
                }
            }
            if(debugLogInfoToggle)
            {
                Debug.Log("Updating Lightmaps...");
            }
        }

        /// <summary>
        /// Called in the beginning to capture all GI information from all child GI Probes before sending this info to the GI Render Feature.
        /// </summary>
        void XXray()
        {
            bakerCamUp.enabled = true;
            bakerCamDown.enabled = true;
            bakerCamLeft.enabled = true;
            bakerCamRight.enabled = true;
            bakerCamFront.enabled = true;
            bakerCamBack.enabled = true;

            if(!clearProbeInfoInitially)
            {
                probePositions.Clear();
                probeUpColor.Clear();
                probeDownColor.Clear();
                probeLeftColor.Clear();
                probeRightColor.Clear();
                probeFrontColor.Clear();
                probeBackColor.Clear();
                clearProbeInfoInitially = true;
            }

            if(currentProbe == transform.childCount - 1)
            {
                initialBakeComplete = true;
                if(fullLightBakeComplete != null)
                    fullLightBakeComplete();
                StartCoroutine(NotifyGIRenderFeature());

                bakerCamUp.enabled = false;
                bakerCamDown.enabled = false;
                bakerCamLeft.enabled = false;
                bakerCamRight.enabled = false;
                bakerCamFront.enabled = false;
                bakerCamBack.enabled = false;
            }
            else
            {
                //if(probePositions.Count != 0)
                //{
                    currentProbe += 1;
                //}
                bool foundNextProbe = false;
                for (int i = currentProbe; i < transform.childCount; i++)
                {
                    if(transform.GetChild(currentProbe).gameObject.activeInHierarchy)
                    {
                        foundNextProbe = true;
                        break;
                    }
                    else
                    {
                        currentProbe += 1;
                    }
                }

                if(foundNextProbe == false)
                {
                    initialBakeComplete = true;
                    if(fullLightBakeComplete != null)
                        fullLightBakeComplete();
                    StartCoroutine(NotifyGIRenderFeature());

                    bakerCamUp.enabled = false;
                    bakerCamDown.enabled = false;
                    bakerCamLeft.enabled = false;
                    bakerCamRight.enabled = false;
                    bakerCamFront.enabled = false;
                    bakerCamBack.enabled = false;
                }
                else
                {
                    if(transform.GetChild(currentProbe).GetComponent<GIProbeProperties>())
                        probeMinDist.Add(transform.GetChild(currentProbe).GetComponent<GIProbeProperties>().influenceMinDistance);
                    probePositions.Add(transform.GetChild(currentProbe).position);
                    
                    StartCoroutine(BakeLighting(-1));
                }
            }
        }

        /// <summary>
        /// Update a single GI probe before recalculating 3D Light Maps.
        /// </summary>
        /// <param name="whichGIProbe">The sibling index of a GI probe parented to a Light Baker.</param>
        public void UpdateGIProbes (int whichGIProbe)
        {
            if(initialBakeComplete)
            {
                bakerCamUp.enabled = true;
                bakerCamDown.enabled = true;
                bakerCamLeft.enabled = true;
                bakerCamRight.enabled = true;
                bakerCamFront.enabled = true;
                bakerCamBack.enabled = true;
            
                if(transform.GetChild(whichGIProbe).GetComponent<GIProbeProperties>())
                    probeMinDist[whichGIProbe] = transform.GetChild(whichGIProbe).GetComponent<GIProbeProperties>().influenceMinDistance;
                probePositions[whichGIProbe] = transform.GetChild(whichGIProbe).position;

                bakerCamUp.transform.position = transform.GetChild(whichGIProbe).position;
                bakerCamDown.transform.position = transform.GetChild(whichGIProbe).position;
                bakerCamLeft.transform.position = transform.GetChild(whichGIProbe).position;
                bakerCamRight.transform.position = transform.GetChild(whichGIProbe).position;
                bakerCamFront.transform.position = transform.GetChild(whichGIProbe).position;
                bakerCamBack.transform.position = transform.GetChild(whichGIProbe).position;

                StartCoroutine(BakeLighting(whichGIProbe));
                StartCoroutine(NotifyGIRenderFeature());

                bakerCamUp.enabled = false;
                bakerCamDown.enabled = false;
                bakerCamLeft.enabled = false;
                bakerCamRight.enabled = false;
                bakerCamFront.enabled = false;
                bakerCamBack.enabled = false;
            }
        }

        /// <summary>
        /// Update multiple GI probes before recalculating 3D Light Maps.
        /// </summary>
        /// <param name="whichGIProbe">An array of sibling indexes of GI probes parented to a Light Baker.</param>
        public void UpdateGIProbes (int[] whichGIProbe)
        {
            if(initialBakeComplete)
            {
                bakerCamUp.enabled = true;
                bakerCamDown.enabled = true;
                bakerCamLeft.enabled = true;
                bakerCamRight.enabled = true;
                bakerCamFront.enabled = true;
                bakerCamBack.enabled = true;
            
                for (int i = 0; i < whichGIProbe.Length; i++)
                {
                    if(transform.GetChild(whichGIProbe[i]).GetComponent<GIProbeProperties>())
                        probeMinDist[whichGIProbe[i]] = transform.GetChild(whichGIProbe[i]).GetComponent<GIProbeProperties>().influenceMinDistance;
                    probePositions[whichGIProbe[i]] = transform.GetChild(whichGIProbe[i]).position;

                    bakerCamUp.transform.position = transform.GetChild(whichGIProbe[i]).position;
                    bakerCamDown.transform.position = transform.GetChild(whichGIProbe[i]).position;
                    bakerCamLeft.transform.position = transform.GetChild(whichGIProbe[i]).position;
                    bakerCamRight.transform.position = transform.GetChild(whichGIProbe[i]).position;
                    bakerCamFront.transform.position = transform.GetChild(whichGIProbe[i]).position;
                    bakerCamBack.transform.position = transform.GetChild(whichGIProbe[i]).position;

                    StartCoroutine(BakeLighting(whichGIProbe[i]));
                }
                StartCoroutine(NotifyGIRenderFeature());

                bakerCamUp.enabled = false;
                bakerCamDown.enabled = false;
                bakerCamLeft.enabled = false;
                bakerCamRight.enabled = false;
                bakerCamFront.enabled = false;
                bakerCamBack.enabled = false;
            }
        }

        /// <summary>
        /// This function is called after Undoing a change and the GI Render Feature needs to be notified.
        /// </summary>
        public void NotifyGIRenderFeatureFunction ()
        {
            if(initialBakeComplete)
                StartCoroutine(NotifyGIRenderFeature());
        }

        /// <summary>
        /// Add a GI probe to the Light Baker.
        /// </summary>
        /// <param name="whichGIProbe">The GI probe to add to the Light Baker.</param>
        public void AddGIProbe (GameObject whichGIProbe)
        {
            if(whichGIProbe.GetComponent<GIProbeProperties>() && probes.Count > 0)
            {
                if(whichGIProbe.transform.parent != transform)
                    whichGIProbe.transform.parent = transform;

                int si = whichGIProbe.transform.GetSiblingIndex();
                probes.Insert(si, whichGIProbe);
                probePositions.Insert(si, whichGIProbe.transform.position);
                probeUpColor.Insert(si, Vector3.zero);
                probeDownColor.Insert(si, Vector3.zero);
                probeLeftColor.Insert(si, Vector3.zero);
                probeRightColor.Insert(si, Vector3.zero);
                probeFrontColor.Insert(si, Vector3.zero);
                probeBackColor.Insert(si, Vector3.zero);

                displayProbeUpColor.Insert(si, Color.clear);
                displayProbeDownColor.Insert(si, Color.clear);
                displayProbeLeftColor.Insert(si, Color.clear);
                displayProbeRightColor.Insert(si, Color.clear);
                displayProbeFrontColor.Insert(si, Color.clear);
                displayProbeBackColor.Insert(si, Color.clear);
            }
        }

        /// <summary>
        /// Remove a GI probe from the Light Baker.
        /// </summary>
        /// <param name="whichGIProbe">The GI probe to remove from the Light Baker.</param>
        public void RemoveGIProbe (GameObject whichGIProbe)
        {
            int probeIndex = probes.IndexOf(whichGIProbe);
            if(probeIndex >= 0 && probeIndex < probes.Count)
            {
                probes.RemoveAt(probeIndex);
                probePositions.RemoveAt(probeIndex);
                probeUpColor.RemoveAt(probeIndex);
                probeDownColor.RemoveAt(probeIndex);
                probeLeftColor.RemoveAt(probeIndex);
                probeRightColor.RemoveAt(probeIndex);
                probeFrontColor.RemoveAt(probeIndex);
                probeBackColor.RemoveAt(probeIndex);
                displayProbeUpColor.RemoveAt(probeIndex);
                displayProbeDownColor.RemoveAt(probeIndex);
                displayProbeLeftColor.RemoveAt(probeIndex);
                displayProbeRightColor.RemoveAt(probeIndex);
                displayProbeFrontColor.RemoveAt(probeIndex);
                displayProbeBackColor.RemoveAt(probeIndex);
            }

            //Destroy(transform.GetChild(whichGIProbe).gameObject);
        }

        /// <summary>
        /// Do a full restart of the light bake.
        /// </summary>
        public void FullLightBakeRestart ()
        {
            if(initialBakeComplete)
            {
                probePositions.Clear();
                probeUpColor.Clear();
                probeDownColor.Clear();
                probeLeftColor.Clear();
                probeRightColor.Clear();
                probeFrontColor.Clear();
                probeBackColor.Clear();

                displayProbeUpColor.Clear();
                displayProbeDownColor.Clear();
                displayProbeLeftColor.Clear();
                displayProbeRightColor.Clear();
                displayProbeFrontColor.Clear();
                displayProbeBackColor.Clear();

                currentProbe = -1;

                initialBakeComplete = false;
                if(fullLightBakeRestart != null)
                    fullLightBakeRestart();
            }
        }
    }
}
