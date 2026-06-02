# Main Scene XR Migration Audit

작성일: 2026-06-02

## 목적

Main 씬에서 예전 Meta OVR 기반 제스처 시스템과 새 Unity XR Hands 기반 제스처 시스템이 어디까지 혼재되어 있는지 파악한다.

이 문서는 삭제 작업을 하지 않는다. 씬과 에셋을 보존한 상태에서, 이후 폐기/정리 순서를 정하기 위한 기준점이다.

## 현재 결론

Main 씬의 실제 활성 제스처 입력 경로는 Unity XR Hands 쪽으로 전환되어 있다.

다만 런타임 스크립트 일부는 아직 OVRHand, OVRSkeleton, OVRCameraRig 타입을 fallback 또는 prototype 경로로 직접 참조한다. 즉, 씬의 활성 오브젝트 기준으로는 OVR 손 추적이 비활성 보관 상태이지만, 코드 기준으로는 OVR 의존성이 완전히 제거되지 않았다.

## 씬 내 활성/비활성 요약

### 활성 XR Hands 경로

- `XR Origin/Left Hand Tracking`
  - `XRHandTrackingEvents`
  - `XRHandSkeletonDriver`
- `XR Origin/Right Hand Tracking`
  - `XRHandTrackingEvents`
  - `XRHandSkeletonDriver`
- `GameSystems/InputSystems/XR Hands Gesture System/Left Hand Gestures`
  - `XRHandTrackingEvents`
  - `LeftGrimoireGesture`
- `GameSystems/InputSystems/XR Hands Gesture System/Right Hand Gestures`
  - `XRHandTrackingEvents`
  - `RightFireGesture`
  - `RightIceGesture`
  - `RightPageTurnGesture`
  - `RightThunderGesture`
- `GameSystems/InputSystems/GestureEventRouter`
  - `GestureEventRouter`
- `GameSystems/InputSystems/GestureDetector`
  - `GestureDetector`
- `GameSystems/InputSystems/HandPullMovementController`
  - `HandPullMovementController`

### 활성 Meta Interaction UnityXR 경로

- `Input Systems/Meta Interaction UnityXRInteraction`
  - Meta Interaction SDK의 UnityXR data source, hand/controller provider, synthetic hand/controller 계층이 활성 상태다.
  - 이름에 Meta/Oculus가 남아 있지만, 현재 컴포넌트 성격은 OVR CameraRig 직접 손 추적이라기보다 Meta Interaction의 UnityXR provider 계층이다.
  - 새 XR Hands 녹화/인식 흐름에 필요한지, 아니면 예전 Recorder/Interaction 잔재인지 별도 검증이 필요하다.

### 비활성 OVR/Meta 보관 경로

- `_Disabled/OVRCameraRig`
  - `OVRCameraRig`
  - `OVRManager`
  - `OVRHeadsetEmulator`
- `_Disabled/OVRCameraRig/TrackingSpace/LeftHandAnchor/Left OVRHandPrefab`
  - `OVRHand`
  - `OVRSkeleton`
  - `OVRSkeletonRenderer`
  - `OVRMesh`
  - `OVRMeshRenderer`
- `_Disabled/OVRCameraRig/TrackingSpace/RightHandAnchor/Right OVRHandPrefab`
  - `OVRHand`
  - `OVRSkeleton`
  - `OVRSkeletonRenderer`
  - `OVRMesh`
  - `OVRMeshRenderer`
- `_Disabled/Arcane Meta Gameplay Gesture Selectors`
  - `Left_Fist`
  - `Left_Grimoire_OpenPalm`
  - `Right_Fire_OpenPalm`
  - `Right_Gun`
  - `Right_Ice_Fist`
  - `Right_Thunder_ThumbsUp`
  - 각 오브젝트에 `HandRef`, `ActiveStateSelector`, `ActiveStateGroup`, `ShapeRecognizerActiveState`, `TransformRecognizerActiveState`, `MetaHandPoseGestureBridge`, `MetaHandPoseDebugLogger`, `MetaHandTrackingActiveState`가 붙어 있다.

## 컴포넌트 카운트

