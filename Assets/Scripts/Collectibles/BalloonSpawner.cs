using System;
using UnityEngine;

namespace HillyWings
{
    // 풀링된 풍선 비주얼 + 충돌 감지.
    // 풍선에 맞으면 BirdController.AddVerticalBoost 로 위쪽 힘을 가해
    // 반발 점프를 구현한다. canvas-y 는 아래 방향이므로 음수 값 = 위쪽.
    public class BalloonSpawner : MonoBehaviour
    {
        [SerializeField] private int   poolSize     = 24;
        // 충돌 시 위로 가하는 canvas-y 속도 증분 (음수 = 위쪽)
        [SerializeField] private float bouncePower  = -560f;
        // 풍선 팝 시 속도 소량 추가로 활기찬 반응
        [SerializeField] private float popSpeedAdd  = 60f;

        public event Action<Vector3, string, Color> OnPickupPopup;

        private static readonly Color BalloonColor = new Color(1f, 0.35f, 0.45f, 0.90f);

        private BalloonField    field;
        private BirdController  bird;
        private Camera          cam;
        private PickupBurst     burst;

        private SpriteRenderer[] pool;
        private Sprite           balloonSprite;

        public void Configure(BirdController birdRef, Camera camera, PickupBurst burstRef, int seed)
        {
            bird  = birdRef;
            cam   = camera;
            burst = burstRef;
            field = new BalloonField(seed, GameManager.Instance.ScrollX);
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
            field = new BalloonField(UnityEngine.Random.Range(1, int.MaxValue), GameManager.Instance.ScrollX);
            if (pool != null)
                foreach (var sr in pool) sr.enabled = false;
        }

        private void Awake()
        {
            balloonSprite = ProceduralSprite.CreateCircle(32, BalloonColor);

            pool = new SpriteRenderer[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"Balloon_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite       = balloonSprite;
                sr.sortingOrder = 6;
                sr.enabled      = false;
                pool[i] = sr;
            }
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            float leftEdge  = ScreenSpace.LeftEdgeCanvasX (gm.ViewHeight, cam.aspect, cam.orthographicSize);
            float rightEdge = ScreenSpace.RightEdgeCanvasX(gm.ViewHeight, cam.aspect, cam.orthographicSize);
            field.EnsureCoverage(gm.ScrollX + rightEdge + (rightEdge - leftEdge) * 0.2f);

            CheckPops(gm);
            RenderVisible(gm, leftEdge, rightEdge);
        }

        private void CheckPops(GameManager gm)
        {
            var entries = field.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Popped) continue;

                float r       = e.Radius;
                float canvasX = e.WorldX - gm.ScrollX;
                float canvasY = gm.Ground.GroundY(e.WorldX) - e.FloatHeight;
                float dx = canvasX - bird.CanvasX;
                float dy = canvasY - bird.CanvasY;
                if (dx * dx + dy * dy >= r * r) continue;

                field.MarkPopped(i);
                Vector3 worldPos = ToWorldPos(gm, canvasX, canvasY);

                // 반발 점프: 위쪽 속도 + 소량 수평 가속
                bird.AddVerticalBoost(bouncePower);
                bird.AddSpeedBoost(popSpeedAdd);
                burst.Burst(worldPos, BalloonColor, 12);
                OnPickupPopup?.Invoke(worldPos, "POP!", BalloonColor);
            }
        }

        private void RenderVisible(GameManager gm, float leftEdge, float rightEdge)
        {
            int used    = 0;
            var entries = field.Entries;

            for (int i = 0; i < entries.Count && used < pool.Length; i++)
            {
                var e = entries[i];
                if (e.Popped) continue;

                float canvasX = e.WorldX - gm.ScrollX;
                if (canvasX < leftEdge - 80f || canvasX > rightEdge + 80f) continue;

                float canvasY = gm.Ground.GroundY(e.WorldX) - e.FloatHeight;
                var sr = pool[used++];
                sr.enabled            = true;
                sr.transform.localScale = Vector3.one * (e.Radius / 28f); // 반지름 → 스케일 변환
                sr.transform.position   = ToWorldPos(gm, canvasX, canvasY);
            }

            for (int i = used; i < pool.Length; i++) pool[i].enabled = false;
        }

        private Vector3 ToWorldPos(GameManager gm, float canvasX, float canvasY)
        {
            float lx = ScreenSpace.ToWorldX(canvasX, gm.ViewHeight, cam.aspect);
            float ly = ScreenSpace.ToWorldY(canvasY, gm.ViewHeight);
            return new Vector3(lx, ly, 0f);
        }
    }
}
