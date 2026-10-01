# Character CC 실행 구조

## 책임

`ActiveSkillButtonController` → `CharacterSkillRegistry` → `Char_Info/*_Status` → `CcEffectService` → `PlayerManager`가 관리하는 실제 `PieceRuntimeData.Cc` → 기존 이동/턴/승리 시스템 및 결과 UI.

- UI: 버튼, 포인터 입력, 선택 강조, 성공/실패 메시지. 효과를 실행하거나 잡기 결과를 직접 소비하지 않는다.
- 캐릭터 스크립트: 사용 가능 단계, 사용자 말/대상 조건, 패시브 발동 조건, 효과 순서. 기존 Char_Info 스크립트와 SO 참조는 유지한다.
- Registry: 말 ID 등록, 공통 SP/플레이어별 쿨다운, 매니저 이벤트 연결, 성공 결과 전달. 효과별 상태는 저장하지 않는다.
- CcDefine: 상태/버프/즉시 효과 식별자. 기존 None=0, Stun=1, Silence=2, Retire=3, Kill=4 및 namespace는 유지하며 CcEffectService 파일에 함께 정의한다.
- CcEffectService: CC 자료형과 적용·소비·만료·판정의 단일 진입점. CcBoardEffects 공개 타입은 유지하되 CharacterBoardUtility 파일 안에서 보드 변경/잡기를 구현한다.
- CcCloneView: 분신 렌더링만 담당한다. 실제 분신 수는 Clone CC.Value에 있으며 CharacterBoardUtility 파일에 함께 둔다.
- PlayerCcInspector: PlayerManager Inspector에서 실제 말별 CC 목록을 읽기 전용으로 보여주며 에디터 검증 코드와 같은 파일에 둔다.

`CharacterSkillStatus`는 기존 SO/UI의 분류 메타데이터로만 유지한다. 실제 효과 분기는 CcDefine을 사용한다.

## 저장과 적용

```csharp
// 아이템 등 다른 시스템도 같은 API로 효과를 적용한다.
playerManager.TryApplyPieceCc(targetPlayerId, targetPieceId,
    CcDefine.Stun, remainingOwnerTurns: 2,
    sourcePlayerId: sourcePlayerId, sourcePieceId: sourcePieceId);

// 캐릭터 스크립트: 자신에게 효과를 적용한다.
ApplyEffect(CcDefine.DoubleMove);

// 캐릭터 스크립트: 대상에게 효과를 적용한다.
CcEffectService.Apply(target.Piece, CcDefine.Protection, turns: 1,
    sourcePlayerId: PlayerId, sourcePieceId: PieceId);

// 상태는 복사본이 아니라 실제 PlayerRuntimeData의 목록이다.
playerManager.TryGetPieceCc(playerId, pieceId, out PieceCcCollection cc);
bool stunned = cc.Has(CcDefine.Stun);
foreach (CcState effect in cc.Effects) { /* Type, RemainingOwnerTurns, Value, Source IDs */ }
```

동시에 여러 효과를 저장한다. CurrentCc/RemainingCcTurns/SetCc/ClearCc는 기존 호출자 호환용이다. 복합 상태 판정은 CurrentCc == 비교 대신 Cc.Has 또는 공통 CanMove/CanUseSkill을 사용한다. ClearCc는 전체 해제, CcEffectService.Remove(piece, type)는 해당 효과만 해제한다. 잡기 결과 소비는 Kill/Retire만 제거한다.

효과가 추가/해제되면 CcEffectService.Changed(piece, state, added)가 발생한다. 즉시 효과(Move/MovePath/ExtraThrow/SkillPoint)는 CC에 등록 → 실행 → 제거되므로 UI/VFX는 이 이벤트로 관측한다. 현재 상태 목록을 누적 로그처럼 유지하지 않는다. Kill/Retire는 턴 시스템에서 소비할 때까지 남는다.

## 지속시간

기존 매니저 계약인 **대상 소유자 턴 시작 감소**를 유지한다. 0은 소비/명시적 해제까지 유지한다. 상대에게 한 턴 행동 불가를 주려면 2를 저장한다(다음 턴 시작 2→1, 그다음 시작 1→0). Protection=1은 다음 소유자 턴 시작까지, Hidden/Parts=3은 세 번째 소유자 턴 시작까지다. 같은 Stun/Silence/Protection/Hidden 재적용은 기존의 더 긴 지속시간을 줄이지 않는다.

