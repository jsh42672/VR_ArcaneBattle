# VR_ArcaneBattle Project Handoff

이 문서는 다른 Codex/개발 세션이 현재 프로젝트 구조와 최근 리팩터링 방향을 빠르게 이어받기 위한 인수인계 문서다.

## 프로젝트 개요

- Unity 프로젝트 루트: `C:\Users\jsh42\nogari\VR_ArcaneBattle`
- 메인 씬: `Assets/Scenes/Main.unity`
- 현재 핵심 입력 기반: Unity XR Hands
- 기존 Meta/OVR 기반 스크립트와 샘플이 일부 남아 있지만, 현재 전투/마법 입력 구조는 XR Hands 중심으로 이전 중이다.
- 씬의 중심 구조는 `GameSystems` 하위 시스템들과 `XR Hands Gesture System`의 양손 gesture 오브젝트가 분리되는 방향이다.

## 현재 메인 씬 핵심 계층

대략적인 메인 씬 구조:

```text
Main
├─ 00_InputCollection_PlayerXR
│  └─ XR Origin
│     ├─ Camera Offset
│     ├─ Left Hand Tracking
│     ├─ Right Hand Tracking
│     └─ XR Hands Gesture System
│        ├─ Left Hand Gestures
│        └─ Right Hand Gestures
├─ 01_InputInterpretation
├─ 02_MagicCombatCore
│  ├─ GrimoireSystem
│  ├─ SpellSpawnRoot
│  └─ GameSystems
│     ├─ InputSystems
│     │  ├─ GestureDetector
│     │  ├─ GestureEventRouter
│     │  ├─ CombinationChecker
│     │  ├─ ArcaneActionModeController
│     │  └─ HandPullMovementController
│     ├─ CombatSystems
│     ├─ SpellSystem
│     │  └─ SpellCaster
│     ├─ TimeStopSystems
│     ├─ VoiceSystem
│     └─ UIManagers
├─ 03_Feedback
├─ 04_TestEnvironment
└─ Environment
```

실제 씬 오브젝트명/계층은 Unity에서 확인해야 한다. 위 구조는 현재 리팩터링 의도를 반영한 논리 구조다.

## 주요 책임 분리

### GestureDetector

파일: `Assets/Scripts/Input/GestureDetector.cs`

역할:

- XR Hands static gesture shape를 읽는 1차 입력 해석기.
- 오른손/왼손 gesture를 감지하고 이벤트로 발행한다.
- 마법 생성, 발사, UI 표시, 시간정지, 조합 결과 계산은 처리하지 않는다.

중요한 serialized field:

- 오른손:
  - `rightFireGesture`
  - `rightIceGesture`
  - `rightThunderGesture`
  - `rightThunderShootGesture`
  - `rightPageTurnGesture`
  - `rightCombine`
  - `rightCombineShoot`
  - `rightBarrier`
- 왼손:
  - `leftGrimoireGesture`
  - `leftFire`
  - `leftIce`
  - `leftThunder`
  - `leftCombine`
  - `leftCombineShoot`
  - `leftBarrier`

이벤트:

- `OnGestureConfirmed(bool isLeft, string gestureName, PoseType pose)`
- `OnGestureCleared(bool isLeft, string gestureName)`
- `OnHandPoseConfirmed(bool isLeft, PoseType pose)`
- `OnHandPoseCleared(bool isLeft)`
- `OnCombinePushDetected`
- `OnGrimTrigger`

주의:

- `rightThunderShootGesture`는 평상시 감지되지 않는다.
- `rightThunderGesture`가 먼저 확정된 뒤 `rightThunderShootArmWindowSeconds` 시간 안에서만 `ThunderShoot`로 감지된다.
- `ThunderShoot`, `Combine`, `CombineShoot`, `PageTurn`, `Barrier`, `Grimoire`는 속성 조합용 element gesture로 흘러가지 않게 유지해야 한다.

### GestureEventRouter

파일: `Assets/Scripts/Input/GestureEventRouter.cs`

역할:

- 현재 구조에서는 콘솔/상태 로그용 라우터다.
- 게임플레이 이벤트를 직접 발생시키지 않는 방향이다.
- 과거 Meta/OVR 기반 event router처럼 마법/마도서/전투 로직을 연결하지 않는다.

