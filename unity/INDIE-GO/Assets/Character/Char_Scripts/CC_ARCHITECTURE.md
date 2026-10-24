# Character 스킬과 효과 ID 구조

## 책임과 변경 범위

UI → CharacterSkillRegistry → Char_Info/*_Status → SO의 효과 설정 → CcEffectService → PlayerManager의 실제 PieceRuntimeData.Cc.

- Status: 발동 시점, 대상 선택, 효과 순서, 첫 진입/예약/리더의 잡기 방어 조건.
- CharacterData/SO: 효과 ID, 지속시간, 수치, 보호 횟수, 액티브 거리, 면역 목록. 초상화·외형·SP 비용·쿨다운 데이터는 유지.
- CcEffectService: 효과 부여·방어·소비·만료. 개별 CHAR 클래스는 참조하지 않는다.
- CharacterBoardUtility/CcBoardEffects: 기존 경로 규칙 조회, 경로 이동/업기/분신 표현. 분신 수 변경도 CcEffectService로 위임한다.
- Registry: 기존 말 등록, 공통 SP/쿨다운, 턴/이동 이벤트 연결과 성공 결과 전달.
- UI: 기존 선택·강조·확정과 표시. 효과 구현은 포함하지 않는다.
- CharacterPieceInteractionController: 현재 턴/남은 결과/스킬 준비 상태를 읽고 말·시전자·대상 선택을 조정한다. 실제 이동과 턴 종료는 기존 턴 API에 요청한다.
- 외부 MoveDestinationSelector/MoveMarker: 전달받은 말·목적지 힌트를 표시하고 화면 좌표/호버만 처리한다. 이동 실행, 스킬 효과, 턴 종료 또는 패시브 강제 실행은 담당하지 않는다.

캐릭터 규칙은 Assets/Character에서 관리한다. 선택 흐름 분리를 위해 기존 Char_Scripts 폴더에 CharacterPieceInteractionController를 추가했고, 외부 네비게이팅의 MoveDestinationSelector를 표시 계층으로 변경했다. 추가 승인을 받아 TestTurnManager에는 기본/윷·모 던지기를 잡기·스킬 횟수와 별도로 소비하는 표시만 추가했다. 폴더를 추가/삭제하거나 씬/프리팹을 수정하지 않는다.

기본 잡기는 그대로다. PieceMovementManager가 뒷도·도·개·걸을 Kill, 윷·모를 Retire로 판정하고 PieceRuntimeData.SetCaptured에 전달한다. TestTurnManager가 결과를 소비한다. 기본 잡기 판정과 턴 순서를 Character로 옮기지 않는다.

## 모드별 캐릭터 스킬 활성화

- `CharacterSkillRegistry.AreSkillsEnabled`가 현재 턴 매니저의 Settings를 읽고, 초기화 전에는 GameStartSettingsHolder.Current를 참조한다. 설정이 없으면 비활성화한다.
- Classic은 `useSkill`이 true여도 모든 캐릭터 패시브·액티브를 비활성화한다. 그 밖의 모드는 `useSkill` 설정을 따른다.
- 이동/확률/잡기 보정, 입장·이동·잡기·턴 이벤트, 면역·부품 전환·바람길, SP 지급과 액티브 실행을 같은 조건으로 차단한다. 액티브 버튼과 스킬 대상 힌트도 표시하지 않는다.
- 캐릭터 말의 등록과 외형, 일반 이동·업기·잡기·완주, 공용 CC API 및 이동 완료 알림은 유지한다. 아이템/타일/승리/턴 규칙이나 외부 매니저의 설정은 변경하지 않는다.

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
| 001_2 | Get_point(1): 잡기/새 업기 | Get_throw(1): 자신의 WaitThrow/WaitAction에서 사용 가능 |
| 002 | protect(1 tick, 무제한): 가까운 아군 한 말 | Move_scale(2): 다음에 던진 특정 결과를 사용할 때 적용 |
| 003 | 비어 있음: 분신 포함 스택 수가 공격 말 수보다 많으면 실제 말 전체 생존 | Stack_fake(1) |
| 004 | Charm(1회): 잡기만 방어 | Stealth(3 ticks) |
| 005 | No_back | Peace_move, Retire: 경로의 적 퇴장 후 이동 |
| 006 | 비어 있음: passive_Immunities에 Sturn/Binding/Silence | Retire, Get_throw: 실제 퇴장한 적 수 × SO 지급량 |
| 007 | Get_point(1) | Move_end(설치물 무시), Sturn(2 ticks) |
| 008 | WindPath(수집 경로), Peace_move(1): 바람길 발동 시 이동 | Binding(2 ticks) |
| 009 | Robot_part(3 ticks): Retire 대체 | Retire: 범위의 자신/아군/적 |
| 010 | Move_plus(1): 업은 실제 말 수 × SO 수치, 원래 착지 후 추가 이동 | Peace_move(1): 실제 스택 수 × SO 수치만큼 후진 후 턴 종료 |

003의 수적 방어 조건, 002의 특정 윷 결과 예약, 첫 진입 여부, 패시브 준비 여부는 **발동 조건/실행 계획**이지 별도의 CC가 아니다. 이미 부여된 보호·은신·속박·스턴·분신·부품·바람길은 리더 말의 Cc에 저장된다. 003의 분신 소모와 업기 용량 보정은 ConsumeClones를 통해 공통 처리한다. 분신은 공격 말 수만큼 먼저 소모할 수 있지만 실제 업힌 말 일부만 퇴장시키지 않는다.

## 사용 시점과 재투입

- 현재 11종 모두 자신의 던지기 대기(WaitThrow)와 행동 대기(WaitAction)에서 액티브를 사용할 수 있다. 첫 윷 결과가 없어도 턴 시작 처리가 끝나는 즉시 버튼·시전자 선택·실행에 같은 조건을 적용한다. 계산/이동 처리 중인 중간 단계나 다른 플레이어 턴에는 입력하지 않는다.
- Get_throw는 대기 단계에 관계없이 기존 GrantSkillExtraThrow에 즉시 전달한다. 기본/윷·모 던지기는 TestTurnManager의 pendingRegularThrow에서 먼저 소비하므로 스킬 기회를 대신 소모하지 않는다. 별도의 Character 예약/턴 복제는 없다.
- 보드 위 말을 대상으로 시전하는 캐릭터는 시전 가능한 자기 리더가 있어야 한다. 윤누리·유가람·전우치의 플레이어 공통 스킬은 기존처럼 대기 말에서도 사용할 수 있다. 업힌 말/완주 말/기절·침묵·부품, 홍길동의 업기 정원, SP·쿨다운 조건도 검사한다.
- 윤누리·전우치는 행동 대기에서 사용해도 이미 던진 결과를 바꾸지 않고 다음 던지기에 예약한다. 군인의 작전상 후퇴는 던지기 전이나 이동 사이 어느 입력 단계에서 사용해도 남은 결과·던지기를 버리고 즉시 턴을 종료한다. 외부 EndTurnAfterSkill이 WaitAction만 허용하므로 Character 결과 처리에서 던지기 대기를 해당 공개 API에 맞춰 전달한다.
- 윤누리/전우치/구미호의 재사용 패시브는 리타이어·잡기뿐 아니라 완주·대기 복귀 후에도 새 투입에서 다시 적용한다. 재투입 초기화는 첫 이동 예약을 만들기 **전**에 수행한다.
- CanUseActive 조회는 SP/쿨다운/턴/CC/시전자 조건을 실행 경로와 공유한다. 미리보기는 CC나 다음 던지기 예약을 소비하지 않는다.

## 선택과 네비게이팅

- 자기 말을 먼저 클릭하면 남은 결과의 이동 목적지와 사용 가능한 액티브 시전자 표시를 함께 보여 준다. 선택한 말은 스킬 버튼에도 전달한다.
- 말 선택 없이 스킬 버튼을 누르면 Status.GetNextInputStep에 따라 시전자 선택, 대상 선택, 확정으로 진행한다. 시전자 없는 플레이어 공통 스킬은 즉시 실행한다.
- 엘프는 스킬 버튼 → 자기 시전자 → 상대 말 선택, 또는 자기 시전자 선택 → 스킬 버튼 → 상대 말 선택 순서로 사용한다. 상대 말 선택 즉시 Registry로 실행하며 버튼을 다시 누르지 않는다. 유효한 대상에 대한 화살은 보호/면역으로 속박이 막혀도 정상 시전이며 SP·쿨다운을 반영하고 선택·강조를 정리한다. 횟수형 보호는 기존 방어 횟수를 소모하고 기간형 보호는 만료까지 유지한다.
- 구미호의 은신은 기존 Stealth 상태가 공통 IsTargetable 판정에서 지정 대상 선택을 차단한다. 엘프에 캐릭터별 예외를 두지 않으며, 대상 후보 표시와 실행 직전 검증이 같은 공통 판정을 사용한다. 아군·거리 밖·은신/부품처럼 유효하지 않은 대상은 시전하지 않고 비용도 쓰지 않는다.
- 남은 결과가 없어도 사용할 수 있는 스킬의 말은 선택할 수 있다. 이동 목적지는 실제 남은 결과와 기존 이동 미리보기 API에서만 얻는다.
- 표시 스크립트는 게임 진행을 바꾸지 않는다. 목적지 클릭은 Character의 선택 조정기를 통해 기존 RequestMovePiece로 전달한다.

전우치는 필드 위 아군 리더만 선택하며, 자신/업힌 말/부품/완주·대기 말/타일 없는 말은 탐색에서 제외한다. 이 조건은 CHAR_002_Status에 있으며 protect 자체에 전우치 전용 대상 제한을 넣지 않는다. 선택한 리더에 보호를 저장하고 업힌 모든 실제 말은 같은 보호 상태를 조회한다. 공통 효과 API에 업힌 말을 직접 전달해도 리더에게 적용한다.

## 업기 상태의 단위

- 업힌 실제 말은 항상 리더의 상태를 따른다. `PieceCcCollection`은 현재 업기 정보를 보고 리더의 동일한 `CcState`를 반환한다. 별도의 보호 횟수나 지속시간을 복제하지 않는다.
- 효과 부여·해제·면역·잡기 방어는 리더 기준으로 실행한다. 업기 전 구성원의 독립 효과와 분신은 이동 완료 시 제거하며, 범위 스킬의 대상 목록과 선택 힌트에도 리더만 표시한다. 퇴장 수를 보상으로 사용하는 스킬은 실제로 함께 퇴장한 말 수를 센다.
- 지속시간과 리더의 턴/이동 패시브는 스택당 한 번 처리한다. 외부 이동 매니저가 구성원마다 잡기를 요청하므로, 같은 이동 안의 공격자/방어 스택 판정을 공유하여 일회성 보호·매혹·분신을 중복 소비하지 않는다. 다음 이동에서는 새로 판정한다.
- 잡기·직접 퇴장은 모든 실제 구성원을 함께 대기로 보낸다. 잡기 보너스가 있는 경우 리더에만 Capture, 나머지에는 Retire를 남긴다. 구성원 순서상 업힌 말부터 잡기 요청이 와도 보너스는 한 번이다.
- 로봇 리더가 부품으로 전환되면 전체 스택이 부품 상태를 따른다. 아군이 구출하면 전체가 구출자의 스택에 합류하며, 만료 시에도 전체가 함께 퇴장한다.

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

BOARD_TILE_RUNTIME_SKILL_GUIDE의 보드 설치 정보 API는 계속 사용할 수 있다. WindPath는 실제 말의 Cc에 설치 경로를 저장한다. 엘프에 자동으로 붙는 ElfWindPathVisuals가 이 상태를 읽고 해당 타일에 청록색 회전 바람 이펙트를 표시한다. 텍스트·디버그 라벨·콜라이더는 생성하지 않는다. 경로 교체·효과 제거·업기 리더 변경·비활성화 시 표시도 갱신하며, Classic 등 스킬 비활성 모드에서는 표시하지 않는다. 색·반경·회전 속도·표시 여부는 런타임 컴포넌트 Inspector에서 조정할 수 있다.

CharacterSkillRuntimeVerifier는 11종의 스킬/CC/등록/쿨다운, 사용 단계, 재투입, 실제 턴 API를 통한 추가 던지기, 선택/네비게이팅 API를 검사한다. RunBatch는 원본 씬과 별개인 임시 프로젝트에서 headless Edit Mode로 실행할 수 있다. 이 검증은 실제 게임 화면의 클릭 좌표·애니메이션·시각 품질 검증과 구분한다.
