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
        // 아무리 빠른 속도에서도 낮이 완전히 멈추지 않도록 하는 최소 진행 배율.
        // 이 값이 없으면 고속 유지만으로 무한 플레이가 가능해짐.
        [SerializeField] private float minDayRate = 0.2f;
        // 섬을 넘을 때마다 낮 진행 배율에 곱해지는 추가 배율 — 후반 난이도 상승.
        // 예: 0.12 → Island 5에서 낮이 1.48배 빠르게 진행
        [SerializeField] private float islandDayRateScale = 0.12f;
        // 섬 경계 통과 시 해를 뒤로 되돌리는 기본 시간(초). 리셋이 아니라 부분 보상.
        [SerializeField] private float islandTimeReward = 25f;
        // 섬을 넘을 때마다 보상이 줄어드는 양(초). 후반으로 갈수록 버티기 어려워짐.
        // 예: 2.0 → Island 1: 25s, Island 5: 17s, Island 10: 7s, Island 13+: 0s
        [SerializeField] private float islandRewardDecay = 2f;

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

        // 섬 경계 통과 시 해를 되돌림. 보상은 섬 번호가 높아질수록 줄어들어 후반이 빡빡해짐.
        private void HandleIslandAdvanced(int island)
        {
            float reward = Mathf.Max(0f, islandTimeReward - (island - 1) * islandRewardDecay);
            ElapsedTime = Mathf.Max(0f, ElapsedTime - reward);
            DayTime = Mathf.Min(1f, ElapsedTime / dayDuration);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm.State != GameState.Playing) return;

            // 속도가 빠를수록 낮 진행이 느려짐. 단 minDayRate 이하로는 내려가지 않아
            // 최고 속도에서도 낮이 완전히 멈추지 않음 — 무한 플레이 방지.
            // 섬 번호가 높아질수록 islandDayRateScale만큼 배율이 올라 후반 난이도가 상승.
            float currentSpeed = bird != null ? bird.Speed : speedRef;
            float baseRate = Mathf.Max(minDayRate, 1f - (currentSpeed - speedRef) * speedInfluence);
            float islandMult = 1f + (gm.Island - 1) * islandDayRateScale;
            float dayRate = baseRate * islandMult;

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
