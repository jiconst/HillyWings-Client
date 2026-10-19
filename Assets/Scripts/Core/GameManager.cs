using System;
using UnityEngine;

namespace HillyWings
{
    // Owns run-wide shared state that terrain, physics, and scoring all
    // read/advance: how far the world has scrolled, which island we're on,
    // and the reference viewport height the ported formulas are tuned
    // against. Singleton per project convention (GameManager/ScoreManager/
    // SaveSystem only).
    //
    // Also owns the Start/Playing/DayOver state machine (M4). BeginRun()
    // resets scroll/island and generates a fresh terrain seed, then fires
    // OnRunStart -- every other system (bird, score, streak, fever, coins,
    // clouds) subscribes to that event to reset itself. GameManager doesn't
    // know any of their details; it just broadcasts.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // 섬 하나의 길이 — 기본 속도(360) 기준 약 14초, 90초 낮 동안 최대 6~7섬 도달 가능
        private const float IslandLength = 5000f;

        [SerializeField] private float viewHeight = 720f;

        public float ViewHeight => viewHeight;
        public float ScrollX { get; private set; }
        public int Island { get; private set; } = 1;
        // NestBonus (M5): permanent +N to the starting multiplier, earned by
        // completing all 3 Nest Multiplier objectives in a run. GameManager
        // just holds the number -- NestMultiplier owns the why/persistence.
        public int NestBonus { get; set; }
        public int Multiplier => 10 + NestBonus + (Island - 1) * 2;
        public GameState State { get; private set; } = GameState.Start;
        // 0~1, 다음 섬까지 얼마나 남았는지 -- HUD의 섬 진행 바("다음 판까지 얼마나
        // 가야 하는지 알 수가 없다" 피드백으로 신설)가 읽음.
        public float IslandProgress => islandDistance / IslandLength;
        // 다음 섬(스테이지)까지 남은 실제 거리(월드 유닛) -- "레벨업 기준"을
        // 눈에 보이는 숫자로 드러냄: 이 값이 0이 되는 순간(AdvanceScroll에서
        // Island가 올라가는 바로 그 조건) 레벨업. HUD의 "남은 거리" 표시가 읽음.
        public float IslandRemainingDistance => IslandLength - islandDistance;

        // Single shared terrain instance -- TerrainGenerator and BirdPhysics
        // both query this so the rendered hill and the physics ground line
        // never disagree (see the earlier island-desync bug for why this
        // matters).
        public GroundSampler Ground { get; private set; }

        public event Action<int> OnIslandAdvanced;
        public event Action OnRunStart;
        // Fired once when the day-length timer runs out, before State
        // becomes DayOver -- all of that run's stats are still live at this
        // point. NestMultiplier/CoinWallet hook run-end bookkeeping here.
        public event Action OnRunEnd;

        private float islandDistance;

        private void Awake()
        {
            Instance = this;
        }

        public void Configure(float viewHeightValue, int terrainSeed)
        {
            viewHeight = viewHeightValue;
            Ground = new GroundSampler(terrainSeed, viewHeight);
        }

        // TODAY 모드: UTC 날짜 기반 고정 씨앗 -- 같은 날은 세계 어디서나 같은 지형
        public static int TodaySeed()
        {
            var now = DateTime.UtcNow;
            return now.Year * 10000 + now.DayOfYear;
        }

        // seed 지정 시 해당 씨앗(TODAY 모드 등), null이면 매 런 랜덤
        public void BeginRun(int? seed = null)
        {
            ScrollX = 0f;
            Island = 1;
            islandDistance = 0f;
            int actualSeed = seed ?? UnityEngine.Random.Range(1, int.MaxValue);
            Ground = new GroundSampler(actualSeed, viewHeight);
            State = GameState.Playing;
            OnRunStart?.Invoke();
        }

        // Called by DayCycle when the day-length timer runs out.
        public void EndRun()
        {
            OnRunEnd?.Invoke();
            State = GameState.DayOver;
        }

        // Called by DayOverScreen ("홈").
        public void ReturnToStart()
        {
            State = GameState.Start;
        }

        public void AdvanceScroll(float delta)
        {
            ScrollX += delta;
            islandDistance += delta;

            if (islandDistance >= IslandLength)
            {
                islandDistance -= IslandLength;
                Island++;
                OnIslandAdvanced?.Invoke(Island);
            }
        }
    }
}