- `OVRHand`: active 0, inactive 2
- `OVRSkeleton`: active 0, inactive 2
- `OVRCameraRig`: active 0, inactive 1
- `OVRManager`: active 0, inactive 1
- `MetaHandPoseGestureBridge`: active 0, inactive 6
- `MetaHandTrackingActiveState`: active 0, inactive 6
- `ShapeRecognizerActiveState`: active 0, inactive 10
- `TransformRecognizerActiveState`: active 0, inactive 6
- `HandRef`: active 0, inactive 30
- `XRHandTrackingEvents`: active 4, inactive 0
- `GestureDetector`: active 1, inactive 0
- `GestureEventRouter`: active 1, inactive 0
- `LeftGrimoireGesture`: active 1, inactive 0
- `RightFireGesture`: active 1, inactive 0
- `RightIceGesture`: active 1, inactive 0
- `RightThunderGesture`: active 1, inactive 0
- `RightPageTurnGesture`: active 1, inactive 0
- `VoiceRecognizer`: active 1, inactive 0
- `SpellCaster`: active 1, inactive 0
- `CombatManager`: active 1, inactive 0
- `DodgeDetector`: active 1, inactive 0
- `HandPullMovementController`: active 1, inactive 0
- `ManaWristDisplay`: active 1, inactive 0
- `GrimoireManager`: active 1, inactive 0
- `FeedbackManager`: active 1, inactive 0

## 목표 구조도 기준 역할 매핑

### 입력 수집 계층

- Head Tracking
  - 현재 씬 역할: `XR Origin` 및 Main Camera 계층
  - 관련 코드: `ArcanePlayerRigResolver`, `BossBattleRuntimeBinder`, `HandPullMovementController`
  - 혼재 지점: 일부 코드가 아직 `OVRCameraRig`를 찾는다.
- Hand Tracking
  - 현재 씬 역할: `XR Origin/* Hand Tracking`, `GameSystems/InputSystems/XR Hands Gesture System/* Hand Gestures`
  - 관련 코드: `LeftGrimoireGesture`, `RightFireGesture`, `RightIceGesture`, `RightThunderGesture`, `RightPageTurnGesture`, `RecordedPoseConditionUtility`
  - 상태: Unity XR Hands 기반이 활성.
- 음성 인식
  - 현재 씬 역할: `GameSystems/SpellSystem/VoiceSystem`
  - 관련 코드: `VoiceRecognizer`, `ExternalVoiceCommandBridge`, `MetaVoiceSdkAutoBridge`

### 입력 해석 계층

- GestureDetector
  - 현재 씬 역할: `GameSystems/InputSystems/GestureDetector`
  - 현재 상태: XR router를 사용하지만 OVR prototype/fallback 코드도 포함.
- GestureEventRouter
  - 현재 씬 역할: `GameSystems/InputSystems/GestureEventRouter`
  - 현재 상태: XR Hands gesture components와 기존 `GestureDetector`/`SpellCaster` 사이를 이어주는 핵심 라우터.
- MovementController
  - 현재 씬 역할: `GameSystems/InputSystems/HandPullMovementController`
  - 현재 상태: 목표 구조도상 `MovementController`에 해당. 별도 `MovementController.cs`도 존재하며 OVR 왼손 fallback을 포함하므로 통합 여부 확인 필요.
- DodgeDetector
  - 현재 씬 역할: `GameSystems/CombatSystems/DodgeSystem`

### 마법/전투 핵심 로직

- SpellDatabase
  - 현재 코드 역할: `SpellCaster`가 `Resources/ArcaneVR/SpellDatabase` fallback을 사용.
- SpellCaster
  - 현재 씬 역할: `GameSystems/SpellSystem/SpellCaster`
  - 혼재 지점: `OVRHand prototypeHand`, `OVRSkeleton` 기반 prototype casting 코드가 남아 있다.
- CombatManager
  - 현재 씬 역할: `GameSystems/CombatSystems/CombatManager`
- BossAI / ConstraintController
  - 현재 씬 역할: 보스 계층 및 `GameSystems/CombatSystems/ConstraintController`
  - 주의: 관련 전투 오브젝트에 Missing Script가 있다.
- GrimoireManager
  - 현재 씬 역할: `GrimoireSystem`
  - 혼재 지점: XR `LeftGrimoireGesture`와 별개로 `GrimoireManager` 내부에 `OVRHand` 기반 open/fist/page fallback이 남아 있다.

### 피드백 계층

