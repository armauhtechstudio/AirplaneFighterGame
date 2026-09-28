using UnityEngine;


namespace AirplaneControllerwithShooting
{
    public class RudderControler : MonoBehaviour
    {
        public GameObject Vertical;
        public GameObject Horizontal;
        public GameObject left;
        public GameObject right;

        public float HorizontalMaxAngle;
        public float VerticalMaxAngle;
        public float FlapsMaxAngle;

        private int directionX;
        private int directionY;
        private int directionZ;

        void Update()
        {
            var Joy_pitch = (AirplaneController.Instance.hMove.y) * -3f;
            var Joy_yaw = (AirplaneController.Instance.hMove.x / 3) * 3;
            var Joy_roll = (AirplaneController.Instance.hMove.x / 3);

            if (Joy_yaw < 0.2f)
            {
                directionZ = 1;
            }

            if (Joy_yaw > 0.2f)
            {
                directionZ = 2;
            }

            if (Joy_roll < 0.2f)
            {
                directionX = 6;
            }

            if (Joy_roll > 0.2f)
            {
                directionX = 5;
            }

            if (Joy_pitch < 0.2f)
            {
                directionY = 3;
            }

            if (Joy_pitch > 0.2f)
            {
                directionY = 4;
            }

            if (directionZ == 1)
            {
                Vertical.transform.localRotation = Quaternion.Euler(0f, VerticalMaxAngle * -1f * Joy_yaw, 0f);
            }
            if (directionZ == 2)
            {
                Vertical.transform.localRotation = Quaternion.Euler(0f, VerticalMaxAngle * -1f * Joy_yaw, 0f);
            }
            if (directionY == 3)
            {
                Horizontal.transform.localRotation = Quaternion.Euler(0f, 0f, HorizontalMaxAngle * -1f * Joy_pitch);
            }
            if (directionY == 4)
            {
                Horizontal.transform.localRotation = Quaternion.Euler(0f, 0f, HorizontalMaxAngle * -1f * Joy_pitch);

            }
            if (directionX == 5)
            {
                left.transform.localRotation = Quaternion.Euler(FlapsMaxAngle * -1f * Joy_roll, -90f, 0f);
                right.transform.localRotation = Quaternion.Euler(FlapsMaxAngle * Joy_roll, -90f, 0f);
            }
            if (directionX == 6)
            {
                left.transform.localRotation = Quaternion.Euler(FlapsMaxAngle * -1f * Joy_roll, -90f, 0f);
                right.transform.localRotation = Quaternion.Euler(FlapsMaxAngle * Joy_roll, -90f, 0f);
            }
        }
    }
}
