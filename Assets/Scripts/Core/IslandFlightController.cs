using UnityEngine;

namespace HillyWings
{
    // 섬 끝 발사대 구간에서 새가 공중에 뜨면 "섬 전환 비행"을 발동.
    // GroundSampler 가 각 섬 경계 앞에 골짜기+발사대를 심어 두므로,
    // 발사대 근처에서 충분한 속도로 날아오르는 것 자체가 트리거 조건.
    //
    // 비행이 끝나면 BirdController.OnIslandFlightEnd → DayCycle.RewindElapsedTime 으로
    // 비행 시간만큼 낮을 되감아 긴장감을 완화해 준다.
    public class IslandFlightController : MonoBehaviour
    {
        // 섬 경계 앞 이 거리 안에 있을 때 발동 허용
        [SerializeField] private float launchZoneDist = 2200f;
        // 발동 최소 속도 — 적당히 빠를 때만 비행 연출을 준다
        [SerializeField] private float minTriggerSpeed = 420f;
        // 비행 지속 시간(초)
        [SerializeField] private float flightDuration = 5.5f;
        // 비행 종료 후 낮을 되감는 양(초). 비행 시간보다 조금 짧게 설정해 여운을 남김.
        [SerializeField] private float rewindSeconds = 5f;

        // 섬 길이 — GameManager.IslandLength 와 동기화
        private const float IslandLength = 25000f;

        private BirdController bird;
        private DayCycle dayCycle;
        private bool triggeredThisIsland;

        public void Configure(BirdController birdRef, DayCycle dayCycleRef)
        {
            bird     = birdRef;
            dayCycle = dayCycleRef;
        }

        private void Start()
        {
            GameManager.Instance.OnIslandAdvanced += HandleIslandAdvanced;
            GameManager.Instance.OnRunStart       += HandleRunStart;
            bird.OnIslandFlightEnd                += HandleFlightEnd;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnIslandAdvanced -= HandleIslandAdvanced;
                GameManager.Instance.OnRunStart       -= HandleRunStart;
            }
            if (bird != null)
                bird.OnIslandFlightEnd -= HandleFlightEnd;
        }

        private void HandleRunStart()      => triggeredThisIsland = false;
        private void HandleIslandAdvanced(int _) => triggeredThisIsland = false;

        private void HandleFlightEnd() => dayCycle.RewindElapsedTime(rewindSeconds);

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (gm.State != GameState.Playing) return;
            if (triggeredThisIsland) return;
            if (bird.IsIslandFlightActive) return;

            // 현재 위치에서 다음 섬 경계까지의 거리
            float worldBirdX = gm.ScrollX + bird.CanvasX;
            int   nextBIdx   = Mathf.CeilToInt(worldBirdX / IslandLength);
            float nextBX     = nextBIdx * IslandLength;
            float distToBoundary = nextBX - worldBirdX;

            if (distToBoundary > launchZoneDist) return;

            // 발사대 위에서 날아오를 때 발동
            if (!bird.Airborne || bird.Speed < minTriggerSpeed) return;

            triggeredThisIsland = true;
            bird.StartIslandFlight(flightDuration);
        }
    }
}
