using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;

namespace AirplaneControllerwithShooting
{
    public class RadarSystem : MonoBehaviour
    {
        public static RadarSystem Instance;

        public Transform PlayerTarget;
        public Transform PlayerCamera;
        public float RadarDistance;
        public Image Background;
        public Transform PlayerView;
        public RectTransform RadarTarget;
        public RectTransform RadarPoint;
        public Transform Root;
        public RadarTypeInfo[] RadarTypeInfo;
        public Dictionary<GameObject, TargetMap> TargetList = new Dictionary<GameObject, TargetMap>();

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (!PlayerCamera)
                PlayerCamera = AirplaneSystemManager.Instance.GetCamera();
        }

        // Once per rendered frame (was FixedUpdate, which can run several times per frame)
        private void LateUpdate()
        {
            if (PlayerTarget == null) return;
            transform.eulerAngles = new Vector3(90f, PlayerTarget.eulerAngles.y, 0f);
            RadarDraw();
        }
        public void AddTarget(RadarItem item)
        {
            if (item == null) return;
            AddTarget(item.gameObject, item.TargetType);
        }
        private void AddTarget(GameObject item, RadarTargetType type)
        {
            if (item == null) return;
            if (TargetList.ContainsKey(item)) return;
            var targetInfo = CreateEnemyInfo(type);
            TargetList.Add(item, targetInfo);
        }

        public void RemoveTarget(GameObject item)
        {
            foreach (var target in TargetList)
            {
                if (target.Key != null && target.Key == item)
                {
                    target.Value.TargetPoint.gameObject.SetActive(false);
                    TargetList.Remove(item);
                    break;
                }
            }
            if(TargetList.Where(x => x.Key.CompareTag("Enemy")).Count() == 0)
            {
                // All Enemies are destroyed.
                if (AirplaneSystemManager.Instance.EventToInvokeWhenAllEnemiesDestroyed != null)
                {
                    AirplaneSystemManager.Instance.EventToInvokeWhenAllEnemiesDestroyed.Invoke();
                }
            }
        }

        private TargetMap CreateEnemyInfo(RadarTargetType type)
        {
            var enemyInfo = new TargetMap
            {
                TargetPoint = (RectTransform)Instantiate(RadarPoint, new Vector3(0, 0, 0), Quaternion.identity)
            };
            var radarTypeInfo = GetIconInfo(type);
            enemyInfo.TargetPoint.transform.SetParent(Root);
            enemyInfo.TargetPoint.localPosition = new Vector3(0, 0, 0);
            enemyInfo.TargetPoint.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            enemyInfo.TargetPoint.GetComponent<Image>().sprite = radarTypeInfo.Icon;
            return enemyInfo;
        }
        private RadarTypeInfo GetIconInfo(RadarTargetType type)
        {
            for (int i = 0; i < RadarTypeInfo.Length; i++)
            {
                if (RadarTypeInfo[i].Type == type)
                    return RadarTypeInfo[i];
            }

            return RadarTypeInfo[0];
        }

        private Camera radarCamera;

        // Same result as before, but cheap enough for hundreds of radar items (rocks, house pieces...):
        // the radar camera used to be moved to every single target each physics step. Looking straight
        // down from RadarDistance above a target, the target's screen position only depends on its
        // horizontal offset from the player, so the camera is placed once and every target is projected
        // at the player's height.
        private void RadarDraw()
        {
            if (radarCamera == null) radarCamera = GetComponent<Camera>();

            PlayerView.localRotation = Quaternion.AngleAxis(PlayerCamera.eulerAngles.y - PlayerTarget.eulerAngles.y, new Vector3(0, 0, -1));
            radarCamera.rect = new Rect(0, 0, 200f / Screen.width, 200f / Screen.height);

            Vector3 player = PlayerTarget.position;
            transform.position = new Vector3(player.x, player.y + RadarDistance, player.z);

            // Old check: distance from (player.x, target.y + R, player.z) to target < 1.05 R
            // => horizontal distance² < (1.05² - 1) R²
            float maxFlatSqr = (1.05f * 1.05f - 1f) * RadarDistance * RadarDistance;
            float size = Background.rectTransform.sizeDelta.x;

            foreach (var enemy in TargetList)
            {
                if (enemy.Key == null) continue;

                Vector3 target = enemy.Key.transform.position;
                float dx = target.x - player.x, dz = target.z - player.z;
                bool inRange = dx * dx + dz * dz < maxFlatSqr;
                GameObject dot = enemy.Value.TargetPoint.gameObject;

                if (inRange)
                {
                    Vector3 screenPos = radarCamera.WorldToScreenPoint(new Vector3(target.x, player.y, target.z));
                    enemy.Value.TargetPoint.localPosition = new Vector3((screenPos.x - size) / 2, (screenPos.y - size) / 2);
                }
                if (dot.activeSelf != inRange) dot.SetActive(inRange);
            }
        }

        public void SetPlayer(Transform player)
        {
            transform.parent.gameObject.SetActive(true);
            PlayerTarget = player.transform;
        }

        public void DeletePlayer(Transform player)
        {
            transform.parent.gameObject.SetActive(false);
            PlayerTarget = null;
        }
    }

    public class TargetMap
    {
        public RectTransform TargetPoint;
    }
}
