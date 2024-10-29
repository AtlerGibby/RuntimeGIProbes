using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GIProbesRuntime
{
    public class DemoSecurityCamera : MonoBehaviour
    {
        float currentAngle;
        Vector3 currentRot;
        Vector3 newRot;
        float time;
        [Tooltip("Camera rotation angle.")]
        public float targetAngle = 90f;
        [Tooltip("Camera rotation speed.")]
        public float speed = 0.2f;

        // Start is called before the first frame update
        void Start()
        {
            currentAngle = transform.rotation.eulerAngles.y;
            currentRot = transform.rotation.eulerAngles;
            newRot = new Vector3(transform.rotation.eulerAngles.x, currentAngle + targetAngle, transform.rotation.eulerAngles.z);
            time = Random.value * 2 -1;
        }

        // Update is called once per frame
        void Update()
        {
            // Simply rotates around slowly
            time += Time.deltaTime * speed;
            transform.rotation = Quaternion.Lerp(Quaternion.Euler(currentRot), Quaternion.Euler(newRot), (Mathf.Sin(time) + 0.9f) / 2);
        }
    }
}
