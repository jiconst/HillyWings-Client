# Hilly Wings (병아리 날다)

타이니윙스(Tiny Wings) 스타일 언덕 슬라이드/글라이드 모바일 게임입니다.
내리막을 빠르게 하강 후 정상에서 발사 — 하늘을 날아 다음 섬으로 이동하는 물리 기반 게임입니다.

## 게임 방법

- **화면 누르기**: 내리막에서 빠르게 하강 (누르고 있는 동안 가속)
- **손 떼기**: 오르막 정상 부근에서 손을 떼면 하늘로 발사
- **Great Slide**: 다이빙 직후 착지 성공 시 획득
- **Fever Mode**: Great Slide 3연속 → 점수 2배
- **코인 수집**: 노란 코인(+3점), 파란 스피드코인(속도 부스트)
- **클라우드 터치**: 공중에서 구름 터치 시 +20점
- **Sky Flight Orb**: 초록 오브 획득 시 하늘 비행

## 실행 환경

- Unity 2022.3 LTS 이상 권장
- 2D Built-in RP (URP 사용 시 셰이더 설정 필요, 아래 참고)
- iOS / Android 빌드 지원
- 서버: FlyingChick-Server (Kotlin + PostgreSQL), 로컬 기본 포트 8010

## 설치 및 실행

1. Unity Hub에서 프로젝트 열기 (`HillyWings-Client/` 폴더)
2. `Assets/Resources/Audio/BGM/` 에 BGM 파일 배치 (예: `bensound-cute.mp3`)
3. Play 버튼 클릭 — 모든 씬/오브젝트는 코드로 자동 생성됨

### URP 사용 시

`TerrainGenerator` 인스펙터의 `Shader Name` 필드를
`Universal Render Pipeline/2D/Sprite-Lit-Default`로 변경.

## 폴더 구조

```
Assets/
  Scripts/
    Core/
      GameBootstrapper.cs   런타임 씬/컴포넌트 조립
      GameManager.cs        낮/밤 타이머, 게임 상태 관리
      Localization.cs       한국어/영어 다국어 (Language enum)
      SaveSystem.cs         로컬 저장 (최고점수, 언어, 새 컬렉션 등)
      CoinWallet.cs         코인 수집/지출 관리
      DailyMissions.cs      일일 미션 생성 및 추적
    Gameplay/
      BirdController.cs     핵심 물리 (아래 알고리즘 참고)
      TerrainGenerator.cs   사인파 기반 절차적 지형 생성
      IslandManager.cs      다중 섬 전환 관리
      Collectibles/
        CoinSpawner.cs      노란/파란 코인 스폰
        CloudSpawner.cs     구름 스폰 (클라우드터치)
        SkyOrbSpawner.cs    Sky Flight Orb
    UI/
      StartScreen.cs        메인 메뉴 + 설정 + 게임방법(슬라이드) + 기록
      DayOverScreen.cs      결과 화면 (점수/통계/코인)
      UIFactory.cs          UGUI 런타임 생성 헬퍼
    Audio/
      AudioManager.cs       BGM + SFX 재생
    Network/
      ApiClient.cs          서버 REST API 클라이언트
      AuthService.cs        로그인/회원가입
      RankingService.cs     온라인 점수 조회
```

## 핵심 물리 알고리즘

`BirdController.FixedUpdate()` 매 스텝:

1. 자유낙하 중이라 가정, 중력 적분으로 다음 위치 후보 계산
2. 해당 x좌표의 지형 높이(`TerrainGenerator.HeightAt`)와 비교
3. **지형 아래** → 착지: 지형 위로 스냅, 속도를 접선 방향으로 재배치. 하강+입력 중이면 추가 가속
4. **지형 위** → 공중: 자유낙하 계속

단 한 번의 비교로 착지/발사를 처리. 지형 수식(`h(x)`)을 시각 메시와 충돌 판정이 공유하므로 보이는 것과 실제 판정이 완전히 일치합니다.

## 낮/밤 사이클 (DayCycle)

- 기본 하루 길이: **90초**
- **섬 도달 시 낮 초기화** — 다음 섬에 닿는 순간 해가 아침으로 돌아감 (섬마다 새로운 하루)
- **속도 연동 (Tiny Wings 방식)** — 빠르게 날수록 해가 느리게 움직이고, 충분히 빠르면 해가 뒤로 밀림

| 속도 | 낮 진행 배율 | 체감 하루 길이 |
|------|------------|-------------|
| 300 (저속) | 1.12× | ~80초 (더 빨리 밤이 옴) |
| 360 (기준) | 1.00× | 90초 |
| 500 | 0.72× | ~125초 |
| 700 | 0.32× | ~280초 |
| 900+ | **음수** | 해가 뒤로 밀림 |

인스펙터 `speedInfluence`(기본 0.002)로 민감도 조절.

## 급경사 언덕 위험 요소 (BirdPhysics)

뾰족하고 가파른 언덕은 의도적인 장애물입니다. 낮은 속도로 접근하면 힘겹게 넘어야 하며, 플레이어는 이를 미리 인지하고 피하거나 충분한 속도를 확보해야 합니다.

**메커니즘**: 경사가 `UphillSlopeThreshold`(0.18)를 초과하는 가파른 오르막에 지면 접촉 시 추가 감속 페널티 적용.
- 낮은 속도로 접근 → 언덕에서 바운스를 반복하며 매번 속도 손실 → 탈출이 점점 어려워짐
- 충분한 속도(다운힐 가속 후)로 접근 → 발사 각도가 높아 언덕을 깔끔하게 넘음
- Island 6+ 급경사(slope ≈ -1.4): 페널티 ~315/s, 저속 조우 시 SpeedMin까지 드레인