- FeedbackManager
  - 현재 씬 역할: `GameSystems/UIManagers/FeedbackManager`
- Mana UI
  - 현재 씬 역할: `GameSystems/UIManagers/ManaWristDisplay`
  - 혼재 지점: `ManaWristDisplay`가 `OVRHand`와 `OVRSkeleton` wrist bone을 직접 찾는다. XR wrist transform 기반으로 바꾸는 것이 다음 정리 후보다.
- VFX
  - 현재 씬 역할: `AuraParticles`, spell projectile visuals, wrist aura 관련 스크립트

### 씬/진행 관리

- Scene flow
  - 현재 코드 역할: `GameSceneManager`
  - 목표 구조도상 `CombatScene`, `ResultScene`, `WorldMapScene` 흐름과 대응.

## 코드상 OVR 의존성 잔존 지점

우선 제거 후보는 활성 Main 씬의 런타임 매니저가 직접 참조하는 아래 파일들이다.

- `Assets/Scripts/Input/GestureDetector.cs`
  - `OVRHand`, `OVRSkeleton`, `OVRPlugin` 기반 prototype/fallback 검출 로직 포함.
  - 동시에 `XRHandSubsystem`, `GestureEventRouter`도 사용하므로 현재 가장 큰 혼재 지점이다.
- `Assets/Scripts/Spell/SpellCaster.cs`
  - `OVRHand prototypeHand`, `OVRSkeleton` 기반 prototype cast origin/fire direction 계산 포함.
  - XR gesture router 기반 cast와 OVR prototype cast가 공존한다.
- `Assets/Scripts/UI/GrimoireManager.cs`
  - `OVRHand` 기반 open palm/fist 판단과 pointer pose fallback 포함.
  - 새 `LeftGrimoireGesture`/`RightPageTurnGesture`와 책임이 겹칠 가능성이 있다.
- `Assets/Scripts/UI/ManaWristDisplay.cs`
  - `OVRHand`와 `OVRSkeleton`에서 오른손 wrist bone을 찾아 UI를 붙인다.
  - XR Hands 전환 후에는 `Right Hand Tracking > R_Wrist` 또는 명시적 wrist transform 참조로 바꾸는 후보.
- `Assets/Scripts/Input/MovementController.cs`
  - 왼손 anchor를 찾을 때 `OVRHand` fallback이 남아 있다.
  - 현재 씬에서는 `HandPullMovementController`가 활성이라 이 스크립트가 실제 사용 중인지 확인 필요.
- `Assets/Scripts/Core/ArcanePlayerRigResolver.cs`
  - `OVRCameraRig` 기반 player rig 판별 fallback 포함.
- `Assets/Scripts/Boss/BossBattleRuntimeBinder.cs`
  - 런타임 바인딩에서 `OVRCameraRig` fallback 포함.
- `Assets/Scripts/Combat/HandPullMovementController.cs`
  - rig root에서 `OVRCameraRig`를 찾는 fallback 포함.
- `Assets/Scripts/Input/MagicSystemTestDriver.cs`
  - 테스트 드라이버 성격이지만 `OVRHand` 정규화/탐색 코드 포함.
- `Assets/Scripts/Input/MetaHandPoseGestureBridge.cs`, `MetaHandTrackingActiveState.cs`, `MetaHandPoseRecognitionDebugPanel.cs`, `RecordedPoseRecognitionTestPanel.cs`
  - 예전 Meta Interaction pose selector/recorder 계층.
  - 현재 씬에서는 `_Disabled/Arcane Meta Gameplay Gesture Selectors` 쪽과 연관.

## Missing Script 보류 지점

Main 씬에는 아래 Missing Script가 있다. 현재 이 항목들은 조원이 개발 중인 기능이 이후 병합될 예정이라, XR/OVR 정리 과정에서 삭제하거나 복구 판단하지 않는다.

- `GameSystems/CombatSystems/BarrierSystem`
  - missing component index 2
  - missing component index 3
- `GameSystems/CombatSystems/DodgeSystem`
  - missing component index 2
- `GameSystems/CombatSystems/ConstraintController`
  - missing component index 1
- `GameSystems/CombatSystems/BossAttackControllers`
  - missing component index 1
  - missing component index 3

## OpenWorld2 확인 결과

대상 씬 경로는 `Assets/Scenes/OPenWorld2.unity`다. 파일명은 `OPenWorld2`로 대소문자가 섞여 있다.

