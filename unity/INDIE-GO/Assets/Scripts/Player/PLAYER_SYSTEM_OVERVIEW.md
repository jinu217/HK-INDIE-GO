# Player 캐릭터 시스템 기능 명세

이 문서는 `Assets/Scripts/Player` 아래에 구현된 시스템이 무엇을 담당하고,
인게임에서 어떤 순서로 동작하며, 다른 시스템과 어디에서 연결되는지 설명한다.

`Player` 폴더는 플레이어 자체의 턴·SP·승리 규칙 전체를 소유하는 폴더가 아니다.
주된 책임은 **플레이어에게 배정된 캐릭터 말의 패시브·액티브 스킬을 인게임 규칙에 연결하는 것**이다.

> 작업 경계: 캐릭터 구현은 `Scripts/Player` 안에서 완결한다. 씬, Hierarchy, 턴/이동/승리 매니저에
> 추가 변경이 필요하다면 Player 코드가 외부 구현을 복제하지 말고 필요한 호출 지점만 협의한다.

## 1. Player 폴더가 제공하는 기능

Player 폴더는 다음 기능을 제공한다.

- 캐릭터별 데이터와 패시브·액티브 상태값 정의
- 플레이어당 말 4개와 캐릭터 프리팹 연결
- `(PlayerId, PieceId)` 기준의 캐릭터 런타임 등록과 조회
- 이동 전 이동 수 보정
- 윷 결과 확률 보정
- 추가 던지기 허용 여부 보정
- 잡기 전 방어·분신·부품 전환 판정
- 보드 진입, 이동 완료, 잡기 완료, 리타이어, 턴 시작·종료 이벤트 처리
- 액티브 스킬의 시전자·대상·턴 단계 검증과 실행
- 스킬용 보드 거리·경로 조회와 직접 이동·잡기 도우미
- 기본 액티브 버튼의 런타임 생성과 외부 선택 UI용 공개 API
- 캐릭터 프리팹과 Classic 종료 경로를 확인하는 에디터 전용 검증 도구

다음 기능은 Player 폴더의 최종 소유 대상이 아니다.

- 로비와 챔피언 선택 흐름
- 게임 모드 선택과 씬 전환
- 윷 던지기 입력 UI 전체
- 플레이어 자원의 최종 저장·동기화 정책
- 전체 턴 진행과 네트워크 권한
- 설치형 아이템의 실제 충돌 처리
- 승리 조건과 EndGame 화면
- 캐릭터 애니메이션, VFX, 사운드와 최종 대상 선택 UI

## 2. 파일별 책임

| 파일/종류 | 책임 |
|---|---|
| `CharacterData.cs` | 캐릭터 이름, 설명, 아이콘, 스킬 설명, 상태 enum, 외형과 런타임 설정 데이터 정의 |
| `CharacterSkillContracts.cs` | 이동·잡기·액티브 요청과 결과, 이동 기록 등 외부 통신 규격 정의 |
| `CharacterStatusBehaviour.cs` | 등록, 공통 검증, 턴 이벤트, 보호 효과, 패시브 기반 기능을 제공하는 추상 기반 클래스 |
| `CharacterSkillRegistry.cs` | 외부 인게임 코드가 사용하는 단일 진입점과 플레이어/말별 스킬 등록소 |
| `CharacterBoardUtility.cs` | 보드 조회, 거리·경로 계산, 스킬 이동·잡기와 아군 업기 처리 |
| `ActiveSkillButtonController.cs` | 현재 턴의 캐릭터를 선택해 액티브 요청을 만들고 결과를 턴 시스템에 전달 |
| `CHAR_xxx_Status.cs` | 캐릭터별 패시브와 액티브의 실제 규칙 구현 |
| `CHAR_xxx_SO.asset` | 캐릭터별 `CharacterData` 값 저장 |
| `CHAR_xxx.prefab` | 런타임에 플레이어 자식으로 생성되는 캐릭터 말 프리팹 |
| `CharacterSkillRuntimeVerifier.cs` | 캐릭터 등록·스킬·쿨다운·Classic 종료 경로의 에디터 전용 자동 검증 |

## 3. 전체 런타임 구조