| 구분 | 임계 경사 | 초당 감속 예시 | 체감 |
|------|---------|------------|------|
| 완만한 오르막 (< 0.18) | 페널티 없음 | 0 | 정상 주행 |
| 보통 오르막 (0.3) | 0.12 초과분 | 36/s | 살짝 느려짐 |
| 급경사 (1.0) | 0.82 초과분 | 246/s | 빠르게 감속 |
| Island 6+ 최대 (1.44) | 1.26 초과분 | 378/s | 저속 시 SpeedMin 도달 |

`UphillDecelRate`(기본 300) 인스펙터 값으로 강도 조절.

## 지형 난이도 커브 (GroundSampler)

섬이 진행될수록 언덕의 너비·높이 변화폭이 커져 점점 어려워집니다.

| 섬 | 반파장 범위 | 진폭 범위 | 체감 |
|----|-----------|---------|------|
| Island 1 | 0.40~0.44 × H | 0.11~0.13 × H | 일정한 리듬, 패턴 예측 쉬움 |
| Island 3 | 0.34~0.54 × H | 0.09~0.17 × H | 불규칙해지기 시작 |
| Island 6+ | 0.24~0.60 × H | 0.08~0.22 × H | 짧은 급경사 + 긴 완만한 언덕 혼재 |

`IslandLength = 5000` — 기본 속도(360) 기준 섬 하나 약 14초, 90초 낮 동안 최대 6~7섬 도달 가능.  
`MaxDifficultyIslands = 5` — Island 6 이후 최고 난이도 고정. 값을 높이면 난이도 상승이 더 완만해짐.

## 섬 전환 (Island Transition) 밸런스

다음 섬으로 이동하는 전환 구간 설계 원칙:

- **직전 섬 마지막 언덕에서 빠른 하강 후 힘찬 점프**로 공중 비행하며 갭을 가로지름
- 전환 공중 구간에 **코인/스피드코인이 산개** — 잘 플레이한 유저에게 보너스 기회 제공
- 갭 너비는 체감상 "힘차게 날면 닿는다" 수준으로 조정 (너무 멀면 답답함)
- 해(태양) 위치가 좌하단 기준으로 표시되며 시간 경과를 나타냄
- `ISLAND N` 배너가 화면 중앙 하단에 표시되며 전환 확인

## UI 주요 화면

### 메인 메뉴
- **오늘의 비행** (빨간 버튼) — UTC 날짜 기반 고정 씨앗, 같은 날은 전 세계 동일한 지형 (Tiny Wings TODAY 모드)
- **일반 비행** (주황 버튼) — 매 런 랜덤 씨앗
- 설정, 기록, 게임 방법 버튼

### 게임 방법 (Tiny Wings 스타일 슬라이드)
4페이지 슬라이드 방식, [이전] [뒤로] [다음] 버튼 네비게이션:
1. **조작 방법** — 하강/발사 기본 컨트롤
2. **Great Slide & Fever** — 콤보 시스템
3. **아이템** — 코인/스피드코인/구름/Sky Orb
4. **섬 & Nest** — 점수 배수 및 Nest 목표

### 섬 도달 배너
- 섬 전환 시 화면 하단에 파란 배너 슬라이드인 ("ISLAND N")
- 페이드인 0.3초 → 유지 → 페이드아웃 0.5초, 총 2.8초 표시

### 결과 화면 (Day Over)
- 점수, 섬 도달, Great Slide 수, 클라우드터치, Fever 최장, 최고기록
- 코인 획득량 및 누적 코인 표시

## Tiny Wings 분석 기반 적용 사항

| 항목 | Tiny Wings | HillyWings 적용 |
|---|---|---|
| TODAY 모드 | 날짜 고정 씨앗 | `GameManager.TodaySeed()` — UTC 날짜 기반 |
| 섬 도달 배너 | 화면 중앙 하단 표시 | `HUD.cs` 슬라이드인 파란 배너 |
| Great Slide 3연속 → Fever | 핵심 콤보 시스템 | `SlideJudge.feverTriggerStreak = 3` |
| 점수 배수 | 섬마다 +2, 최대 30× | `10 + NestBonus + (Island-1)*2` |
| 이동 거리 점수 트리클 | 항상 소량 쌓임 | `BirdController.distanceScoreRate = 0.005f` |
| 속도 연동 낮 연장 | 빠를수록 해가 느려짐 | `DayCycle.speedInfluence = 0.002f` |
| 섬마다 낮 초기화 | 섬 전환 시 새 하루 | `DayCycle.HandleIslandAdvanced` |
| 진행형 지형 난이도 | 섬마다 어려워짐 | `GroundSampler` easy→hard 커브 |
| 급경사 언덕 장애물 | 뾰족한 언덕은 피해야 할 위험 요소 | `BirdPhysics.UphillDecelRate` 페널티 |
| 슬라이드 설명 슬라이드 | 4페이지 방식 | `HowToPlay` 4페이지 ([이전]/[다음]) |
| 미니멀 HUD | 디버그 요소 숨김 | `showDebugHud = false` 기본값 |

## 다국어

`Localization.Get("key")` 로 모든 UI 텍스트 조회.
설정 화면에서 언어 전환 (한국어/영어).
폰트: `Assets/StreamingAssets/Fonts/NanumGothic-Bold.ttf` (기기 빌드용 한국어 폰트).

## 서버 연동 (FlyingChick-Server)

- 로그인/회원가입: `POST /auth/login`, `POST /auth/signup`
- 점수 등록: `POST /scores` (Day Over 화면 — Phase C, 미구현)
- 온라인 랭킹 조회: `GET /scores/top`
- 기본 URL: `Assets/Scripts/Network/ServerConfig.cs` 의 `BaseUrl` 변경
