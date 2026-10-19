using System.Collections.Generic;
using UnityEngine;

namespace HillyWings
{
    // Coin placement, ported 1:1 from hilly-wings.html's ensureCoins().
    // Pure data (no MonoBehaviour) -- CoinSpawner owns pooled visuals and
    // collision, reading this list. Canvas-space convention.
    public class CoinField
    {
        public enum CoinType { Coin, Speed }

        public struct CoinEntry
        {
            public float WorldX;
            public float Offset; // height above ground, canvas-space (subtracted from groundY)
            public CoinType Type;
            public bool Taken;
        }

        // GameManager.IslandLength / GroundSampler.IslandLength 와 반드시 동기화 유지
        private const float IslandLength = 25000f;
        // 섬 전환 발사대 오르막 구간에 아치형으로 밀집 배치할 코인 구간 파라미터
        private const float CoinTransitionTriggerDist = 1200f; // 경계 앞 이 거리부터 아치 삽입

        private readonly System.Random rng;
        private readonly List<CoinEntry> entries = new List<CoinEntry>();
        private float lastCoinX;
        private int lastTransitionedCoinBoundary; // 마지막으로 전환 구간 아치를 삽입한 섬 경계 인덱스

        public IReadOnlyList<CoinEntry> Entries => entries;

        public CoinField(int seed, float startX)
        {
            rng = new System.Random(seed);
            lastCoinX = startX;
        }

        public void EnsureCoverage(float uptoWorldX)
        {
            while (lastCoinX < uptoWorldX)
            {
                // 섬 전환 오르막 구간 — 골짜기→발사대 사이에 코인 아치 밀집 배치.
                // 플레이어가 가속하며 올라오는 구간에 자연스럽게 보상을 제공.
                int nextBIdx = Mathf.Max(1, Mathf.CeilToInt(lastCoinX / IslandLength));
                float nextBX = nextBIdx * IslandLength;
                float distToB = nextBX - lastCoinX;
                if (lastTransitionedCoinBoundary < nextBIdx && distToB < CoinTransitionTriggerDist && lastCoinX < nextBX - 200f)
                {
                    float zoneStart = Mathf.Max(lastCoinX, nextBX - 1100f);
                    float zoneEnd   = nextBX - 300f;
                    if (zoneEnd > zoneStart)
                    {
                        int arcCount = 8 + (int)(rng.NextDouble() * 5); // 8~12개
                        float span = zoneEnd - zoneStart;
                        for (int i = 0; i < arcCount; i++)
                        {
                            float frac = (float)(i + 1) / (arcCount + 1);
                            float arch = Mathf.Sin(frac * Mathf.PI); // 0→1→0 아치
                            entries.Add(new CoinEntry {
                                WorldX = zoneStart + frac * span,
                                Offset = 15f + arch * 22f,
                                Type   = (rng.NextDouble() < 0.28) ? CoinType.Speed : CoinType.Coin
                            });
                        }
                    }
                    lastCoinX = nextBX - 100f;
                    lastTransitionedCoinBoundary = nextBIdx;
                    continue;
                }

                lastCoinX += 90f + (float)rng.NextDouble() * 60f;
                double roll = rng.NextDouble();

                if (roll < 0.10)
                {
                    // Reference offset (60-100) assumed taller hills/higher
                    // launches than our current tuning reaches -- lowered so
                    // it's actually catchable mid-flight. Lowered again
                    // (28-54 -> 14-26) per "언덕에 가급적 붙어 있었으면" feedback.
                    entries.Add(new CoinEntry
                    {
                        WorldX = lastCoinX,
                        Offset = 14f + (float)rng.NextDouble() * 12f,
                        Type = CoinType.Speed
                    });
                }
                else if (roll < 0.40)
                {
                    // "노란색 골드나 나오는 횟수를 줄이고" 피드백 -- 골드 코인 런이
                    // 걸릴 확률을 0.65(0.10~0.75)에서 0.30(0.10~0.40)으로 낮춤,
                    // 그만큼 아무것도 없는 구간(roll>=0.40)이 늘어남. 높이도
                    // 42~78 -> 18~34로 낮춰서 언덕에 더 붙어 있게 함.
                    int run = 3 + (int)(rng.NextDouble() * 4);
                    for (int i = 0; i < run; i++)
                    {
                        float off = 18f + Mathf.Sin((float)i / run * Mathf.PI) * 16f;
                        entries.Add(new CoinEntry
                        {
                            WorldX = lastCoinX + i * 38f,
                            Offset = off,
                            Type = CoinType.Coin
                        });
                    }
                    lastCoinX += run * 38f;
                }
                // else (roll >= 0.40): a gap, no coins this cycle.
            }
        }

        public void MarkTaken(int index)
        {
            var e = entries[index];
            e.Taken = true;
            entries[index] = e;
        }
    }
}
