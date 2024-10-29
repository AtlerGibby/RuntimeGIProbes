using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GIProbesRuntime
{
    public class DemoFreeCamera : MonoBehaviour
    {
        [Tooltip("Default movement speed.")]
        public float moveSpeed = 5;

        [Tooltip("Speed multiplier.")]
        public float speedBoostAmount = 2;
        float speedBoost = 1;


        Light spotLight;
        bool spotlightOn;

        [Tooltip("The \"UniversalRendererData\" Asset used by this camera.")]
        public UniversalRendererData myURPData;

        GICameraProperties myCam;
        bool directLightDebugOn;
        bool lightMapDebugOn;
        bool HUDOn = true;

        [Tooltip("The first light baker for scene swapping. Doesn't have to be assigned if you just need a free camera.")]
        public GILightBaker modernLightBaker;
        [Tooltip("The second light baker for scene swapping. Doesn't have to be assigned if you just need a free camera.")]
        public GILightBaker medievalLightBaker;
        [Tooltip("The first scene for scene swapping. Doesn't have to be assigned if you just need a free camera.")]
        public GameObject modernScene;
        [Tooltip("The second scene for scene swapping. Doesn't have to be assigned if you just need a free camera.")]
        public GameObject medievalScene;

        bool insideMedievalScene;
        bool pauseWhileLoading;
        bool mouseLookToggle = true;

        [Tooltip("A prefab that can be spawned in, expected to have a rigidbody.")]
        public GameObject cubePrimitive;
        List<GameObject> primitives = new List<GameObject>();

        AudioSource lightAudio;
        AudioSource spawnAudio;

        // Start is called before the first frame update
        void Start()
        {
            spotLight = transform.Find("Spot Light").gameObject.GetComponent<Light>();
            myCam = transform.GetComponent<GICameraProperties>();
            if(myCam)
            {
                myCam.gIImageEffect.SetFloat("_DebugGILC", 0);
                myCam.gIImageEffect.SetFloat("_DebugMode", 0);
            }

            // These are events called by the light baker we subscribe to
            GILightBaker.fullLightBakeRestart += GILoadingStart;
            GILightBaker.fullLightBakeComplete += GILoadingComplete;

            if(medievalLightBaker != null && medievalScene != null)
            {
                if(medievalLightBaker.gameObject.activeInHierarchy && medievalScene.activeInHierarchy)
                {
                    insideMedievalScene = true;
                }
            }

            lightAudio = transform.Find("Light Audio").GetComponent<AudioSource>();
            spawnAudio = transform.Find("Spawn Audio").GetComponent<AudioSource>();

            // Load GI at beginning
            GILoadingStart();
        }

        // Update is called once per frame
        void Update()
        {
            // Disable all input when loading GI info
            if(!pauseWhileLoading)
            {
                PlayerFunction();
            }
        }

        void PlayerFunction ()
        {
            // Rotate
            float inputRotateAxisX = 0;
            float inputRotateAxisY = 0;

            // Left, Right, Front, and Back
            float inputVertical = 0;
            float inputHorizontal = 0;
            // Up and Down
            float inputYAxis = 0;

            for(int i = 0; i < Input.touchCount; i++)
            {
                if(Input.touches[i].position.x < Screen.width / 2)
                {
                    inputHorizontal += (Input.touches[i].position.x - Screen.width / 4) / (Screen.width/4) * 3;
                    inputVertical += (Input.touches[i].position.y - Screen.height / 2) / (Screen.height/2) * 3;
                }
            }

            if(Input.GetKeyDown(KeyCode.Escape))
                mouseLookToggle = !mouseLookToggle;

            if(Input.GetMouseButtonDown(0))
                Cursor.visible = !Cursor.visible;
            
            if(Input.GetKey(KeyCode.W))
                inputVertical += 1;
            if(Input.GetKey(KeyCode.S))
                inputVertical -= 1;
            if(Input.GetKey(KeyCode.A))
                inputHorizontal -= 1;
            if(Input.GetKey(KeyCode.D))
                inputHorizontal += 1;
            if(Input.GetKey(KeyCode.Space))
                inputYAxis += 1;
            if(Input.GetKey(KeyCode.LeftControl))
                inputYAxis -= 1;
            if(Input.GetKey(KeyCode.LeftShift))
                speedBoost = speedBoostAmount;
            else
                speedBoost = 1;
            
            if(Input.GetKeyDown(KeyCode.F)) // Spot Light
            {
                spotlightOn = !spotlightOn;
                lightAudio.Play();
            }

            if(spotlightOn)
                spotLight.intensity = 25;
            else
                spotLight.intensity = 0;

            if(Input.GetKeyDown(KeyCode.G)) // Spawn Primitive
            {
                GameObject goTmp = GameObject.Instantiate(cubePrimitive, transform.position, transform.rotation);
                goTmp.GetComponent<Rigidbody>().AddForce(transform.forward * 600);
                primitives.Add(goTmp);
                spawnAudio.Play();
            }

            if(Input.GetKeyDown(KeyCode.V)) // Toggle GI Probe / Bounding Box Visibility
            {
                if(modernLightBaker != null && modernScene != null &&
                medievalLightBaker != null && medievalScene != null)
                {
                    if(insideMedievalScene)
                    {
                        for (int i = 0; i < medievalLightBaker.transform.childCount; i++)
                        {
                            medievalLightBaker.transform.GetChild(i).GetComponent<Renderer>().enabled = !medievalLightBaker.transform.GetChild(i).GetComponent<Renderer>().enabled;
                        }
                        for (int i = 0; i < medievalScene.transform.childCount; i++)
                        {
                            if(medievalScene.transform.GetChild(i).GetComponent<GIBoundingBox>())
                                medievalScene.transform.GetChild(i).GetComponent<Renderer>().enabled = !medievalScene.transform.GetChild(i).GetComponent<Renderer>().enabled;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < modernLightBaker.transform.childCount; i++)
                        {
                            modernLightBaker.transform.GetChild(i).GetComponent<Renderer>().enabled = !modernLightBaker.transform.GetChild(i).GetComponent<Renderer>().enabled;
                        }
                        for (int i = 0; i < modernScene.transform.childCount; i++)
                        {
                            if(modernScene.transform.GetChild(i).GetComponent<GIBoundingBox>())
                                modernScene.transform.GetChild(i).GetComponent<Renderer>().enabled = !modernScene.transform.GetChild(i).GetComponent<Renderer>().enabled;
                        }
                    }
                }
                else
                {
                    GILightBaker lb = FindObjectOfType<GILightBaker>();
                    for (int i = 0; i < lb.transform.childCount; i++)
                    {
                        lb.transform.GetChild(i).GetComponent<Renderer>().enabled = !lb.transform.GetChild(i).GetComponent<Renderer>().enabled;
                    }
                    GIBoundingBox[] bbs = FindObjectsOfType<GIBoundingBox>();
                    for (int i = 0; i < bbs.Length; i++)
                    {
                        bbs[i].gameObject.GetComponent<Renderer>().enabled = !bbs[i].gameObject.GetComponent<Renderer>().enabled;
                    }
                }
            }

            if(Input.GetKeyDown(KeyCode.T) && myCam) // Toggle Direct Lighting Debug
            {
                directLightDebugOn = !directLightDebugOn;

                if(directLightDebugOn)
                    myCam.gIImageEffect.SetFloat("_DebugGILC", 1);
                else
                    myCam.gIImageEffect.SetFloat("_DebugGILC", 0);
            }

            if(Input.GetKeyDown(KeyCode.R) && myCam) // Toggle LightMap Debug
            {
                lightMapDebugOn = !lightMapDebugOn;
                
                if(lightMapDebugOn)
                {
                    myCam.gIImageEffect.SetFloat("_DebugMode", 1);
                    myCam.gIImageEffect.EnableKeyword("_DEBUGMODE_ENABLE");
                    myCam.gIImageEffect.DisableKeyword("_DEBUGMODE_DISABLE");
                }   
                else
                {
                    myCam.gIImageEffect.SetFloat("_DebugMode", 0);
                    myCam.gIImageEffect.EnableKeyword("_DEBUGMODE_DISABLE");
                    myCam.gIImageEffect.DisableKeyword("_DEBUGMODE_ENABLE");
                }
            }

            if(Input.GetKeyDown(KeyCode.H)) // Toggle HUD
            {
                HUDOn = !HUDOn;
                transform.Find("HUD Text").gameObject.SetActive(HUDOn);
            }

            if(modernLightBaker != null && modernScene != null &&
            medievalLightBaker != null && medievalScene != null)
            {
                if(Input.GetKeyDown(KeyCode.Tab) || Input.touchCount >= 4) // Swap scenes
                {
                    pauseWhileLoading = true;
                    insideMedievalScene = !insideMedievalScene;
                    int primitivesCount = primitives.Count;
                    for (int i = 0; i < primitivesCount; i++)
                    {
                        GameObject.Destroy(primitives[0]);
                        primitives.RemoveAt(0);
                    }

                    // Disable one light baker and scene, enable another light baker and scene
                    if(insideMedievalScene)
                    {
                        modernLightBaker.gameObject.SetActive(false);
                        modernScene.gameObject.SetActive(false);

                        medievalLightBaker.gameObject.SetActive(true);
                        medievalScene.gameObject.SetActive(true);
                    }
                    else
                    {
                        medievalLightBaker.gameObject.SetActive(false);
                        medievalScene.gameObject.SetActive(false);

                        modernLightBaker.gameObject.SetActive(true);
                        modernScene.gameObject.SetActive(true);
                    }

                    spotLight.intensity = 0;
                    spotlightOn = false;

                    for (int i = 0; i < myURPData.rendererFeatures.Count; i++)
                    {
                        if(myURPData.rendererFeatures[i] is GIRenderFeature)
                        {
                            // Call all these functions if updating the light map, you don't need
                            // to update the light baker if modifying probes in 1 light baker and 
                            // you only need 1 light baker in a scene.
                            (myURPData.rendererFeatures[i] as GIRenderFeature).UpdateGICameraInfo();
                            (myURPData.rendererFeatures[i] as GIRenderFeature).UpdateBoundingBoxInfo();
                            (myURPData.rendererFeatures[i] as GIRenderFeature).UpdateLightBaker();
                            break;
                        }
                    }
                    
                    // DynamicGI.UpdateEnvironment() updates lighting from skybox
                    if(insideMedievalScene)
                    {
                        DynamicGI.UpdateEnvironment();
                        medievalLightBaker.FullLightBakeRestart();
                    }
                    else
                    {
                        DynamicGI.UpdateEnvironment();
                        modernLightBaker.FullLightBakeRestart();
                    }

                    // Load GI after swapping scenes
                    GILoadingStart();
                    
                }
            }

            // looking around
            if(mouseLookToggle)
            {
                inputRotateAxisX = Input.GetAxisRaw("Mouse X") * 5;
                inputRotateAxisY = Input.GetAxisRaw("Mouse Y") * 5;

                for(int i = 0; i < Input.touchCount; i++)
                {
                    if(Input.touches[i].position.x > Screen.width / 2)
                    {
                        inputRotateAxisX += (Input.touches[i].position.x - (Screen.width / 4 + Screen.width / 2)) / (Screen.width/4);
                        inputRotateAxisY += (Input.touches[i].position.y - Screen.height / 2) / (Screen.height/2);

                        inputRotateAxisX *= 3;
                        inputRotateAxisY *= 3;
                    }
                }
            }

            float rotationX = transform.localEulerAngles.x;
            float newRotationY = transform.localEulerAngles.y + inputRotateAxisX;

            float newRotationX = (rotationX - inputRotateAxisY);
            if (rotationX <= 90.0f && newRotationX >= 0.0f)
                newRotationX = Mathf.Clamp(newRotationX, 0.0f, 90.0f);
            if (rotationX >= 270.0f)
                newRotationX = Mathf.Clamp(newRotationX, 270.0f, 360.0f);

            transform.localRotation = Quaternion.Euler(newRotationX, newRotationY, transform.localEulerAngles.z);

            // moving around
            float moveSpeedUnscaled = Time.unscaledDeltaTime * moveSpeed * speedBoost;

            transform.position += transform.forward * moveSpeedUnscaled * inputVertical;
            transform.position += transform.right * moveSpeedUnscaled * inputHorizontal;
            transform.position += Vector3.up * moveSpeedUnscaled * inputYAxis;
        }

        void OnDestroy ()
        {
            // Reset shader variables on exit
            if(myCam)
            {
                myCam.gIImageEffect.SetFloat("_DebugGILC", 0);
                myCam.gIImageEffect.SetFloat("_DebugMode", 0);
            }
        }

        void GILoadingStart ()
        {
            // Stop input and display loading text
            pauseWhileLoading = true;
            transform.Find("Loading Text").gameObject.SetActive(true);
            transform.Find("HUD Text").gameObject.SetActive(false);
        }

        void GILoadingComplete ()
        {
            StartCoroutine(WaitForComputeShader());
        }

        // Need to wait a short time to allow the compute shader to create light maps
        // after the light baker captures all GI info, then disable loading text / enable input
        IEnumerator WaitForComputeShader ()
        {
            yield return new WaitForSeconds(0.5f);
            pauseWhileLoading = false;
            transform.Find("Loading Text").gameObject.SetActive(false);
            transform.Find("HUD Text").gameObject.SetActive(HUDOn);
        }
    }
}
