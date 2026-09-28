using UnityEngine;

namespace AirplaneControllerwithShooting
{
    public class PropellerScript : MonoBehaviour
    {
        public Vector3 rotationDirection;
        public float rotationSpeed = 10;
        void FixedUpdate()
        {
            transform.Rotate(rotationDirection, AirplaneController.Instance.Speed * rotationSpeed);
        }
    }
}