```text
PlayerManager
└─ PlayerController (P1~P4)
   └─ 캐릭터 말 프리팹 × 4
      └─ CHAR_xxx_Status : CharacterStatusBehaviour
         └─ CharacterData (CHAR_xxx_SO.asset)

PieceMovementManager ─┐
TestTurnManager       ├─ CharacterSkillRegistry ─ CHAR_xxx_Status
TestYutRuleManager    ┘

ActiveSkillButtonController
└─ CharacterActiveRequest ─ CharacterSkillRegistry.TryUseActive(...)
```

외부 시스템은 캐릭터별 `CHAR_xxx_Status` 클래스를 직접 참조하지 않는다.
모든 공통 호출은 `CharacterSkillRegistry`와 `CharacterSkillContracts`의 요청 구조체를 사용한다.
이 구조를 지켜야 새 캐릭터를 추가해도 이동·턴 매니저에 캐릭터별 분기가 늘어나지 않는다.

`CharacterSkillStatus`는 UI나 외부 시스템이 효과 종류를 식별하기 위한 메타데이터다.
상태 enum만 지정한다고 스킬이 자동 실행되는 것은 아니며, 실제 동작은 각 Status 클래스의 override가 담당한다.

## 4. 캐릭터 프리팹과 말 4개 등록

한 플레이어가 말 4개를 사용한다면 선택된 동일 캐릭터 프리팹을
해당 `PlayerController` 자식으로 4개 생성하는 구성을 전제로 한다.

각 프리팹에는 다음 항목이 필요하다.

1. 캐릭터에 대응하는 `CHAR_xxx_Status` 컴포넌트
2. Status 컴포넌트의 `characterData`에 연결된 `CHAR_xxx_SO.asset`
3. 명시적으로 고정할 경우 `pieceId` 0~3, 자동 배정할 경우 `pieceId = -1`
4. 선택 사항인 `CharacterData.visualModelPrefab`

등록 과정은 다음과 같다.

1. `Awake`에서 CharacterData를 검증하고 외형 프리팹이 있으면 자식으로 생성한다.
2. `OnEnable`과 `Start`에서 부모 `PlayerController` 등록을 시도한다.
3. PlayerController 초기화가 늦으면 등록될 때까지만 `Update`에서 재시도한다.
4. 등록 성공 시 `CharacterSkillRegistry`에 `(PlayerId, PieceId)` 키로 저장한다.
5. 비활성화되면 레지스트리에서 해당 컴포넌트를 해제한다.

등록 예외와 방어 동작:

- 부모에 초기화된 `PlayerController`가 없으면 즉시 실패하지 않고 초기화를 기다린다.
- `pieceId = -1`이거나 다른 말이 이미 사용 중이면 0부터 비어 있는 PieceId를 찾는다.
- 플레이어의 실제 말 개수 밖인 PieceId는 등록 오류다.
- 네 PieceId가 모두 사용 중인데 다섯 번째 프리팹이 등록되면 오류를 기록하고 비활성화한다.
- CharacterData가 없으면 오류를 기록하며 상태 조회와 외형 생성이 불가능하다.
- `visualModelPrefab`이 비어 있어도 캐릭터 스킬 로직은 동작한다.
- 자기 자신을 `visualModelPrefab`으로 지정하면 무한 재귀 생성을 막기 위해 생성하지 않는다.

## 5. CharacterData와 상태값

`CharacterData`에는 다음 데이터가 있다.

- 기본 정보: `char_ID`, `char_Name`, `char_Desc`, `char_Icon`
- 패시브 정보: `passive_Name`, `passive_Desc`, `passive_Status`
- 액티브 정보: `active_Name`, `active_Desc`, `active_Status`
- 런타임 설정: `active_CooldownTurns`, `active_SkillPointCost`, `passive_CooldownTurns`
- 외형: `visualModelPrefab`

현재 정의된 `CharacterSkillStatus`:

```text
None, Get_point, Get_turn, Move_1, Transformation,
Shield, Hide, No_back, Kill_atk, Catch_atk,
Move_end, Binding
```

상태값이 지정되지 않은 캐릭터는 `None`을 유지한다.
새 효과의 의미가 기존 상태값과 다르면 억지로 재사용하지 않고 새 enum 항목을 추가한다.

## 6. CharacterSkillRegistry의 역할

`CharacterSkillRegistry`는 Player 폴더 밖에서 캐릭터 기능을 호출하는 단일 진입점이다.
주요 역할은 다음과 같다.

