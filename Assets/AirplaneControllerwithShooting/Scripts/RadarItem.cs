
using UnityEngine;

namespace AirplaneControllerwithShooting
{
    public class RadarItem : MonoBehaviour
    {
        public RadarTargetType TargetType;

        void Start()
        {
            if (RadarSystem.Instance != null)
                RadarSystem.Instance.AddTarget(this);
        }
    }
}