# 보드 설치형 스킬 런타임 API 사용법

이 문서는 `CHAR_xxx_Status` 같은 캐릭터 스킬 스크립트에서
`BoardTileRuntimeStateManager`를 이용해 보드 칸에 스킬 정보를 설치하고,
조회·수정·제거하는 방법을 설명한다.

## 1. 책임 범위

`BoardTileRuntimeStateManager`가 담당하는 기능:

- 타일별 설치 스킬 정보 보관
- 같은 타일에 여러 플레이어/팀의 스킬 정보 보관
- 설치 ID, 스킬, 캐릭터, 플레이어, 말, 팀 기준 조회
- 남은 턴, 발동 횟수, 중첩 수 변경
- 타일 Anchor를 기준으로 선택적 3D 프리팹 생성 및 제거
- 설치·변경·제거 이벤트 제공

담당하지 않는 기능:

- 스킬 발동 조건의 최종 판단
- 피해, CC, 추가 이동, 리타이어 등의 실제 효과 실행
- `remainingTurns`, `remainingTriggers` 자동 차감
- 턴 종료 및 말 이동 완료 이벤트 자동 구독

실제 효과와 카운터 차감 시점은 각 캐릭터 스킬 코드가 결정한다.

## 2. 네임스페이스와 매니저 가져오기

```csharp
using UnityEngine;
using YutArena.Common;
using YutArena.InGame;
```

캐릭터 스킬에서 최초 한 번 `MapManager`를 찾고 런타임 저장소를 보관한다.

```csharp
private BoardTileRuntimeStateManager boardState;

protected override void Start()
{
    base.Start();

    MapManager mapManager = FindFirstObjectByType<MapManager>();
    if (mapManager != null)
        boardState = mapManager.BoardRuntimeState;
}
```

`MapManager.BoardRuntimeState`는 플레이 중 필요한 컴포넌트를 자동으로 준비한다.
매번 스킬을 사용할 때 `FindFirstObjectByType`을 반복하지 말고 필드에 보관한다.

## 3. 설치 정보 구성

`BoardSkillInstallRequest`에 설치 시점의 정보를 전부 담아 `TryInstall`에 전달한다.

| 필드 | 의미 |
|---|---|
| `tileId` | 설치할 `BoardTileId` |
| `skillId` | 스킬을 구분하는 고유 문자열 |
| `characterId` | `CharacterData.char_ID`; 캐릭터와 무관하면 `-1` |
| `ownerPlayerId` | 설치한 플레이어 ID |
| `ownerPieceId` | 설치한 말 ID |
| `ownerTeam` | 설치한 플레이어의 팀 |
| `remainingTurns` | 남은 지속 턴; 무제한은 `-1` |
| `remainingTriggers` | 남은 발동 횟수; 무제한은 `-1` |
| `stackCount` | 중첩 수; 최솟값은 `1` |
| `runtimeValues` | 스킬별 추가 데이터 |
| `visualPrefab` | 선택적인 3D 표시 프리팹 |
| `visualLocalPosition` | 타일 Anchor 기준 로컬 위치 |
| `visualLocalEulerAngles` | 타일 Anchor 기준 로컬 회전 |
| `visualLocalScale` | 표시 프리팹의 로컬 크기 |

`skillId`는 프로젝트 전체에서 같은 스킬을 항상 같은 문자열로 사용해야 한다.
오타를 막기 위해 각 스킬 클래스에 상수로 선언하는 것을 권장한다.

```csharp
private const string InstalledSkillId = "CHAR_008_WindPath";
```

## 4. 스킬 설치

```csharp
private bool TryInstallWindPath(
    PlayerRuntimeData.PieceRuntimeData caster,
    BoardTileId targetTile,
    GameObject effectPrefab,
    out int installationId)
{
    installationId = -1;
    if (boardState == null || Data == null || caster == null)
        return false;

    TeamSlot ownerTeam = MatchCompositionRule.GetTeamSlot(
        GameStartSettingsHolder.Current,
        (PlayerSlot)PlayerId);

    var request = new BoardSkillInstallRequest
    {
        tileId = targetTile,
        skillId = InstalledSkillId,
        characterId = Data.char_ID,
        ownerPlayerId = PlayerId,
        ownerPieceId = caster.PieceId,
        ownerTeam = ownerTeam,
        remainingTurns = 3,
        remainingTriggers = 1,
        stackCount = 1,
        visualPrefab = effectPrefab,
        visualLocalPosition = Vector3.zero,
        visualLocalEulerAngles = Vector3.zero,
        visualLocalScale = Vector3.one
    };

    if (!boardState.TryInstall(request, out InstalledBoardSkill installed))
        return false;

    installationId = installed.InstallationId;
    return true;
}
```