- `(PlayerId, PieceId)`에 대응하는 정확한 `CharacterStatusBehaviour` 조회
- 이동 수, 윷 확률, 추가 던지기 결과의 캐릭터별 보정
- 잡기 전 대상 캐릭터의 방어 판정
- 보드 진입·이동·잡기·리타이어·턴 이벤트 전달
- 액티브 공통 조건과 플레이어별 SP·쿨다운 검사
- Yut/Turn 매니저에 대한 중복 없는 런타임 브리지 구성
- 새 게임 시작 시 정적 런타임 상태 초기화

캐릭터가 등록되지 않은 말에 대한 기본 동작은 기존 인게임 규칙을 그대로 유지하는 것이다.

- 이동 수: 전달받은 값 반환
- 윷 확률: 기존 확률표 반환
- 추가 던지기: 기본 판정 반환
- 잡기: `Proceed`
- 대상 지정: 허용
- 액티브: 실패 결과와 원인 메시지 반환

## 7. 현재 인게임 호출 지점

아래 호출은 현재 `PieceMovementManager`와 `TestTurnManager`에 연결되어 있다.
외부 코드를 다시 구성할 때도 호출 시점과 의미를 유지해야 한다.

| 시점 | Registry 호출 | 반환값/역할 |
|---|---|---|
| 이동 수 확정 전 | `ModifyMoveCount(request)` | 최종 이동 수 보정 |
| 말이 대기 상태에서 보드로 진입한 직후 | `NotifyPieceEnteredBoard(playerId, pieceId)` | 입장형 패시브 발동 |
| 상대 말을 실제로 잡기 전 | `EvaluateIncomingCapture(request)` | 잡기·방어·분신·부품 판정 |
| 잡힌 말의 상태 변경 직후 | `NotifyPieceRetired(playerId, pieceId)` | 리타이어 초기화 실행 |
| 잡기 확정 직후 | `NotifyCaptureCompleted(request)` | 공격자에게 잡기 성공 전달 |
| 이동과 착지 처리 완료 후 | `NotifyMoveCompleted(record)` | 이동자와 전체 캐릭터에 이동 기록 전달 |
| 추가 던지기 확정 전 | `ShouldGrantExtraThrow(playerId, result, defaultValue)` | 기본 추가 던지기 결과 보정 |
| 지정형 대상 후보 표시·실행 전 | `IsTargetable(playerId, pieceId)` | 은신·부품 등 지정 불가 상태 제외 |
| 액티브 버튼 입력 | `TryUseActive(request)` | 공통 검증 후 해당 말의 액티브 실행 |

윷 확률과 턴 이벤트는 캐릭터 등록 시 `EnsureManagerBridges`가 자동 연결한다.

- 기존 `TestYutRuleManager.ProbabilityTableProvider`가 있으면 보존한 뒤 캐릭터 확률 보정을 덧붙인다.
- `OnTurnStarted`에서 액티브 쿨다운과 패시브 지속 턴을 갱신한다.
- `OnTurnEnded`에서 턴 종료형 예약 효과를 정리한다.
- 같은 매니저에 중복 구독하지 않도록 브리지된 인스턴스를 기록한다.
- 새 게임/도메인 초기화 시 등록, 브리지, SP, 쿨다운과 이벤트 상태를 초기화한다.

## 8. 이동 처리

일반 이동은 원본 이동 수를 실제 보드에 적용하기 전에 캐릭터에게 전달한다.

```csharp
var request = new CharacterMoveRequest(
    playerId,
    pieceId,
    rolledMoveCount,
    isFirstBoardMove,
    isActiveSkillMove);

int finalMoveCount = CharacterSkillRegistry.ModifyMoveCount(request);
```

`CharacterMoveRequest` 필드:

- `PlayerId`, `PieceId`: 이동하는 말의 레지스트리 키
- `MoveCount`: 캐릭터 효과 적용 전 이동 수. 뒷도는 음수다.
- `IsFirstBoardMove`: 대기 말이 처음 보드에 올라가는 이동인지 여부
- `IsActiveSkillMove`: 액티브가 직접 일으킨 이동인지 여부

`IsActiveSkillMove`는 액티브 이동 도중 일반 이동 패시브가 다시 적용되는 것을 막는다.
실제 이동이 끝나면 `CharacterMoveRecord`를 전달한다.

- `From`, `To`: 시작과 도착 타일
- `Path`: 실제로 통과한 타일 전체
- `IgnoresInstalledItems`: 설치형 아이템 무시 요청 표식