### CombinationChecker

파일: `Assets/Scripts/Input/CombinationChecker.cs`

역할:

- 양손 속성 선언을 받아 조합 후보를 계산한다.
- 조합 상태 머신을 가진다.
- `OnCombinationSuccess`, `OnCombinationFail`, `OnComboReadyChanged`를 발행한다.

중요 상태:

- `CombinationState`
- `LeftDeclaredElement`
- `RightDeclaredElement`
- `IsComboReady`
- `CurrentComboCandidate`

주의:

- 조합 시간정지 중에는 양손 속성 선언만 허용되어야 한다.
- combo ready가 되면 시간정지를 해제하고, `ArmComboShootWindow(seconds)`로 짧은 발사 허용 창을 연다.
- 실제 조합 발사는 `leftCombineShoot`/`rightCombineShoot`와 전방 push 조건을 통해 `ReportCombinePush()`가 호출될 때 이루어진다.

### TimeStopSystems

파일: `Assets/Scripts/Input/TimeStopSystems.cs`

역할:

- 시간정지와 행동 잠금을 중앙에서 관리한다.
- 시간정지 효과 자체는 `ArcaneTimeFocusController`를 사용한다.
- 마도서 시간정지와 조합 시간정지를 구분한다.

상태:

- `IsGrimoireTimeStopActive`
- `IsCombinationTimeStopActive`
- `IsCombinationShootPending`

마도서 시간정지 규칙:

- 왼손 마도서 gesture로 마도서가 나타나면 time focus 요청.
- 마도서가 펼쳐진 동안 허용:
  - 마도서 유지
  - 오른손 page swipe
- 마도서가 펼쳐진 동안 금지:
  - 속성 선언 및 발사
  - pull movement
  - 일반 spell cast
- 마도서 gesture가 clear되면 즉시 마도서가 사라지고 시간정지도 종료되어야 한다.

조합 시간정지 규칙:

- 양손 combine gesture가 맞닿으면 조합 시간정지 진입.
- 조합 시간정지 동안 허용:
  - 왼손/오른손 속성 선언
- 조합 시간정지 동안 금지:
  - 일반 발사
  - 마도서 펼치기
  - pull movement
- `CombinationChecker`가 combo ready를 만들면:
  - 양손 사이 combo aura 표시
  - 시간정지 해제
  - combine-shoot push 발사 대기 상태 진입
- combine-shoot push 발사 후 평상시 상태로 복귀.

### ArcaneTimeFocusController

파일: `Assets/Scripts/Input/ArcaneTimeFocusController.cs`

역할:

- 실제 `Time.timeScale`, grayscale post-process, tick sound, overlay camera를 관리한다.
- 여러 reason으로 focus를 요청/해제할 수 있다.
- `TimeFocusExempt` 레이어를 overlay camera로 따로 렌더링해 회색 후처리에서 제외한다.

주의:

- 마도서와 아우라는 `TimeFocusExempt` 레이어에 있어야 본래 색으로 보인다.

### HandPullMovementController

파일: `Assets/Scripts/Combat/HandPullMovementController.cs`

역할:

- 손 pull 기반 이동.
- 현재 잘 동작하는 시스템으로 간주하고, 직접 로직 변경은 최소화한다.

잠금 API:

- `SetMovementSuppressed(bool suppressed, string reason)`

`TimeStopSystems`가 마도서/조합 시간정지 중 이 API를 통해 이동을 잠근다.

### SpellCaster

파일: `Assets/Scripts/Spell/SpellCaster.cs`

역할:

- XR gesture 이벤트를 받아 더미 마법 생성/발사 처리.
- SpellSpawnRoot 하위에 projectile/aura를 생성한다.
- `CombinationChecker.OnCombinationSuccess`를 통해 조합 spell cast도 처리한다.

중요:

- `GestureDetector`가 recognition만 하고, `SpellCaster`가 dummy attack을 담당한다.
- 기존 오른손 gesture 스크립트에 있던 fire/ice/thunder 더미 발사 방식을 이쪽으로 옮기는 중이다.
- `SetCastingSuppressed(bool suppressed, string source)`로 발사를 잠글 수 있다.
- `TimeStopSystems`가 마도서/조합 시간정지 중 이 API를 사용해 발사를 막는다.