SP/쿨다운/첫 진입 여부 같은 **발동 조건**은 캐릭터/Registry에 남는다. 은신 여부·분신 수·보호 횟수·이동 배율 등 **이미 부여된 효과 상태**는 CC에만 있다. WindPath의 활성 경로는 CC에, 다음 활성화를 위해 수집 중인 이동 기록은 캐릭터에 둔다.

## 캐릭터별 연결

| 캐릭터 | 패시브 효과 | 액티브 효과 |
|---|---|---|
| 001_1 | 첫 이동과 착지 처리를 마친 뒤 별도 Move(도 1칸), 리타이어 시 해당 말 재사용 | DoOrMo |
| 001_2 | SkillPoint | ExtraThrow |
| 002 | Protection | DoubleMove |
| 003 | LimitCapture | Clone |
| 004 | Protection | Hidden |
| 005 | RemoveBackDo | Retire + MovePath |
| 006 | Protection | Kill(방어 판정 후) |
| 007 | SkillPoint | MovePath + Stun |
| 008 | WindPath → Move | Stun |
| 009 | Parts → 부활 또는 Retire | 범위 Retire |
| 010 | MoveBonus | Move(-1) |
| 018 | Mark → SkillPoint | MovePath + Kill |
| 019 | YutMoExtraThrow | ReverseExtraThrow |

일반 잡기와 검기/암살은 방어 판정을 거친다. 자폭은 종전 규칙대로 범위의 아군/자신을 포함해 직접 Retire한다. 잡기/SP/추가 던지기/완주 결과는 UI 호출 여부와 관계없이 Registry의 성공 경로에서 반영된다.

## 선택 흐름 확장

공통 입력은 UI에 유지하고 캐릭터가 GetNextInputStep과 CanSelectActiveTarget을 재정의한다. 기본 순서는 사용자 말 → 필요 시 대상 → 버튼으로 확정이며, RequiresCasterPieceSelection=false인 플레이어 전체 스킬은 사용자 말 선택을 생략한다. 캐릭터마다 포인터 입력/강조 코드를 복제하지 않는다.

## Character 밖 변경 (필수 연결만)

- Scripts/InGame/PlayerRuntimeData.cs: 실제 말별 CC 목록, 기존 setter의 효과 처리기 위임, 잡힌 말의 좌표 변경용 내부 메서드.
- Scripts/Managers/PlayerManager.cs: TryApplyPieceCc/TryGetPieceCc API 추가. 기존 초기화 구조 유지.
- Scripts/Managers/TestTurnManager.cs: 단일 CC 감소/소비 구현을 공통 효과 처리기에 위임하고 이동 제한 조회를 교체. 기존 턴 순서와 보너스 계산 유지.
- Scripts/Managers/PieceMovementManager.cs: 직접 이동의 CC 제한, 표식 보상 연결, 부품을 업기에서 제외. 기존 경로/일반 잡기 규칙 유지.
- Scripts/InGame/MoveDestinationSelector.cs: 목적지 표시도 공통 CC 이동 제한 사용(1줄).
- Scripts/InGame/CcDefine.cs 및 meta: 정의를 Character/Char_Scripts/CcEffectService.cs에 통합하고 옛 위치에 중복 정의를 남기지 않음.

씬/프리팹/SO/프로젝트 설정은 수정하지 않았다. 기존 CharacterBoardUtility의 효과 구현은 CcBoardEffects로 옮기고 기존 API는 위임 함수로 유지했다. 공통 기반/Registry/조회 도구는 아직 사용되므로 삭제하지 않았다.

Char_Scripts는 폴더를 추가하지 않고 7개 C# 파일로 유지한다. CcDefine/CcState는 CcEffectService에, CcBoardEffects/CcCloneView는 CharacterBoardUtility에, 요청·결과·선택 단계 자료형은 CharacterSkillRegistry에, CC 검증기와 읽기 전용 Inspector는 CharacterSkillRuntimeVerifier에 통합했다. 외부에서 사용하는 타입과 함수 이름은 그대로 유지한다.

