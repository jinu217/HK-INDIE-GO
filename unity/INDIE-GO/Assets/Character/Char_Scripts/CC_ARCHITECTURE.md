# Character 스킬과 효과 ID 구조

## 책임과 변경 범위

UI → CharacterSkillRegistry → Char_Info/*_Status → SO의 효과 설정 → CcEffectService → PlayerManager의 실제 PieceRuntimeData.Cc.

- Status: 발동 시점, 대상 선택, 효과 순서, 첫 진입/예약/잡기 방어 계획.
- CharacterData/SO: 효과 ID, 지속시간, 수치, 보호 횟수, 액티브 거리, 면역 목록. 초상화·외형·SP 비용·쿨다운 데이터는 유지.
- CcEffectService: 효과 부여·방어·소비·만료. 개별 CHAR 클래스는 참조하지 않는다.
- CharacterBoardUtility/CcBoardEffects: 기존 경로 규칙 조회, 경로 이동/업기/분신 표현. 분신 수 변경도 CcEffectService로 위임한다.
- Registry: 기존 말 등록, 공통 SP/쿨다운, 턴/이동 이벤트 연결과 성공 결과 전달.
- UI: 기존 선택·강조·확정과 표시. 효과 구현은 포함하지 않는다.

이번 이관은 Assets/Character 안에서만 수행한다. 씬/외부 매니저 수정, 폴더/스크립트 생성·삭제는 없다. Char_Scripts는 기존 7개 C# 파일을 유지한다.

기본 잡기는 그대로다. PieceMovementManager가 뒷도·도·개·걸을 Kill, 윷·모를 Retire로 판정하고 PieceRuntimeData.SetCaptured에 전달한다. TestTurnManager가 결과를 소비한다. 기본 잡기 판정과 턴 순서를 Character로 옮기지 않는다.

## SO에서 관리할 값

CharacterData의 passive_Effects / active_Effects는 CharacterEffectDefinition 목록이다.

| 필드 | 의미 |
|---|---|
| id | 테이블의 공통 효과 ID |
| ownerTurnTicks | 대상 소유자의 턴 시작마다 감소하는 횟수. 0은 소비/명시적 해제까지 유지 |
| amount | SP 지급량, 분신 수, 추가 이동량, 이동 배율 등 |
| charges | protect/Charm의 방어 횟수. -1은 기간 동안 무제한, 양수는 횟수 제한 |
| ignoresInstalledItems | 경로 이동 기록의 설치물 무시 옵션 |
| isSimpleMove | 기존 이동 완료 이벤트의 단순 이동 여부. 바람길 재발동 조건 등에 사용 |

active_Range는 일섬/검기/정령의 화살의 이동·검색 거리다. passive_Immunities는 면역 처리할 ID 목록이다. 기존 passive_Status / active_Status와 CharacterSkillStatus는 SO/UI 호환용 분류이며 실제 효과 실행의 설정값은 아니다.

Status에서는 ApplyPassiveEffect(index, target, path, amount) / ApplyActiveEffect(...)를 사용한다. 대상 생략 시 자신의 말, amount 생략 시 SO의 수치를 사용한다. 경로와 업은 말 수처럼 플레이 중 결정되는 값만 인자로 넘긴다. 잘못된 액티브 설정은 실행 전에 거부하며 SP/쿨다운을 소비하지 않는다.

목록은 모든 항목을 무조건 실행하는 파이프라인이 아니다. Status가 필요한 시점에 해당 슬롯을 실행한다. 아래 슬롯 순서를 유지해야 하며 새로운 동작/순서가 필요하면 해당 Status를 수정한다. ID와 수치 조정 때문에 외부 매니저를 수정할 필요는 없다.

## 캐릭터별 슬롯

목록 안의 순서는 슬롯 0, 1 순서다. '→'는 설명상의 실행 순서로, 실제 적용 시점은 Status가 결정한다.

| 캐릭터 | passive_Effects | active_Effects |
|---|---|---|
| 001_1 | Move_plus(1): 첫 착지 뒤 화살표/턴 종료 시 추가 이동 | MOorDO |
| 001_2 | Get_point(1): 잡기/새 업기 | Get_throw(1) |
| 002 | protect(1 tick, 무제한): 가까운 아군 한 말 | Move_scale(2): 다음에 던진 특정 결과를 사용할 때 적용 |
| 003 | 비어 있음: 스택 전체의 잡기 소모 순서를 계획하는 방어 규칙 | Stack_fake(1) |
| 004 | Charm(1회): 잡기만 방어 | Stealth(3 ticks) |
| 005 | No_back | Peace_move, Retire: 경로의 적 퇴장 후 이동 |
| 006 | 비어 있음: passive_Immunities에 Sturn/Binding/Silence | Retire, Get_throw: 실제 퇴장한 적 수 × SO 지급량 |
| 007 | Get_point(1) | Move_end(설치물 무시), Sturn(2 ticks) |
| 008 | WindPath(수집 경로), Peace_move(1): 바람길 발동 시 이동 | Binding(2 ticks) |
| 009 | Robot_part(3 ticks): Retire 대체 | Retire: 범위의 자신/아군/적 |
| 010 | Move_plus(1): 업은 실제 말 수 × SO 수치, 원래 착지 후 추가 이동 | Peace_move(1): 실제 스택 수 × SO 수치만큼 후진 후 턴 종료 |

003의 방어 계획, 002의 특정 윷 결과 예약, 첫 진입 여부, 패시브 준비 여부는 **발동 조건/실행 계획**이지 별도의 CC가 아니다. 이미 부여된 보호·은신·속박·스턴·분신·부품·바람길은 실제 말의 Cc에 저장된다. 003의 분신 소모와 업기 용량 보정은 ConsumeClones를 통해 공통 처리한다.

전우치는 필드 위 아군만 선택하며, 자신/업힌 말/부품/완주·대기 말/타일 없는 말은 제외한다. 이 조건은 CHAR_002_Status에 있으며 protect 자체에 전우치 전용 대상 제한을 넣지 않는다. 보호는 지정한 말에만 저장하고 다른 말에 복사하지 않는다.

## ID 통합과 호환

기존 24개 enum 값(None 포함)을 20개 실값으로 정리했다. 실제 효과 19개 + None이며, enum 이름은 외부 호환 별칭 2개를 포함해 22개다.

- 테이블 ID: Sturn(표의 철자), Retire, Capture, protect, Stealth, Stack_fake, Robot_part, MOorDO, No_back, Get_throw, Get_point, Peace_move, Binding, Charm, Move_end.
- 매개변수 표기인 ()_Move_plus(n)은 Move_plus + amount로 정규화한다.
- 확장 ID: Silence(기존 침묵/면역), Move_scale(특정 결과 배율), WindPath(바람길 설치 경로). 표에 대응 ID가 없어서 기존 기능을 유지하기 위해 명시적으로 남긴다.
- Kill = Capture = 4, Parts = Robot_part = 8. 기존 외부 코드/직렬화 숫자 계약을 유지하는 별칭이다.
- TalismanProtection은 protect로 통합한다. 같은 ID의 기간 보호와 횟수 보호는 출처/횟수 유형별로 독립 유지되며 서로 덮어쓰지 않는다.
- 미사용 MoveBonus/ReverseExtraThrow/Mark/LimitCapture/YutMoExtraThrow와 중복된 이전 이름·전용 처리를 제거한다. 제거된 번호는 다른 효과에 재사용하지 않는다.
- 외부 기본 잡기에서 호출하는 RewardMarks는 빈 호환 함수로만 남긴다. 표식 생성/보상 구현은 제거했다.
- 표에만 있고 현재 캐릭터가 쓰지 않는 Shield/Untarget/Tp_move 등은 이번 이관으로 추가하지 않는다.

CcState.SkillId / CcEffectService.GetSkillId(type)는 별칭을 Capture/Robot_part로 정규화하여 로그·런타임 Inspector에서 테이블 ID를 표시한다.

## 공통 부여·해제 API

```csharp
// 전우치 Status는 target을 선택한 다음 SO의 패시브 슬롯을 적용한다.
ApplyPassiveEffect(target: target);

// 아이템/기믹 등의 공통 적용 예. 캐릭터의 대상 선택 규칙과 별개다.
CcEffectService.Apply(target, CcDefine.protect, turns: 1,
    sourcePlayerId: playerId, sourcePieceId: pieceId); // charges=-1
CcEffectService.Apply(target, CcDefine.protect, turns: 3, charges: 1);

CcEffectService.Remove(target, CcDefine.protect); // 해당 ID의 모든 출처 해제
CcEffectService.Clear(target); // 모든 상태 해제
bool bound = target.Cc.Has(CcDefine.Binding);
foreach (CcState state in target.Cc.Effects)
{
    // SkillId, RemainingOwnerTurns, Value, Charges, SourcePlayerId/SourcePieceId, Path
}
```

효과 추가/해제는 Changed 이벤트로 알린다. 즉시 효과(Get_point/Get_throw/Move_plus/Peace_move/Move_end)는 등록 → 실행 → 제거 순서이며, Cc 목록은 사용 이력 저장소가 아니다. Capture/Retire는 기존 턴 시스템이 소비할 때까지 남는다.

## 지속시간과 방어

기존 **대상 소유자 턴 시작 차감** 계약은 유지한다. protect=1은 대상의 다음 턴 시작까지, Stealth/Robot_part=3은 세 번째 턴 시작까지다. 한 턴 행동 제한은 2 ticks(다음 턴 시작 2→1, 그다음 1→0)로 설정한다. Inspector에 표시되는 tick을 임의로 일반 '행동 가능한 턴 수'와 혼동하지 않는다.

protect는 잡기·직접 Retire·해로운 상태를 막는다. Charm은 잡기 판정만 막으며 직접 Retire/상태이상을 막지 않는다. Stealth는 지정 대상/일반 잡기를 막지만 범위 Retire에는 별도 면역을 주지 않는다. 여러 출처의 protect 중 기간 보호를 먼저 사용하여 횟수 보호를 낭비하지 않는다.

## 설치형 효과와 검증

BOARD_TILE_RUNTIME_SKILL_GUIDE의 보드 설치 정보 API는 계속 사용할 수 있다. 현재 WindPath는 실제 말의 Cc에 설치 경로를 저장하는 기존 동작이며 타일 설치물/VFX 생성까지 추가한 것은 아니다.

CharacterSkillRuntimeVerifier의 기존 검증과 전우치 대상 제외/보호 중첩 검증을 새 ID/설정에 맞게 갱신한다. 이번 작업에서는 PC 제어, Unity 실행, 실제 플레이/에디터 효과 실행 검증을 하지 않는다. C# 빌드 및 SO/참조/변경 범위 정적 검증과 실제 플레이 검증은 구분한다.
