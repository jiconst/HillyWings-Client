using System.Collections.Generic;
using UnityEngine;

namespace HillyWings
{
    // 언덕 위 물방울 배치 — 순수 데이터 (MonoBehaviour 없음).
    // DropletSpawner가 풀링된 비주얼과 충돌 처리를 담당하며 이 목록을 읽음.
    // 섬 번호가 높아질수록 물방울 밀도·크기·코인 함유 확률이 증가.
    public class DropletField
    {
        public enum DropletSize { Small, Medium, Large }
        public enum DropletContent { None, GoldCoin, SpeedCoin, GreenCoin }

        public struct DropletEntry
        {
            public float WorldX;
            public float Offset;    // 지면에서 위쪽 거리 (canvas-space, groundY에서 뺌)
            public DropletSize Size;
            public DropletContent Content;
            public bool Taken;
        }

        // CoinField의 IslandLength와 반드시 동기화
        private const float IslandLength = 25000f;

        // 코인 함유 확률 (0~1) — 외부에서 조절 가능하도록 public 필드로 노출
        public float GoldCoinChance   = 0.20f;
        public float SpeedCoinChance  = 0.10f;
        public float GreenCoinChance  = 0.08f;

        private readonly System.Random rng;
        private readonly List<DropletEntry> entries = new List<DropletEntry>();
        private float lastDropletX;

        public IReadOnlyList<DropletEntry> Entries => entries;

        public DropletField(int seed, float startX)
        {
            rng = new System.Random(seed);
            lastDropletX = startX;
        }

        public void EnsureCoverage(float uptoWorldX)
        {
            while (lastDropletX < uptoWorldX)
            {
                // 현재 섬 번호 추정 — 배치 간격·크기·코인 확률 조정에 사용
                int island = Mathf.Max(1, Mathf.FloorToInt(lastDropletX / IslandLength) + 1);

                // 섬이 높아질수록 간격을 좁혀 물방울 밀도 증가
                float minGap = Mathf.Max(300f, 900f - island * 60f);
                float maxGap = Mathf.Max(500f, 1600f - island * 100f);
                lastDropletX += minGap + (float)rng.NextDouble() * (maxGap - minGap);

                // 크기 결정 — 섬이 높을수록 큰 물방울 확률 상승
                float largeChance  = Mathf.Min(0.35f, 0.05f + island * 0.04f);
                float mediumChance = Mathf.Min(0.55f, 0.25f + island * 0.03f);
                DropletSize size;
                double sizeRoll = rng.NextDouble();
                if (sizeRoll < largeChance)
                    size = DropletSize.Large;
                else if (sizeRoll < largeChance + mediumChance)
                    size = DropletSize.Medium;
                else
                    size = DropletSize.Small;

                // 지면 근처 높이 — 코인보다 낮게 배치해 언덕에 붙어있는 느낌
                float offset = 8f + (float)rng.NextDouble() * 14f;

                // 내용물 결정 — 섬 번호에 따라 코인 확률 소폭 상승
                float islandBonus = Mathf.Min(0.10f, (island - 1) * 0.015f);
                DropletContent content = DropletContent.None;
                double contentRoll = rng.NextDouble();
                if (contentRoll < GoldCoinChance + islandBonus)
                    content = DropletContent.GoldCoin;
                else if (contentRoll < GoldCoinChance + islandBonus + SpeedCoinChance)
                    content = DropletContent.SpeedCoin;
                else if (contentRoll < GoldCoinChance + islandBonus + SpeedCoinChance + GreenCoinChance)
                    content = DropletContent.GreenCoin;

                entries.Add(new DropletEntry
                {
                    WorldX  = lastDropletX,
                    Offset  = offset,
                    Size    = size,
                    Content = content
                });
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