설치에 성공하면 고유한 `InstallationId`가 발급된다. 동일한 타일에 동일한
스킬을 다시 설치해도 각각 다른 설치 ID와 정보로 저장된다. 중복 설치를 금지해야
한다면 `TryInstall` 호출 전에 필요한 조회 함수를 사용해 직접 검사한다.

`visualPrefab`이 `null`이면 정보만 저장되고 화면 오브젝트는 생성되지 않는다.
프리팹이 있으면 해당 타일 Anchor 아래에 다음과 같이 생성된다.

```text
Outer01
└─ InstalledSkillVisuals
   ├─ CHAR_008_WindPath_1
   └─ 다른스킬_2
```

## 5. 추가 런타임 데이터 설치

공통 필드로 부족한 스킬 전용 값은 `runtimeValues`에 넣는다.

```csharp
request.runtimeValues.Add(new BoardSkillRuntimeValue
{
    key = "ExtraMoveCount",
    valueType = BoardSkillRuntimeValueType.Integer,
    intValue = 1
});

request.runtimeValues.Add(new BoardSkillRuntimeValue
{
    key = "OnlyEnemyCanTrigger",
    valueType = BoardSkillRuntimeValueType.Boolean,
    boolValue = true
});
```

조회할 때는 값 종류를 확인한 뒤 맞는 필드를 읽는다.

```csharp
if (installed.TryGetRuntimeValue("ExtraMoveCount", out BoardSkillRuntimeValue value) &&
    value.valueType == BoardSkillRuntimeValueType.Integer)
{
    int extraMoveCount = value.intValue;
}
```

키 문자열도 스킬 클래스의 상수로 관리하는 것을 권장한다.

## 6. 조회

### 해당 타일에 특정 스킬이 하나라도 있는지

```csharp
bool exists = boardState.HasSkill(
    caster.CurrentTileId,
    InstalledSkillId);
```

### 특정 플레이어가 설치했는지

```csharp
bool exists = boardState.HasSkillOwnedByPlayer(
    caster.CurrentTileId,
    InstalledSkillId,
    PlayerId);
```

### 정확히 특정 말이 설치했는지

```csharp
bool exists = boardState.HasSkillOwnedByPiece(
    caster.CurrentTileId,
    InstalledSkillId,
    PlayerId,
    caster.PieceId);
```

### 특정 팀이 설치했는지

```csharp
TeamSlot team = MatchCompositionRule.GetTeamSlot(
    GameStartSettingsHolder.Current,
    (PlayerSlot)PlayerId);

bool exists = boardState.HasSkillFromTeam(
    caster.CurrentTileId,
    InstalledSkillId,
    team);
```

### 특정 캐릭터의 스킬인지

```csharp
bool exists = boardState.HasSkillFromCharacter(
    caster.CurrentTileId,
    InstalledSkillId,
    Data.char_ID);
```

### 첫 번째 일치 항목 가져오기

```csharp
if (boardState.TryFindSkill(
        caster.CurrentTileId,
        InstalledSkillId,
        out InstalledBoardSkill installed))
{
    int ownerPlayerId = installed.OwnerPlayerId;
    int remainingTriggers = installed.RemainingTriggers;
}
```

### 타일의 모든 설치 정보 가져오기

```csharp
IReadOnlyList<InstalledBoardSkill> allSkills =
    boardState.GetSkills(caster.CurrentTileId);
```

### 여러 조건을 동시에 적용하기

인자를 생략하거나 `-1`, `TeamSlot.None`, `null`을 전달한 조건은 무시된다.

```csharp
IReadOnlyList<InstalledBoardSkill> matchingSkills = boardState.FindSkills(
    tileId: caster.CurrentTileId,
    skillId: InstalledSkillId,
    characterId: Data.char_ID,
    ownerPlayerId: PlayerId,
    ownerPieceId: caster.PieceId,
    ownerTeam: team);
```

`GetSkills`와 `FindSkills`가 반환하는 목록은 복사된 목록이다. 목록 자체를 변경해도
저장소에는 반영되지 않는다. 저장된 정보 변경은 아래의 수정 함수를 사용한다.

## 7. 설치 정보 수정

모든 수정 함수는 `InstallationId`를 사용한다.

```csharp
boardState.TrySetRemainingTurns(
    installed.InstallationId,
    newRemainingTurns);

boardState.TrySetRemainingTriggers(
    installed.InstallationId,
    newRemainingTriggers);

boardState.TrySetStackCount(
    installed.InstallationId,
    newStackCount);
```

- 남은 턴과 발동 횟수는 `-1` 이상이어야 한다.
- `-1`은 무제한이다.
- 중첩 수는 `1` 이상이어야 한다.
- 매니저가 카운터를 자동으로 감소시키지 않는다.

