using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GIProbesRuntime
{
    [DisallowMultipleComponent]
    public class GIProbeProperties : MonoBehaviour
    {
        public enum GIInfluenceType
        {
            Default,
            Overwrite,
            Multiply,
            Add,
            Divide,
            Subtract,
            Lighten,
            Darken,
        }
        [Tooltip("Method used for applying the GI Influence Color to the GI Probe. Default means no color influence.")]
        public GIInfluenceType gIInfluenceType;

        [Tooltip("Color used for coloring this GI Probe. Alpha controls the strength of the influence type.")]
        public Color gIInfluenceColor = Color.white;

        [Tooltip("The minimum distance this GI Probe has an affect in world space. Normally the minimum distance is the minimum distance to the nearest GI Probe.")]
        public float influenceMinDistance = 10;

        bool quiting = false;

        void Awake ()
        {
            if(Time.time > 2)
            {
                if(transform.parent != null)
                {
                    if(transform.parent.gameObject.GetComponent<GILightBaker>())
                    {
                        transform.parent.gameObject.GetComponent<GILightBaker>().
                        AddGIProbe(gameObject);
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
            StartCoroutine(ISceneChange());
        }

        IEnumerator ISceneChange ()
        {
            yield return new WaitForSeconds(0.2f);
            SceneManager.activeSceneChanged += SceneChange;
        }


        void OnApplicationQuit() 
        {
            quiting = true;
        }

        void SceneChange(Scene a, Scene b)
        {
            quiting = true;
        }

        void OnDestroy() 
        {
            if(quiting == false)
            {
                transform.parent.gameObject.GetComponent<GILightBaker>().
                RemoveGIProbe(gameObject);
            }
        }
    }
}
