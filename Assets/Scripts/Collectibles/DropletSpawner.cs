using System;
using UnityEngine;

namespace HillyWings
{
    // 풀링된 물방울 비주얼 + 충돌 감지. CoinSpawner/SkyOrbSpawner와 동일한
    // 풀링 패턴 사용. 충돌 시 크기에 따라 속도 부스트가 달라지고,
    // 내용물(노란/파란/녹색 코인)이 있으면 추가 효과 발동.
    public class DropletSpawner : MonoBehaviour
    {
        [SerializeField] private int poolSize = 32;

        // 크기별 충돌 반지름 (Small / Medium / Large)
        private static readonly float[] PickupRadii = { 18f, 26f, 36f };
        // 크기별 속도 부스트
        private static readonly float[] SpeedBoosts = { 80f, 160f, 260f };
        // 크기별 시각적 스케일
        private static readonly float[] VisualScales = { 0.65f, 1.0f, 1.5f };

        public event Action<Vector3, string, Color> OnPickupPopup;
        // 내용물 종류별 이벤트 — HUD 픽업 카운터 연결용
        public event Action OnDropletGoldCoin;
        public event Action OnDropletSpeedCoin;
        public event Action OnDropletGreenCoin;

        private static readonly Color DropletColor = new Color(0.35f, 0.72f, 1.0f, 0.90f);
        private static readonly Color GoldColor    = new Color(1f,    0.82f, 0.25f);
        private static readonly Color BlueColor    = new Color(0.25f, 0.65f, 1f);
        private static readonly Color GreenColor   = new Color(0.3f,  0.9f,  0.45f);

        private DropletField field;
        private BirdController bird;
        private Camera cam;
        private PickupBurst burst;

        private SpriteRenderer[] pool;
        private Sprite dropletSprite;

        public void Configure(BirdController birdRef, Camera camera, PickupBurst burstRef, int seed)
        {
            bird   = birdRef;
            cam    = camera;
            burst  = burstRef;
            field  = new DropletField(seed, GameManager.Instance.ScrollX);
        }

        private void Start()
        {
            GameManager.Instance.OnRunStart += HandleRunStart;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnRunStart -= HandleRunStart;
        }

        private void HandleRunStart()
        {
            field = new DropletField(UnityEngine.Random.Range(1, int.MaxValue), GameManager.Instance.ScrollX);
            if (pool != null)
                foreach (var sr in pool) sr.enabled = false;
        }

        private void Awake()
        {
            dropletSprite = ProceduralSprite.CreateCircle(28, DropletColor);

            pool = new SpriteRenderer[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"Droplet_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = dropletSprite;
                sr.sortingOrder = 5;
                sr.enabled = false;
                pool[i] = sr;
            }
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            float leftEdge  = ScreenSpace.LeftEdgeCanvasX(gm.ViewHeight, cam.aspect, cam.orthographicSize);
            float rightEdge = ScreenSpace.RightEdgeCanvasX(gm.ViewHeight, cam.aspect, cam.orthographicSize);
            field.EnsureCoverage(gm.ScrollX + rightEdge + (rightEdge - leftEdge) * 0.2f);

            CheckPickups(gm);
            RenderVisible(gm, leftEdge, rightEdge);
        }

        private void CheckPickups(GameManager gm)
        {
            var entries = field.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Taken) continue;

                int   sizeIdx = (int)e.Size;
                float r  = PickupRadii[sizeIdx];
                float rr = r * r;

                float canvasX = e.WorldX - gm.ScrollX;
                float canvasY = gm.Ground.GroundY(e.WorldX) - e.Offset;
                float dx = canvasX - bird.CanvasX;
                float dy = canvasY - bird.CanvasY;
                if (dx * dx + dy * dy >= rr) continue;

                field.MarkTaken(i);
                Vector3 worldPos = ToWorldPos(gm, canvasX, canvasY);

                // 크기 기반 속도 부스트 + 파티클
                bird.AddSpeedBoost(SpeedBoosts[sizeIdx]);
                burst.Burst(worldPos, DropletColor, 8 + sizeIdx * 3);

                string label = e.Size == DropletField.DropletSize.Large  ? "SPLASH!" :
                               e.Size == DropletField.DropletSize.Medium ? "DROP!"   : "DRIP!";
                OnPickupPopup?.Invoke(worldPos, label, DropletColor);

                FireContent(e.Content, worldPos);
            }
        }

        private void FireContent(DropletField.DropletContent content, Vector3 worldPos)
        {
            switch (content)
            {
                case DropletField.DropletContent.GoldCoin:
                    ScoreManager.Instance.AddScore(3f);
                    burst.Burst(worldPos, GoldColor, 6);
                    OnDropletGoldCoin?.Invoke();
                    break;
                case DropletField.DropletContent.SpeedCoin:
                    bird.AddSpeedBoost(80f);
                    burst.Burst(worldPos, BlueColor, 6);
                    OnDropletSpeedCoin?.Invoke();
                    break;
                case DropletField.DropletContent.GreenCoin:
                    ScoreManager.Instance.AddScore(5f);
                    burst.Burst(worldPos, GreenColor, 6);
                    OnDropletGreenCoin?.Invoke();
                    break;
            }
        }

        private void RenderVisible(GameManager gm, float leftEdge, float rightEdge)
        {
            int used = 0;
            var entries = field.Entries;

            for (int i = 0; i < entries.Count && used < pool.Length; i++)
            {
                var e = entries[i];
                if (e.Taken) continue;

                float canvasX = e.WorldX - gm.ScrollX;
                if (canvasX < leftEdge - 60f || canvasX > rightEdge + 60f) continue;

                float canvasY = gm.Ground.GroundY(e.WorldX) - e.Offset;
                var sr = pool[used++];
                sr.enabled = true;
                sr.transform.localScale = Vector3.one * VisualScales[(int)e.Size];
                sr.transform.position   = ToWorldPos(gm, canvasX, canvasY);
            }

            for (int i = used; i < pool.Length; i++) pool[i].enabled = false;
        }

        private Vector3 ToWorldPos(GameManager gm, float canvasX, float canvasY)
        {
            float localX = ScreenSpace.ToWorldX(canvasX, gm.ViewHeight, cam.aspect);
            float localY = ScreenSpace.ToWorldY(canvasY, gm.ViewHeight);
            return new Vector3(localX, localY, 0f);
        }
    }
}