현재 OpenWorld2는 월드/환경 씬에 가깝고, XR 플레이어/입력/마법 런타임 세팅은 거의 없다.

### OpenWorld2 루트 구조

- `=== Environment_Terrain ===`
  - 지형, 나무, 물, 암석 등 환경 오브젝트
- `=== Environment_Props ===`
  - 소품
- `=== Buildings_Structures ===`
  - `house3`, Torii gate 등 구조물
- `=== Gameplay_Targets_Combat ===`
  - `Dummy`
  - `Dummy (1)`
- `=== VFX_Magic ===`
  - `portal`
- `=== Lighting_Cameras ===`
  - `Sun_Key_Light`
  - `Main Camera`

### OpenWorld2에 없는 것

- `XROrigin`
- `XRHandTrackingEvents`
- `XRHandSkeletonDriver`
- `GestureDetector`
- `GestureEventRouter`
- `LeftGrimoireGesture`
- `RightFireGesture`
- `RightIceGesture`
- `RightThunderGesture`
- `RightPageTurnGesture`
- `SpellCaster`
- `VoiceRecognizer`
- `CombatManager`
- `FeedbackManager`
- `ManaWristDisplay`
- `GrimoireManager`

즉, OpenWorld2에는 현재 플레이어 입력 수집 계층도, 입력 해석 계층도, 마법/전투 핵심 로직도 들어가 있지 않다. Main 씬에서 정리한 XR 기반 모듈을 이식해야 실제 플레이 테스트가 가능하다.

## 기존 프리팹 상태

`Assets/Prefabs/Core/GameSystems.prefab`는 이미 존재한다.

포함된 주요 구성:

- `GameSystems/InputSystems/GestureDetector`
- `GameSystems/InputSystems/GestureEventRouter`
- `GameSystems/InputSystems/CombinationChecker`
- `GameSystems/InputSystems/ArcaneActionModeController`
- `GameSystems/InputSystems/HandPullMovementController`
- `GameSystems/CombatSystems/CombatManager`
- `GameSystems/CombatSystems/DodgeSystem`
- `GameSystems/SpellSystem/SpellCaster`
- `GameSystems/SpellSystem/VoiceSystem`
- `GameSystems/UIManagers/ManaWristDisplay`
- `GameSystems/UIManagers/ArcaneAimReticle`
- `GameSystems/UIManagers/BossHealthBarUI`
- `GameSystems/UIManagers/FeedbackManager`

하지만 이 프리팹에는 Main 씬에서 새로 동작 중인 아래 XR Hands 제스처 호스트가 포함되어 있지 않다.

- `GameSystems/InputSystems/XR Hands Gesture System/Left Hand Gestures`
- `GameSystems/InputSystems/XR Hands Gesture System/Right Hand Gestures`
- `LeftGrimoireGesture`
- `RightFireGesture`
- `RightIceGesture`
- `RightPageTurnGesture`
- `RightThunderGesture`
- 각 gesture 컴포넌트가 참조하는 `XRHandTrackingEvents`, `XRHandShape`, wrist transform 참조

따라서 OpenWorld2 이식은 `GameSystems.prefab`만 배치해서 끝나는 작업이 아니다. XR Origin과 좌우 hand tracking, XR Hands Gesture System까지 함께 묶어야 한다.

## 재스처 담당 범위의 모듈화 방향

OpenWorld2로 넘기기 전에 Main 씬에서 먼저 만들 단위는 다음처럼 나누는 것이 안전하다.

### 1. Player XR Rig 모듈

역할:

- HMD/head tracking
- left/right hand tracking
- `XRHandTrackingEvents`
- wrist transform 제공

후보 구성:

- `XR Origin`
- `XR Origin/Left Hand Tracking`
- `XR Origin/Right Hand Tracking`

주의:

- OpenWorld2에는 이미 일반 `Main Camera`가 있으므로, XR Rig를 넣을 때 카메라 중복을 정리해야 한다.
- 삭제보다 먼저 OpenWorld2의 기존 `Main Camera`를 비활성 보관하거나 `=== Lighting_Cameras ===` 아래에 유지한 채 XR 카메라를 우선 사용하도록 검증하는 방식이 안전하다.

### 2. Arcane XR Gesture Input 모듈