`IgnoresInstalledItems`는 표식만 제공한다. 실제 아이템 시스템이 이동 기록을 읽고
아이템 효과를 건너뛰어야 완전한 설치형 아이템 무시가 된다.

## 9. 잡기 처리

대상 상태를 바꾸기 전에 `CharacterCaptureRequest`를 생성하고 판정해야 한다.

```csharp
var request = new CharacterCaptureRequest(
    attackerPlayerId,
    attackerPieceId,
    targetPlayerId,
    targetPieceId,
    attackingPieceCount,
    wouldGrantExtraThrow);

CharacterCaptureDecision decision =
    CharacterSkillRegistry.EvaluateIncomingCapture(request);
```

잡기 결정의 의미:

- `Proceed`: 기존 잡기 처리 진행
- `Prevent`: 잡기 무효
- `LimitRetireToAttackingCount`: 공격측 말 수만큼만 대상 스택 리타이어
- `ConsumeCloneWithoutBonus`: 실제 말 대신 분신 하나만 제거하고 추가 던지기 금지
- `ConvertToParts`: 리타이어하지 않고 해당 타일의 부품 상태로 유지

`Prevent`, `ConsumeCloneWithoutBonus`, `ConvertToParts`는 일반적인 잡기 성공이 아니다.
공격자에게 잡기 보상이나 추가 던지기를 부여하면 안 된다.
실제 잡기가 완료된 경우에만 `NotifyPieceRetired`와 `NotifyCaptureCompleted`를 호출한다.

지정형 스킬은 후보 표시 시점과 실행 직전에 모두 `IsTargetable`을 검사한다.
일반 착지 잡기는 `IsTargetable`만으로 끝내지 않고 반드시 `EvaluateIncomingCapture`까지 호출한다.

## 10. CharacterBoardUtility

캐릭터 스킬이 외부 매니저의 내부 구현을 직접 복제하지 않도록 공통 보드 기능을 제공한다.

- 특정 플레이어/말 조회
- 보드 위 전체 말 또는 적 말 목록 생성
- 그래프 최단거리 계산
- 가장 가까운 아군 탐색
- 현재 경로 기준 전방·후방 타일 계산
- 지정 걸음 수만큼 전방 경로 생성
- 스킬 잡기와 리타이어 처리
- 스택 전체를 경로에 따라 이동
- 같은 타일의 아군과 업기 그룹 생성
- 이동 완료 기록 전달

스킬이 직접 이동할 때도 말 데이터만 임의 변경하고 끝내지 말고,
가능하면 Utility 또는 `PieceMovementManager`를 사용해 이동 완료 이벤트까지 발생시킨다.

## 11. 액티브 요청과 공통 검증

외부 UI는 다음 요청을 만들고 Registry에 전달한다.

```csharp
var request = new CharacterActiveRequest(
    playerId,
    casterPieceId,
    targetPlayerId,       // 대상이 없으면 -1
    targetPieceId,        // 대상이 없으면 -1
    selectedYutResult);   // 선택 결과가 없으면 YutResult.None

CharacterActiveResult result = CharacterSkillRegistry.TryUseActive(request);
```

공통 검증 순서:

1. 시전자 PlayerId/PieceId에 등록된 캐릭터 확인
2. 액티브 스킬 보유 여부 확인
3. 플레이어 단위 액티브 쿨다운 확인
4. 플레이어 단위 SP 확인
5. 컴포넌트 등록 상태와 자기 턴 여부 확인
6. 현재 턴 단계에서 사용 가능한지 확인
7. 시전자 말 데이터와 Goal 여부 확인
8. 캐릭터별 대상·거리·상태 조건 확인
9. 스킬 성공 후에만 SP 차감과 쿨다운 시작

실패하면 `CharacterActiveResult.Succeeded`가 `false`이며 `Message`에 원인이 들어간다.
실패한 요청은 SP를 차감하거나 쿨다운을 시작하지 않는다.
`SuppressExtraThrow`가 `true`이면 턴 시스템은 해당 스킬 결과로 추가 던지기를 만들지 않는다.

기본 액티브 사용 단계는 `TurnPhase.WaitAction`이다.
윷을 던지기 전에 쓰는 캐릭터는 `CanUseActiveDuringPhase`를 override해 `WaitThrow`를 허용한다.

