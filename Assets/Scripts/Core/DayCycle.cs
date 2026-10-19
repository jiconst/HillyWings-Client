using System;
using UnityEngine;

namespace HillyWings
{
    // Reference: DAY_LENGTH = 90s, dayTime 0..1, endGame() when it hits 1.
    // Only ticks while GameManager.State == Playing; resets on OnRunStart.
    //
    // Tiny Wings 방식 속도 연동: 빠르게 날수록 해가 느리게 움직이고,
    // SpeedBase 이상으로 충분히 빠르면 해가 뒤로 밀린다 — 잘 플레이할수록 낮이 연장됨.
    public class DayCycle : MonoBehaviour
    {
        [SerializeField] private float dayDuration = 90f;
        // 기준 속도 (BirdPhysics.SpeedBase와 일치) — 이 속도에서 낮이 정상 속도로 흐름
        [SerializeField] private float speedRef = 360f;
        // 속도 1 unit 차이당 낮 진행 배율 조절량.
        // 예: 0.002 → SpeedMax(980)에서 rate = 1 - (980-360)*0.002 = -0.24 (해가 뒤로 밀림)
        [SerializeField] private float speedInfluence = 0.002f;
        // 섬 경계 통과 시 해를 뒤로 되돌리는 시간(초). 리셋이 아니라 부분 보상.
        [SerializeField] private float islandTimeReward = 30f;

        public float DayTime { get; private set; }
        public float ElapsedTime { get; private set; }

        public event Action OnDayOver;

        private BirdController bird;

        // GameBootstrapper에서 BirdController 참조 전달
        public void Configure(BirdController birdController)
        {
            bird = birdController;
        }

        private void Start()
        {
            GameManager.Instance.OnRunStart += HandleRunStart;
            GameManager.Instance.OnIslandAdvanced += HandleIslandAdvanced;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnRunStart -= HandleRunStart;
                GameManager.Instance.OnIslandAdvanced -= HandleIslandAdvanced;
            }
        }

        private void HandleRunStart()
        {
            ElapsedTime = 0f;
            DayTime = 0f;
        }

        // 섬 경계 통과 시 해를 islandTimeReward 초만큼 되돌림 (리셋이 아닌 부분 보상)
        private void HandleIslandAdvanced(int island)
        {
            ElapsedTime = Mathf.Max(0f, ElapsedTime - islandTimeReward);
            DayTime = Mathf.Min(1f, ElapsedTime / dayDuration);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm.State != GameState.Playing) return;

            // 속도가 빠를수록 낮 진행이 느려짐. 단, 속도가 아무리 빨라도 낮은 항상 밤 방향으로만
            // 진행 (dayRate >= 0 클램프). 해가 뒤로 가는 것은 섬 경계 통과 시에만 발생.
            float currentSpeed = bird != null ? bird.Speed : speedRef;
            float dayRate = Mathf.Max(0f, 1f - (currentSpeed - speedRef) * speedInfluence);

            ElapsedTime += Time.deltaTime * dayRate;

            DayTime = Mathf.Min(1f, ElapsedTime / dayDuration);

            if (DayTime >= 1f)
            {
                gm.EndRun();
                OnDayOver?.Invoke();
            }
        }
    }
}