역할:

- XR Hands static gesture 감지
- gesture 이벤트 라우팅
- 마도서, 화염, 얼음, 번개, 페이지 넘김 감지

후보 구성:

- `GestureEventRouter`
- `GestureDetector`
- `XR Hands Gesture System`
  - `Left Hand Gestures`
  - `Right Hand Gestures`

주의:

- `GameSystems.prefab`에는 `GestureDetector`와 `GestureEventRouter`만 있고, `XR Hands Gesture System`은 없다.
- 따라서 `GameSystems.prefab`을 갱신하거나, 별도의 `ArcaneXRGestureInput.prefab`로 분리하는 선택지가 있다.

### 3. Arcane Spell Runtime 모듈

역할:

- 음성 인식 결과와 gesture 결과 조합
- 마법 발사
- 마나/피드백 연동

후보 구성:

- `SpellCaster`
- `VoiceSystem`
- `CombinationChecker`
- `CombatManager`
- `FeedbackManager`
- `SpellSpawnRoot`

주의:

- 재스처 담당 범위에서는 `CombatManager`와 보스/제약 시스템을 깊게 손대지 않고, 마법 발사가 호출 가능한 최소 연결만 확인하는 쪽이 좋다.

## OpenWorld2 이식 전 확인 순서

1. Main 씬에서 XR Hands만으로 Fire/Ice/Thunder/Grimoire/PageTurn 이벤트가 들어오는지 다시 확인한다.
2. `ManaWristDisplay`, `GrimoireManager`, `SpellCaster`, `GestureDetector`의 OVR fallback 경로를 비활성화하거나 XR 경로와 분리한다.
3. `ArcaneXRGestureInput.prefab` 또는 갱신된 `GameSystems.prefab` 중 어느 단위로 가져갈지 결정한다.
4. OpenWorld2에는 먼저 삭제 없이 additive 또는 별도 테스트 복제 씬에서 XR Rig + Gesture Input + Spell Runtime을 배치한다.
5. OpenWorld2의 기존 `Main Camera`와 XR Origin 카메라 중복을 처리한다.
6. `=== Gameplay_Targets_Combat ===/Dummy`를 임시 마법 타격 대상으로 써서 Fire/Ice/Thunder 입력부터 검증한다.
7. 이후 보스/전투/진행 관리 시스템은 조원 작업 병합 이후 연결한다.

## 비파괴 정리 순서 제안

1. Main 씬에서 `_Disabled/OVRCameraRig`와 `_Disabled/Arcane Meta Gameplay Gesture Selectors`를 유지한 채, 더 명확한 이름으로 보관 목적을 표시한다.
   - 예: `_Disabled/Legacy_MetaOVR_DoNotDeleteYet`
   - 단, 이 단계는 씬 변경이므로 팀 합의 후 진행.
2. `Input Systems/Meta Interaction UnityXRInteraction`이 현재 XR Hands 녹화/검증에 필요한지 플레이 모드에서 검증한다.
   - 필요하면 `Input Collection` 계층의 UnityXR provider로 유지.
   - 필요 없으면 비활성 보관 후보로만 표시하고 삭제하지 않는다.
3. `ManaWristDisplay`를 OVRHand 기반에서 XR wrist transform 기반으로 전환한다.
   - Main 씬의 `Right Hand Tracking` 또는 `Right Hand Gestures`의 wrist transform을 명시 참조하도록 바꾸는 것이 우선순위가 높다.
4. `GrimoireManager`의 OVR fallback 입력 책임을 `LeftGrimoireGesture`/`RightPageTurnGesture`로 이관한다.
   - `GrimoireManager`는 UI/상태/이벤트 관리에 집중.
5. `SpellCaster`의 OVR prototype casting 경로를 XR gesture router 기반 casting으로 대체한다.
   - 목표 구조도상 `GestureDetector -> CombinationChecker/SpellCaster` 흐름으로 정리.
6. `GestureDetector`의 OVR skeleton pose detection을 제거하거나 legacy adapter로 격리한다.
   - 즉시 삭제보다 `LegacyOvrGestureDetectorAdapter` 같은 별도 파일로 분리하는 방식이 안전하다.