기본 잡기는 현재 계약을 유지한다. PieceMovementManager가 뒷도·도·개·걸을 Kill, 윷·모를 Retire로 결정하여 PieceRuntimeData.SetCaptured에 전달하고, TestTurnManager가 Kill/Retire를 소비해 잡기 추가 던지기를 판정한다. 캐릭터 보호·분신·부품 판정은 SetCaptured 호출 전에 실행한다.

## 검증

PlayerManager가 없는 씬(예: StartScene)의 Edit Mode에서 Tools → Character → Verify CC Pipeline. 임시 additive 씬과 메모리 SO 복사본으로 검증 후 정리하며 결과는 Temp/cc-verification.log에 기록한다. 실제 SO 값은 변경하지 않는다. 이 검증은 CC 로직/13개 캐릭터/API 연결의 회귀 검증이며, 실제 게임 화면의 클릭/애니메이션 전체를 자동 검증하는 테스트는 아니다.

## 기존 Character 안내문과의 관계

`Character/PLAYER_SYSTEM_OVERVIEW.md`와 `Character/BOARD_TILE_RUNTIME_SKILL_GUIDE.md`를 함께 확인했다. 기존 안내문은 수정하지 않았다.

- 시스템 개요의 `Scripts/Player` 경로와 일부 효과 설명은 이전 구현 기준이다. 현재 파일 위치는 `Character`이며, 이번 변경의 책임 분리와 CC 저장 방식은 이 문서를 따른다.
- 기존 Registry 호출 계약, 말 등록, 공유 SP/액티브 쿨다운, 턴/이동/승리 매니저의 소유권은 유지한다. 외부 매니저에는 개별 캐릭터 타입 분기를 추가하지 않는다.
- 보드 설치 API는 설치 정보와 선택적 표시 프리팹의 저장소다. 실제 CC 실행, 발동 조건, 카운터 차감 책임을 해당 매니저로 옮기지 않는다.
- 현재 WindPath는 말의 CC에 경로를 저장하는 기존 동작을 이관했다. `BoardTileRuntimeStateManager`에 타일 설치물을 등록하거나 설치형 VFX를 생성하는 기능까지 추가한 것은 아니다. 향후 설치형 스킬은 안내문의 API로 설치 정보를 관리하고, 발동 시 `CcEffectService`로 말에 효과를 적용한다.
- 기존 안내문에 명시된 것처럼 로직 검증 통과와 실제 게임 화면 전체 검증은 구분한다.

## 참고 테이블과의 관계

사용자가 제공한 '윷 아레나 캐릭터 스킬 테이블.xlsx'는 의도와 용어를 확인하는 참고자료다. 표와 동일하게 만드는 작업이 아니며, 기존 SO의 비용/쿨다운은 일괄 변경하지 않았다. 폴더 생성/삭제/이동 없이 기존 폴더 안에서 작업한다.

별도 기획 판단이 필요한 대표 차이(이번 이관에서 표 기준으로 변경하지 않음):

- CHAR_006: 기존 25% 잡기 회피/선택한 적 1개 대상. 표에는 디버프 면역/두 칸의 모든 적 대상이 있다(Character_Data D10/F10).
- CHAR_007: 기존 경로의 첫 적만 스턴. 표는 경로의 모든 적이다(F11).
- CHAR_008: 기존 효과는 Stun이다. 표는 스킬 사용이 가능한 이동 제한 Binding을 구별한다(F12 및 효과, 상태이상_Data C18/D18).
- CHAR_009: 기존은 세 번째 소유자 턴 시작 만료/아군이 1칸 이내에 도착하면 부활. 표에는 2턴/같은 칸 부활 후 업기가 있다(D13).
- CHAR_010: 기존 후퇴는 1칸. 표에는 업힌 말 수 추가 후퇴/즉시 턴 종료가 있다(D14/F14).
- 표의 보호/방어/유혹은 방어 범위를 세분화한다. 이번 Protection CC는 기존 1회 잡기 방어 의미를 유지한다.
- 표에만 있는 미구현 캐릭터/아이템은 이 구조 변경을 이유로 새로 생성하지 않는다.
