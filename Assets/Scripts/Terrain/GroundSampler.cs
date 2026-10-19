using System.Collections.Generic;
using UnityEngine;

namespace HillyWings
{
    // Randomized rolling-hill terrain: a sequence of control points at
    // random x-spacing (hill width) and random y-offset (hill height),
    // smoothly cosine-interpolated between them (zero slope exactly at each
    // peak/valley, so there's no kink for the physics to snag on). Replaces
    // the earlier fixed sine-wave terrain per playtest feedback -- hills now
    // vary in both width and height instead of repeating one period.
    //
    // Canvas-space convention: y grows downward. Stateful and seeded
    // (deterministic per run), so it's an instance, not a static class.
    // GameManager owns the single instance; TerrainGenerator and BirdPhysics
    // both query it so the rendered hill and the physics ground line never
    // disagree.
    public class GroundSampler
    {
        private struct ControlPoint
        {
            public float X;
            public float Y;
        }

        private readonly System.Random rng;
        private readonly List<ControlPoint> points = new List<ControlPoint>();
        private readonly float baseY;
        private float lastDirection = -1f;

        // 초반 (Island 1) 파라미터 — 좁은 범위, 일정한 리듬
        private readonly float easyMinHalfWave;
        private readonly float easyMaxHalfWave;
        private readonly float easyMinAmp;
        private readonly float easyMaxAmp;
        // 후반 (Island 6+) 파라미터 — 넓은 범위, 예측 불가
        private readonly float hardMinHalfWave;
        private readonly float hardMaxHalfWave;
        private readonly float hardMinAmp;
        private readonly float hardMaxAmp;

        // 이 섬 수만큼 지나면 최고 난이도 도달 (0-indexed 기준: 5 = Island 6 이후 완전 최고)
        private const float MaxDifficultyIslands = 5f;
        private const float IslandLength = 5000f;

        public float BaseY => baseY;
        // 색상 그라디언트 기준은 최고 난이도 진폭 — 전체 런에서 일관된 색 밴딩
        public float MaxAmplitude => hardMaxAmp;

        public GroundSampler(int seed, float viewHeight)
        {
            rng = new System.Random(seed);
            baseY = viewHeight * 0.66f;

            // Island 1-2: 반파장·진폭 모두 좁은 범위 → 일정한 리듬, 초보자도 패턴 파악 가능
            easyMinHalfWave = viewHeight * 0.40f;
            easyMaxHalfWave = viewHeight * 0.44f;
            easyMinAmp      = viewHeight * 0.11f;
            easyMaxAmp      = viewHeight * 0.13f;

            // Island 6+: 극단적으로 넓은 범위 → 짧고 가파른 언덕과 길고 완만한 언덕이 혼재
            hardMinHalfWave = viewHeight * 0.24f;
            hardMaxHalfWave = viewHeight * 0.60f;
            hardMinAmp      = viewHeight * 0.08f;
            hardMaxAmp      = viewHeight * 0.22f;

            float startMaxHalf = easyMaxHalfWave;
            // 시작 지점: 새가 평지에서 안전하게 스폰되도록 짧은 평지 구간 확보
            points.Add(new ControlPoint { X = -startMaxHalf * 2f, Y = baseY });
            points.Add(new ControlPoint { X = 0f, Y = baseY });
            EnsureCoverage(startMaxHalf * 4f);
        }

        public float GroundY(float worldX)
        {
            EnsureCoverage(worldX + hardMaxHalfWave);
            int i = FindSegment(worldX);
            ControlPoint a = points[i];
            ControlPoint b = points[i + 1];
            float t = Mathf.InverseLerp(a.X, b.X, worldX);
            float eased = (1f - Mathf.Cos(t * Mathf.PI)) * 0.5f;
            return Mathf.Lerp(a.Y, b.Y, eased);
        }

        public float GroundSlope(float worldX)
        {
            const float d = 2f;
            return (GroundY(worldX + d) - GroundY(worldX - d)) / (2f * d);
        }

        private void EnsureCoverage(float upToX)
        {
            while (points[points.Count - 1].X < upToX)
            {
                // 현재 생성 위치를 섬 번호로 환산해 난이도 보간
                // (GameManager 의존 없이 X 좌표에서 직접 계산)
                float curX = points[points.Count - 1].X;
                float islandIdx = Mathf.Max(0f, curX / IslandLength);
                float t = Mathf.Clamp01(islandIdx / MaxDifficultyIslands);

                float curMinHalfWave = Mathf.Lerp(easyMinHalfWave, hardMinHalfWave, t);
                float curMaxHalfWave = Mathf.Lerp(easyMaxHalfWave, hardMaxHalfWave, t);
                float curMinAmp      = Mathf.Lerp(easyMinAmp,      hardMinAmp,      t);
                float curMaxAmp      = Mathf.Lerp(easyMaxAmp,      hardMaxAmp,      t);

                float halfWave = Mathf.Lerp(curMinHalfWave, curMaxHalfWave, (float)rng.NextDouble());
                float amp      = Mathf.Lerp(curMinAmp,      curMaxAmp,      (float)rng.NextDouble());
                lastDirection *= -1f;
                float nextX = curX + halfWave;
                float nextY = baseY + lastDirection * amp;
                points.Add(new ControlPoint { X = nextX, Y = nextY });
            }
        }

        private int FindSegment(float worldX)
        {
            int lo = 0, hi = points.Count - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (points[mid].X <= worldX) lo = mid; else hi = mid - 1;
            }
            return lo;
        }
    }
}