7. `ArcanePlayerRigResolver`, `BossBattleRuntimeBinder`, `HandPullMovementController`의 `OVRCameraRig` fallback을 `XROrigin`/Camera 기반 resolver로 치환한다.
8. Missing Script 오브젝트는 조원 개발분 병합 전까지 보류한다.

## 다음 작업 체크리스트

- [ ] Main 씬에 비파괴 legacy 보관 계층명을 적용할지 결정.
- [ ] `Input Systems/Meta Interaction UnityXRInteraction`의 현재 필요 여부 플레이 모드 검증.
- [ ] `ManaWristDisplay` XR wrist transform 전환.
- [ ] `GrimoireManager` OVR fallback 제거 전 `LeftGrimoireGesture` 이벤트 연결 확인.
- [ ] `SpellCaster` OVR prototype field와 XR gesture router 경로 분리.
- [ ] `GestureDetector` OVR skeleton 검출 경로 격리 또는 제거.
- [ ] OpenWorld2 테스트 복제 씬 또는 additive 테스트 방식 결정.
- [ ] XR Rig + XR Gesture Input + Spell Runtime 프리팹 경계 결정.
- [ ] Missing Script 항목은 조원 개발분 병합 전까지 보류.

## 2026-06-02 Main 씬 정리 적용 결과

Main 씬 하이라키를 목표 구조도에 맞춰 우선 보기 쉬운 계층으로 재분류했다.

현재 루트:

- `00_InputCollection_PlayerXR`
  - `XR Origin`
  - `XR Hands Gesture System`
- `01_InputInterpretation`
  - `CombinationFocusModeController`
  - `Arcane Time Focus`
- `02_MagicCombatCore`
  - `GameSystems`
  - `GrimoireSystem`
  - `SpellSpawnRoot`
- `03_Feedback`
  - `AuraParticles`
- `04_TestEnvironment`
  - `Environment`
- `90_Legacy_ToReview_DoNotDeleteYet`
  - `Input Systems`
  - `_Disabled`
  - `GrimoireBook`

삭제 대신 격리한 이유:

- `_Disabled`와 기존 `Input Systems`는 구 Meta/OVR 또는 recorder/provider 성격이 강하지만, 활성 씬 루트 삭제는 되돌리기 어려워 안전 검토에서 거부되었다.
- 따라서 에셋/씬 오브젝트 삭제 없이 `90_Legacy_ToReview_DoNotDeleteYet` 아래 비활성 격리했다.
- 이후 명시 승인 또는 팀 합의가 있으면 이 루트만 삭제 대상으로 검토하면 된다.

Unity XR 기준으로 바꾼 참조:

- `SpellCaster`
  - `rightHandSpawnPoint`: `R_Wrist`
  - `headTransform`: XR Origin의 `Main Camera`
  - `prototypeHand`: `null`
  - `prototypeSpawnPoint`: `R_Wrist`
  - `prototypeTrackingSpaceRoot`: `null`
  - `enableGesturePrototype`: `false`
- `GestureDetector`
  - `leftOvrHand`: `null`
  - `rightOvrHand`: `null`
  - `useXrHandsStaticGestureRouter`: `true`
  - `allowOvrPrototypeOverrideRouter`: `false`
- `ManaWristDisplay`
  - OVRHand 직접 참조 대신 XR `R_Wrist` Transform을 쓰도록 스크립트와 씬 참조를 변경.
- `LeftGrimoireGesture`
  - 씬 안의 비활성 `GrimoireBook` 대신 `Assets/Prefabs/GrimoireBook.prefab`을 참조.
- `CombinationFocusModeController`
  - `headTransform`: XR Origin의 `Main Camera`
  - `combinationChecker`, `handPullMovement`: 활성 `GameSystems` 내부 컴포넌트로 재연결.

검증:

- Unity 콘솔 오류/경고 없음.
- 필수 활성 경로 확인 완료:
  - `00_InputCollection_PlayerXR/XR Origin`
  - `00_InputCollection_PlayerXR/XR Hands Gesture System/Left Hand Gestures`
  - `00_InputCollection_PlayerXR/XR Hands Gesture System/Right Hand Gestures`
  - `02_MagicCombatCore/GameSystems/InputSystems/GestureDetector`
  - `02_MagicCombatCore/GameSystems/SpellSystem/SpellCaster`
  - `02_MagicCombatCore/GameSystems/UIManagers/ManaWristDisplay`
- Main 씬 저장 완료.
