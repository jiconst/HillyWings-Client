using System.Collections.Generic;
using UnityEngine;

namespace HillyWings
{
    // 풍선 배치 — 순수 데이터 (MonoBehaviour 없음).
    // Island 2 부터 출현. 섬이 높아질수록 밀도와 크기 증가.
    // 풍선은 지면에서 floatHeight 위에 떠 있으며, BalloonSpawner 가
    // GroundSampler 를 통해 실제 Y 좌표를 계산한다.
    public class BalloonField
    {
        public struct BalloonEntry
        {
            public float WorldX;
            public float FloatHeight; // 지면 위 높이 (canvas-space, groundY에서 뺌)
            public float Radius;      // 시각/충돌 반지름
            public bool  Popped;
        }

        // 최초 풍선 출현 섬 번호
        private const int   StartIsland   = 2;
        private const float IslandLength  = 25000f;

        private readonly System.Random rng;
        private readonly List<BalloonEntry> entries = new List<BalloonEntry>();
        private float lastBalloonX;

        public IReadOnlyList<BalloonEntry> Entries => entries;

        public BalloonField(int seed, float startX)
        {
            rng          = new System.Random(seed);
            lastBalloonX = Mathf.Max(startX, IslandLength * (StartIsland - 1));
        }

        public void EnsureCoverage(float uptoWorldX)
        {
            while (lastBalloonX < uptoWorldX)
            {
                int island = Mathf.Max(1, Mathf.FloorToInt(lastBalloonX / IslandLength) + 1);

                // 섬이 높아질수록 풍선 간격을 좁혀 밀도 상승
                float minGap = Mathf.Max(1200f, 3200f - island * 180f);
                float maxGap = Mathf.Max(2000f, 5000f - island * 200f);
                lastBalloonX += minGap + (float)rng.NextDouble() * (maxGap - minGap);

                // 지면 위 부유 높이: 초반엔 낮게, 후반엔 조금 높게
                float floatMin = 40f + island * 4f;
                float floatMax = 80f + island * 6f;
                float fh = floatMin + (float)rng.NextDouble() * (floatMax - floatMin);

                // 반지름: 초반 작고 후반 조금 크게 (최소 20, 최대 36)
                float rad = Mathf.Clamp(20f + island * 1.5f, 20f, 36f);

                entries.Add(new BalloonEntry
                {
                    WorldX      = lastBalloonX,
                    FloatHeight = fh,
                    Radius      = rad
                });
            }
        }

        public void MarkPopped(int index)
        {
            var e    = entries[index];
            e.Popped = true;
            entries[index] = e;
        }
    }
}