현재 더미 공격:

- Fire:
  - `rightFireGesture` 확정 후 손목 위/앞 움직임 threshold로 fireball 발사.
- Ice:
  - `rightIceGesture` 확정 시 ice orb를 손목 근처에 유지.
  - forward throw speed 조건으로 ice projectile 발사.
- Thunder:
  - `rightThunderGesture` 확정 시 charge/aura.
  - `rightThunderShootGesture`가 charge window 안에서 들어와야 beam 발사.

### ElementAuraDummy

파일: `Assets/Scripts/Spell/ElementAuraDummy.cs`

역할:

- Fire/Ice/Thunder/Combo 공통 더미 아우라.
- 모든 속성이 같은 형태를 쓰고 색상만 다르게 한다.
- 런타임 생성 방식이라 별도 VFX prefab 의존을 줄인다.

특징:

- 작은 core sphere
- 얇은 ring 2개
- 약한 point light
- unscaled time pulse
- `TimeFocusExempt` 레이어 재귀 적용

현재 `SpellCaster`는 `useCommonDummyAuras = true`일 때 기존 prefab aura 대신 이 공통 아우라를 우선 사용한다.

### LeftGrimoireGesture

파일: `Assets/Scripts/Input/LeftGrimoireGesture.cs`

역할:

- 마도서 prefab 생성/추적/숨김.
- 현재는 `GestureDetector` 이벤트를 우선 사용한다.
- 자체 XR shape 체크 로직도 남아 있지만, 구조상 recognition은 `GestureDetector`가 담당하는 방향이다.

중요:

- `onGrimoireAppear`, `onGrimoireDisappear` 이벤트를 발행한다.
- `TimeStopSystems`가 이 이벤트로 마도서 시간정지 진입/해제를 처리한다.
- `NextPage()`, `PreviousPage()`는 page turn script에서 호출한다.

### RightPageTurnGesture

파일: `Assets/Scripts/Input/RightPageTurnGesture.cs`

역할:

- 마도서가 열려 있을 때 오른손 wrist swipe를 감지해 페이지 넘김.
- 스와이프 성공 시 `LeftGrimoireGesture.NextPage()` 또는 `PreviousPage()`를 직접 호출한다.

주의:

- 평상시에는 비활성화되어야 한다.
- `TimeStopSystems`가 마도서 시간정지 중에만 page turn을 허용한다.

## 마도서 시스템 분리 방향

현재 `LeftGrimoireGesture`에 다음 책임이 아직 많이 모여 있다.

- gesture 이벤트 수신
- 마도서 prefab 생성
- 손목 추적
- TimeFocusExempt layer 적용
- page turn API
- 외부 suppression

향후 권장 분리:

- `GrimoireGestureController`
  - GestureDetector 이벤트 수신만 담당
- `GrimoireSpawner`
  - prefab instantiate/destroy/position tracking 담당
- `GrimoirePageController`
  - NextPage/PreviousPage와 page turner 참조 담당
- `GrimoireTimeStopBridge`
  - TimeStopSystems와 이벤트 연결 담당

현재는 기능 안정화를 위해 `LeftGrimoireGesture`를 유지하면서 `TimeStopSystems`에서 외부 잠금과 시간정지를 제어한다.

## 아우라 시스템 방향

현재 목표:

- Fire/Ice/Thunder/Combo 아우라를 같은 시스템으로 통일.
- 색상만 다르게 표현.
- 마도서처럼 시간정지 grayscale에서 제외.

구현 상태:

- `ElementAuraDummy` 추가.
- `SpellCaster`가 `useCommonDummyAuras = true`일 때 공통 더미 아우라 사용.
- 모든 아우라에 `TimeFocusExempt` 레이어 적용.
- Fire/Ice/Thunder/Combo aura가 본연 색을 유지하도록 처리.

주의:

- `rightFireAuraPrefab`, `rightThunderAuraPrefab` serialized field는 호환용으로 남아 있다.
- 현재 공통 아우라가 우선이라 기존 `VFX_Fire_01_Small`, `ThunderAuraSphere_Prototype`은 일반 경로에서 사용하지 않는다.