발동 횟수 차감 예시:

```csharp
if (installed.RemainingTriggers > 0)
{
    int nextCount = installed.RemainingTriggers - 1;
    boardState.TrySetRemainingTriggers(installed.InstallationId, nextCount);

    if (nextCount == 0)
        boardState.RemoveInstallation(installed.InstallationId);
}
```

`RemainingTriggers == -1`인 경우에는 차감하지 않는다.

## 8. 제거

### 정확한 설치 하나 제거

```csharp
bool removed = boardState.RemoveInstallation(installed.InstallationId);
```

연결된 3D 표시 오브젝트도 함께 제거된다.

### 타일 조건으로 여러 개 제거

```csharp
// Outer01의 모든 설치 정보 제거
int removedCount = boardState.RemoveSkills(BoardTileId.Outer01);

// Outer01의 특정 스킬 전부 제거
removedCount = boardState.RemoveSkills(
    BoardTileId.Outer01,
    InstalledSkillId);

// Outer01에서 특정 플레이어가 설치한 특정 스킬 제거
removedCount = boardState.RemoveSkills(
    BoardTileId.Outer01,
    InstalledSkillId,
    PlayerId);
```

### 전체 초기화

```csharp
boardState.ClearAll();
```

`ClearAll`은 보드의 모든 설치 정보와 연결된 모든 3D 표시 오브젝트를 제거한다.
개별 캐릭터 스킬에서는 일반적으로 호출하지 않는다. 맵을 다시 불러올 때
`MapManager`가 자동으로 호출한다.

## 9. 설치·변경·제거 이벤트

UI, 디버그 화면 또는 이펙트 보조 시스템에서 상태 변화를 감지할 수 있다.

```csharp
protected override void Start()
{
    base.Start();

    MapManager mapManager = FindFirstObjectByType<MapManager>();
    if (mapManager == null) return;

    boardState = mapManager.BoardRuntimeState;
    boardState.SkillInstalled += HandleInstalled;
    boardState.SkillChanged += HandleChanged;
    boardState.SkillRemoved += HandleRemoved;
}

protected override void OnDisable()
{
    if (boardState != null)
    {
        boardState.SkillInstalled -= HandleInstalled;
        boardState.SkillChanged -= HandleChanged;
        boardState.SkillRemoved -= HandleRemoved;
    }

    base.OnDisable();
}
```

`OnEnable`은 `Start`보다 먼저 호출될 수 있으므로 매니저를 찾기 전에 이벤트부터
구독하지 않는다. 오브젝트 풀링처럼 다시 활성화되는 구조에서는 중복 구독되지 않도록
별도의 구독 상태값을 함께 관리한다.

## 10. 한 칸 추가 이동 조건 예시

현재 말이 자신이 설치한 바람 길 위에 있을 때만 한 칸 추가 이동하는 예시다.

```csharp
private bool TryMoveOneExtraTile(PlayerRuntimeData.PieceRuntimeData caster)
{
    if (boardState == null || caster == null || Movement == null)
        return false;

    bool hasOwnWindPath = boardState.HasSkillOwnedByPlayer(
        caster.CurrentTileId,
        InstalledSkillId,
        PlayerId);

    if (!hasOwnWindPath)
        return false;

    return Movement.TryMovePiece(
        PlayerId,
        caster.PieceId,
        1,
        true);
}
```

같은 팀원이 설치한 것도 인정하려면 `HasSkillFromTeam`, 정확히 같은 말이 설치한
것만 인정하려면 `HasSkillOwnedByPiece`를 사용한다.

## 11. 구현 시 주의사항

- `skillId`와 추가 데이터의 `key`는 상수로 관리한다.
- 타일 설치 정보는 `MapDefinition` 에셋에 넣지 않는다. 게임 중 변하는 런타임 정보다.
- `BoardTileRuntimeStateManager` 안에 피해나 CC 같은 실제 스킬 효과를 구현하지 않는다.
- 같은 칸의 여러 스킬을 처리하면서 제거할 때는 `GetSkills` 또는 `FindSkills` 결과를 순회한다.
  두 함수가 복사 목록을 반환하므로 순회 중 `RemoveInstallation`을 호출해도 안전하다.
- 팀 판정은 `ownerPlayerId`를 팀처럼 취급하지 말고 저장된 `OwnerTeam`을 사용한다.
- 설치자의 말이 잡히거나 골인했을 때 설치물을 제거할지는 각 스킬 규칙에서 결정한다.
- 시각 프리팹의 위치·회전·크기는 공통 보드 Anchor의 로컬 좌표 기준이다.
- `Start`, `OuterXX`, `CornerXX`, `InnerXX`, `Center`에는 설치할 수 있다.
  `None`과 `Goal`에는 설치할 수 없다.