## 12. SP와 쿨다운의 현재 단위

현재 SP와 액티브 쿨다운은 말 인스턴스별 값이 아니라 Registry에서 플레이어 단위로 관리한다.

- SP 키: `playerId`
- 액티브 쿨다운 키: `(playerId, CharacterData)`
- 액티브 쿨다운 감소: 해당 플레이어의 턴 시작 시
- SP 획득 알림: `SkillPointRequested(playerId, amount)`
- 패시브 쿨다운: 말마다 발동하는 패시브를 위해 각 Status 인스턴스가 보관

같은 플레이어의 동일 캐릭터 말 4개는 같은 CharacterData를 사용하므로 액티브 쿨다운을 공유한다.
다른 플레이어는 같은 캐릭터를 선택해도 SP와 쿨다운을 공유하지 않는다.

향후 별도 플레이어 자원 시스템이 최종 권한을 갖게 되면 SP 저장·검증·차감과 쿨다운 표시를
그 시스템으로 이전해야 한다. Registry와 외부 시스템에서 이중 차감하지 않도록 소유권을 한 곳으로 정한다.

## 13. 액티브 버튼과 선택 UI

`ActiveSkillButtonController` 공개 API:

- `SetCasterPiece(pieceId)`: 말 선택 UI가 시전자 지정
- `ClearCasterPiece()`: 시전자 선택 해제
- `SetTarget(playerId, pieceId)`: 대상 선택 UI가 적 말 지정
- `ClearTarget()`: 대상 선택 해제
- `SetSelectedYutResult(result)`: 결과 선택형 스킬 인자 설정
- `ClearSelectedYutResult()`: 결과 선택 해제
- `RefreshForCurrentTurn()`: 현재 턴과 선택 상태로 버튼 갱신
- `SkillUseCompleted`: 코드에서 성공·실패 결과를 수신하는 이벤트
- `onSkillSucceeded`, `onSkillFailed`: Inspector에서 메시지나 연출을 연결하는 UnityEvent

시전자를 명시하지 않으면 현재 플레이어의 보드 위 말, 대기 말 순서로 첫 사용 가능 캐릭터를 고른다.
첫 시전자가 실패하고 명시 선택이 없으면 다른 사용 가능 말도 순서대로 시도한다.
대상이 없을 때 일부 스킬은 자동으로 유효 대상을 찾지만, 실제 대상 강조·선택·취소 UI는
외부 UI 시스템에서 위 API에 연결해야 한다.

현재 컨트롤러는 `InGameScene` 로드 후 Canvas 아래에 유효한 컨트롤러가 없으면
`InGameSkillCanvas`, `EventSystem`, `ActiveSkillButton`을 런타임에 자동 생성한다.
이 오브젝트들은 실행 전 Hierarchy에 보이지 않으며 플레이 모드 종료 시 사라진다.
자동 버튼은 연결 확인용 기본 UI이고, 최종 UI가 씬에 배치되면 씬의 컨트롤러를 우선 사용한다.

## 14. 캐릭터별 구현 기능

| 캐릭터 | 패시브 | 액티브 |
|---|---|---|
| `CHAR_001_1` 기본형 남자 | 첫 보드 이동 +1, 리타이어 시 재사용 | 다음 윷을 도/모 50%로 제한 (`WaitThrow`) |
| `CHAR_001_2` 기본형 여자 | 없음 | 윷을 던진 뒤 스킬 추가 던지기 예약 |
| `CHAR_002` 전우치 | 보드 진입 시 가장 가까운 아군에게 1회 보호 | 다음 일반 이동 수 2배 |
| `CHAR_003` 홍길동 | 잡힐 때 공격측 말 수만큼만 실제 말 리타이어 | 분신 추가, 분신 피격 시 잡기 보상 차단 |
| `CHAR_004` 구미호 | 말마다 일반 잡기 1회 방어 | 3번의 소유자 턴 동안 지정과 일반 착지 잡기 방어 |
| `CHAR_005` 사무라이 | 뒷도 확률을 윷·모에 절반씩 분배 | 전방 3칸 이동하며 경로 적 처리, 추가 던지기 억제 |
| `CHAR_006` 판타지 용사 | 25% 확률로 잡기 방어 | 현재 칸 또는 앞 1칸의 적 잡기 |
| `CHAR_007` 판타지 오크 | 적 잡기 완료 시 SP 요청 | 직선 끝까지 이동, 첫 적 스턴, 설치형 아이템 무시 표식 |
| `CHAR_008` 판타지 엘프 | 이전 이동 경로에 정지한 아군 1칸 추가 이동 | 거리 5 이내 적 속박 |
| `CHAR_009` SF 로봇 | 잡힐 때 3턴간 부품 유지, 아군 접근 시 부활 | 주변 거리 1의 모든 말과 자신 리타이어 |
| `CHAR_010` SF 군인 | 업은 실제 말 수만큼 이동 수 증가 | 1칸 후진, 추가 던지기 억제 |
| `CHAR_018` 암살자 | 보드 진입 시 목표 지정, 목표 잡기 시 SP 요청 | 보드 위 적 위치로 이동 후 잡기 시도 |
| `CHAR_019` 도깨비 | 윷·모의 추가 던지기 기회를 턴당 1회 추가 | 다음 결과가 도·개·걸·뒷도일 때만 추가 던지기 (`WaitThrow`) |

