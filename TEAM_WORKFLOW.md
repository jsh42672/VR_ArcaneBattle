# VR Arcane Battle Team Workflow

이 문서는 팀원이 clone 후 바로 역할, 브랜치, 공유 파일 규칙을 확인하기 위한 협업 기준입니다.

## 전체 작업 목록

| 카테고리 | 내용 |
| --- | --- |
| A. 입력 & 마법 | 손 추적, 제스처, 마법 발사 |
| B. 전투 & AI | 충돌 판정, 보스 AI, 이동/회피 |
| C. 아트 & 환경 | 맵, 보스 모델, 이펙트, 사운드 |
| D. UI/씬 | 피드백, 마도서, 씬 전환 |

## 역할 분담

### 전수환: 입력 & 마법 시스템

| 컴포넌트 | 작업 내용 |
| --- | --- |
| `GestureDetector` | Meta XR SDK Hand Tracking API 연동, 포즈 ID 출력 |
| `CombinationChecker` | 0.5초 윈도우 내 양손 조합 판정 |
| `VoiceRecognizer` | STT 연동, Fire/Ice/Thunder 감지 |
| `SpellCaster` | 손 위치 기반 마법 Spawn, Head 방향 조준 보정 |
| `SpellDatabase` | ScriptableObject 조합표 + 속성 데이터 |

담당 산출물:

- 단일 마법 발사 동작
- 양손 조합 마법 발사 동작
- 보이스 주문 마나 환급 동작
- 마법 이펙트 프리팹 제작: `Assets/Art/VFX/Spells/`
- 마법 프리팹 코드 + 비주얼 통합: `Assets/Prefabs/Spells/`

### 노경민: 전투 & 보스 AI

| 컴포넌트 | 작업 내용 |
| --- | --- |
| `CombatManager` | 충돌 판정 + 속성 효과 + HP 관리 통합 |
| `DodgeDetector` | Head Y/X축 이동 15cm 이상 회피 판정 |
| `MovementController` | Hand Pull 이동 처리 |
| `ConstraintController` | 속박 발동, 위치 보정, 이동 잠금 |
| `BossAI` | 상태 머신: Idle/Defense/Charging/Weakness/Dead |

담당 산출물:

- 골렘 HP 구간별 상태 전환: 70% / 40% / 15%
- 25초 주기 방어 상태 반복
- 차징 시 속박 자동 발동
- 회피 성공/실패 판정
- 보스 공격 패턴 로직

### 이동현: 아트 & 환경 + UI/씬

아트 작업 비중이 크므로 코드 담당은 상대적으로 적게 배분합니다.

| 작업 | 내용 |
| --- | --- |
| 맵 환경 | WorldMapScene 지형, 조명, 분위기 세팅 |
| 전투 아레나 | CombatScene 아레나 환경, 속성별 4개 |
| 골렘 모델 | 화염/냉기/전기/보스 골렘 외형, 에셋 활용 또는 제작 |
| 이펙트 | 피격, 골렘 상태, 균열, 환경 ambient 이펙트 |
| 사운드 | 마법 발사음, 피격음, 환경음, 보이스 피드백음 |
| 조명 | 속성별 아레나 분위기 조명 |

| 컴포넌트 | 작업 내용 |
| --- | --- |
| `FeedbackManager` | 화면 비네팅 + 손 오라 + 공간 사운드 통합 |
| `GrimoireManager` | 마도서 소환/해제 + 페이지 UI + 진행도 |
| `GameSceneManager` | 씬 전환 전체: Title -> Tutorial -> World -> Combat -> Result |

담당 산출물:

- 화면 비네팅, HP 연동
- 손 오라 색상, 속성 연동
- 골렘 HP 표현, 코어 밝기 + 균열 이펙트
- 마도서 UI 3페이지
- 씬 전환 및 속성 해금 반영

## Git 브랜치 전략

```text
main
  발표용. 항상 동작하는 상태를 유지합니다.

dev
  통합 개발 브랜치. feature 브랜치는 dev에서 시작하고 dev로 Pull Request를 보냅니다.

feature/input-spell
  전수환: 입력 & 마법 시스템

feature/combat-boss
  노경민: 전투 & 보스 AI

feature/art-ui-scene
  이동현: 아트 & 환경 + UI/씬
```

규칙:

- `main`에 직접 push 금지
- 개인 작업은 각자 `feature/...` 브랜치에 push
- `dev` 머지 전 Pull Request 생성
- Pull Request는 1명 이상 리뷰 후 merge
- 발표 가능한 안정 상태만 `dev`에서 `main`으로 merge
- 공유 파일 수정 시 팀 채팅에 사전 공지

## 팀원별 시작 명령어

전수환:

```bash
git clone https://github.com/jsh42672/VR_ArcaneBattle.git
cd VR_ArcaneBattle
git checkout feature/input-spell
```

노경민:

```bash
git clone https://github.com/jsh42672/VR_ArcaneBattle.git
cd VR_ArcaneBattle
git checkout feature/combat-boss
```

이동현:

```bash
git clone https://github.com/jsh42672/VR_ArcaneBattle.git
cd VR_ArcaneBattle
git checkout feature/art-ui-scene
```

작업 후 공통:

```bash
git status
git add .
git commit -m "작업 내용 요약"
git push origin 현재-브랜치명
```

예시:

```bash
git push origin feature/input-spell
```

## 컴포넌트 간 이벤트 약속

팀원 A 발행, 팀원 B/C 구독:

```csharp
GestureDetector.OnPoseDetected(PoseId left, PoseId right)
CombinationChecker.OnCombinationSuccess(SpellId spellId)
CombinationChecker.OnCombinationFail()
VoiceRecognizer.OnVoiceCommand(ElementType element)
```

팀원 B 발행, 팀원 A/C 구독:

```csharp
CombatManager.OnPlayerHit(float damage)
CombatManager.OnBossHit(float damage, ElementType element)
BossAI.OnStateChanged(BossState newState)
ConstraintController.OnConstraintStart()
ConstraintController.OnConstraintEnd()
DodgeDetector.OnDodgeSuccess()
```

팀원 C 발행, 팀원 A/B 구독:

```csharp
GrimoireManager.OnGrimoireOpen()
GrimoireManager.OnGrimoireClose()
GameSceneManager.OnSceneLoaded(SceneType scene)
```

## 공유 에셋 담당자

| 파일 | 담당 | 수정 시 공지 대상 |
| --- | --- | --- |
| `Assets/Data/SpellDatabase.asset` | 전수환 | 노경민, 이동현 |
| `Assets/Data/GolemConfig.asset` | 노경민 | 전수환, 이동현 |
| 골렘 프리팹 | 이동현 | 노경민 |
| `Assets/Prefabs/Spells/` | 전수환 | 이동현 |
| `Assets/Art/VFX/Spells/` | 전수환 | 이동현 |

## 충돌 줄이기

- `ProjectSettings/`, `Packages/`, `Assets/XR/`는 설정 변경 전 팀 채팅에 공유합니다.
- Unity `.meta` 파일은 반드시 함께 커밋합니다.
- `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`는 커밋하지 않습니다.
- 큰 에셋을 추가하기 전 Git LFS 적용 여부를 확인합니다.
