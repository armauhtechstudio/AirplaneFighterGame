using System.Collections.Generic;
using UnityEngine;

namespace AirplaneControllerwithShooting
{
    [CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/PlaneConfiguration", order = 1)]
    public class PlaneConfiguration : ScriptableObject
    {
        public PlaneType planeType;
        public int TopSpeed;
        public int Health;
        public int MachineGunDamage;
        public int MachineGunAmmoCapacity;
        public int RocketLauncherDamage;
        public int RocketLauncherAmmoCapacity;
        public int FuelTankCapacity;
        public bool HasEngine;
    }
}