캐릭터 설명에 적힌 시각적 분신, 부품 외형, 목표 표시, 은신 이펙트 등은
상태 판정과 별개의 연출 계층이다. 관련 SpriteRenderer나 최종 UI/VFX가 없으면 로직만 적용된다.

## 15. 새 캐릭터 추가 절차

1. CharacterData 에셋을 만들고 기본 정보, 스킬 설명과 상태값을 입력한다.
2. `CharacterStatusBehaviour`를 상속하는 `CHAR_xxx_Status` 클래스를 만든다.
3. 필요한 hook만 override하고 외부 매니저의 private 상태를 직접 참조하지 않는다.
4. 보드 조회와 직접 이동·잡기는 가능한 한 `CharacterBoardUtility`를 사용한다.
5. 실패 가능한 액티브는 명확한 `CharacterActiveResult.Failure(message)`를 반환한다.
6. 실제 효과가 적용된 뒤에만 `CharacterActiveResult.Success`를 반환한다.
7. 캐릭터 프리팹에 Status 컴포넌트와 CharacterData를 연결한다.
8. 플레이어 아래 프리팹 4개를 두고 PieceId 0~3 등록을 확인한다.
9. P1~P4가 같은 캐릭터를 사용할 때 상태, SP, 쿨다운이 플레이어 간 섞이지 않는지 확인한다.
10. 보드 진입, 리타이어 후 재사용, 스택, 방어, 추가 던지기 억제 등 회귀 조건을 검사한다.

외부 시스템이 특정 캐릭터 타입을 검사하는 `if (CHAR_xxx)` 분기는 만들지 않는다.
새 기능은 공통 계약과 hook을 통해 전달한다.

## 16. 에디터 전용 검증 도구

`CharacterSkillRuntimeVerifier`는 운영용 씬 구성 요소가 아니다.
임시 폴더에 `indiego-character-runtime-verifier.flag`가 있을 때만 `InGameScene` 로드 후
자기 GameObject를 동적으로 만든다. 검증 시작 시 플래그를 삭제하고,
플레이 모드 종료 시 동적 GameObject와 런타임 컴포넌트도 사라진다.

검증 범위:

- 13종 캐릭터 프리팹 로드
- 각 캐릭터를 P1~P4의 말 4개에 순환 배치
- `(PlayerId, PieceId)` 등록
- 패시브 핵심 판정
- 액티브 성공 판정
- 플레이어별 쿨다운 격리
- 실제 PieceMovementManager를 통한 Goal 이동
- Classic 규칙이 연결됐을 때 EndGame 진입

Classic 종료 검사에서 씬에 `ClassicModeRule`이 없으면 이를 실패로 기록한다.
이후 Player 외부의 종료 경로도 계속 확인하기 위해 에디터 런타임에만 임시 규칙을 추가한다.
따라서 검증 성공은 씬이나 Hierarchy에 Classic 구성이 영구 저장됐다는 의미가 아니다.

최종 확인은 두 층으로 구분한다.

- Player 로직 검증: 등록, 스킬 판정, 이동·잡기 hook, 플레이어 간 상태 격리
- 실제 게임 통합 검증: 씬 모드 설정, 턴 전체 진행, 선택 UI, 연출, 승리와 결과 화면

Player 로직 검증 통과만으로 전체 게임이 완성됐다고 판단하면 안 된다.