## 테스트와 검증

주요 테스트 파일:

- `Assets/Editor/CombinationArchitectureEditorTests.cs`

최근 확인된 통과 테스트:

- `CombinationArchitectureEditorTests`: 13개 통과

최근 validation:

- `GestureDetector.cs`: error 0
- `GestureEventRouter.cs`: error 0
- `CombinationChecker.cs`: error 0
- `TimeStopSystems.cs`: error 0
- `SpellCaster.cs`: error 0
- `ElementAuraDummy.cs`: error 0
- `RightPageTurnGesture.cs`: error 0
- `LeftGrimoireGesture.cs`: error 0

Unity 콘솔:

- 마지막 확인 시 error/warning 0개.

## 현재 Git 작업 상태 관련 주의

작업 트리에는 여러 수정 파일이 있다. 다른 세션은 임의로 revert하지 말 것.

주요 수정/추가 파일:

- `Assets/Scenes/Main.unity`
- `Assets/Editor/CombinationArchitectureEditorTests.cs`
- `Assets/Scripts/Input/GestureDetector.cs`
- `Assets/Scripts/Input/GestureEventRouter.cs`
- `Assets/Scripts/Input/CombinationChecker.cs`
- `Assets/Scripts/Input/TimeStopSystems.cs`
- `Assets/Scripts/Input/LeftGrimoireGesture.cs`
- `Assets/Scripts/Input/RightPageTurnGesture.cs`
- `Assets/Scripts/Spell/SpellCaster.cs`
- `Assets/Scripts/Spell/ElementAuraDummy.cs`
- `Assets/Scripts/UI/GrimoireManager.cs`
- `Assets/Scripts/Editor/XRHandsStaticGestureSceneSetup.cs`

관찰된 기타 dirty 항목:

- `.external_samples/Unity-InteractionSDK-Samples`
- `Assets/Art/VFX/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Small.prefab`

위 두 항목은 주 작업 범위와 직접 관련 없을 수 있으므로, 다음 세션에서 변경 출처를 확인하기 전에는 건드리지 않는 것이 좋다.

## 다음 세션에서 우선 확인할 것

1. Unity MCP 연결 확인
   - active instance: `VR_ArcaneBattle@b93aa38c525f2256`
   - `mcpforunity://instances`
   - `mcpforunity://editor/state`

2. Main 씬 상태 확인
   - `GameSystems/TimeStopSystems` 존재 여부
   - `SpellCaster.useCommonDummyAuras == true`
   - `LeftGrimoireGesture.gestureDetector` 연결
   - `RightPageTurnGesture.leftGrimoireGesture` 연결
   - `GestureDetector.rightThunderShootGesture` 연결

3. Play Mode 수동 검증
   - Fire gesture 시 주황 공통 aura 표시 후 fireball 발사.
   - Ice gesture 시 청록 aura + ice orb 표시 후 throw 발사.
   - Thunder gesture 시 노랑 aura만 표시.
   - Thunder shoot gesture는 Thunder 후 짧은 시간 안에서만 beam 발사.
   - 마도서 gesture 유지 중 마도서 표시 + 시간정지 + page swipe만 허용.
   - 마도서 gesture 해제 시 마도서 숨김 + 시간정지 해제.
   - 조합 시간정지 중 일반 발사/pull/grimoire 금지.
   - 조합 ready 후 시간정지 해제 + combo aura 표시 + combine shoot push로 발사.

4. 테스트 재실행
   - `CombinationArchitectureEditorTests`
   - 필요 시 PlayMode 쪽 수동 테스트 보강.

## 설계 원칙

- `GestureDetector`: 감지만 한다.
- `GestureEventRouter`: 로그/진단만 한다.
- `CombinationChecker`: 조합표와 조합 상태만 처리한다.
- `SpellCaster`: 마법 생성/발사/아우라 feedback만 처리한다.
- `TimeStopSystems`: 시간정지와 행동 잠금만 처리한다.
- `HandPullMovementController`: 이동만 처리한다. 되도록 건드리지 않는다.
- `LeftGrimoireGesture`: 현재는 마도서 생성/추적 담당. 향후 더 작게 분리 예정.
