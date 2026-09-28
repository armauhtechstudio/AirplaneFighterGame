using UnityEngine;
using System.Collections;

namespace AirplaneControllerwithShooting
{
    public class SmoothFollowUsual : MonoBehaviour
    {
        public Transform target;
        public float distance = 10.0f;
        public float height = 5.0f;
        public float heightDamping = 2.0f;
        public float rotationDamping = 3.0f;
        public float movementDamping = 1.0f;
        private Vector3 lastLocation;
        [AddComponentMenu("Camera-Control/Smooth Follow")]
        public float fCamShakeImpulse = 0.0f;

        public static SmoothFollowUsual Instance;


        private void Awake()
        {
            Instance = this;
        }


        void LateUpdate()
        {
            if (!target)
                return;

            Quaternion currentRotation = transform.rotation;
            Quaternion wantedRotation = target.rotation;
            Quaternion newRotation = Quaternion.Lerp(currentRotation, wantedRotation, rotationDamping * Time.deltaTime);
            transform.rotation = newRotation;
            transform.position = target.position - (transform.forward * distance) + (target.up * height);

            if (fCamShakeImpulse > 0.0f)
                shakeCamera();
        }

        public void shakeCamera()
        {
            Camera.main.transform.position += new Vector3(Random.Range(-fCamShakeImpulse, fCamShakeImpulse) / 4, Random.Range(-fCamShakeImpulse, fCamShakeImpulse) / 4, Random.Range(-fCamShakeImpulse, fCamShakeImpulse) / 4);
            fCamShakeImpulse -= Time.deltaTime * fCamShakeImpulse * 4.0f;
            if (fCamShakeImpulse < 0.01f)
            {
                fCamShakeImpulse = 0.0f;
            }
        }
    }
}