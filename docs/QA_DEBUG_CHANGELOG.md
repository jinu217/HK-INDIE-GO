# 인게임 QA 디버그 변경 로그 — 2026-11-03

브랜치: `dev/Debug`. 아래 내용은 최신 구현 기준이다.
코드 차이는 `QA_DEBUG_CODE_CHANGES.patch`, 실행 원문은 `QA_DEBUG_VERIFICATION_LOG.txt`에 있다.

## 1. 이번 재확인에서 찾은 원인

- 긴 다중 행 로그의 첫 부분이 게임 설정이어서 Console 목록에서는 플레이어/말 상세가 보이지 않았다.
  실제 Editor.log에는 P1~P4와 보드 위 말의 위치·CC가 기록되어 있었다.
- 턴/SP/말 위치가 바뀔 때마다 메시지 전체가 달라져 Console의 Collapse로 합쳐지지 않았다.
- 상태 창이 떠 있는 별도 창으로 열려 게임을 가렸다.
- 턴 매니저는 존재하지만 아직 Settings가 없을 때 로비 설정으로 대체하지 않는 초기화 공백이 있었다.

## 2. 실제 사용 흐름

**StartScene → 로비 설정/플레이어 수 확정 → 캐릭터 선택 → InGameScene**을 기준으로 한다.

1. StartScene에서 Play 후 평소처럼 게임에 들어간다.
2. 인게임의 기존 DataDebuger가 실제 PlayerManager/TurnManager 데이터를 수집한다.
3. **Playing Debug** 탭이 Console 옆에 자동으로 표시된다. 게임 화면을 가리지 않는다.
   직접 열기: `Tools > QA > Playing Debug` 또는 Ctrl+Shift+Q.
4. 이 탭의 현재 상태가 같은 자리에 갱신된다. Console에는 기본적으로 특별 행동만 쌓인다.
5. 현재 상태를 Console에 남기려면 **현재 상태 Console 출력**을 누른다.
   **상태 + 행동 복사**, **게임 일시정지/계속**, **행동 기록 비우기**도 제공한다.
6. 수집/Console 설정은 접힌 옵션 안에 있다. **상태 변경도 Console에 누적**은 기본 OFF다.
   이 옵션을 켜면 상태 기록이 다시 누적되며, 변한 메시지는 Collapse로 합쳐지지 않는다.

디버그가 로비 설정, 선택 캐릭터, 말 위치, 턴을 새로 만들거나 덮어쓰지 않는다.
설정은 `turnManager.Settings ?? GameStartSettingsHolder.Current`에서 읽는다.
플레이어는 실제 초기화된 `PlayerManager.ActivePlayers`를 사용한다.
초기화 전에는 대기 문구를 표시한다.

## 3. 상시 표시

표시 순서:

```text
[게임 설정] 모드 / 선택 맵 / 실제 로드된 맵 / 인원 / 옵션
[턴 순서] PlayerN → …
[턴 진행] 라운드 / 전체 턴 / 현재 플레이어·팀 / 현재 단계
  현재 턴의 단계 진행 이력 / 최근 윷 / 남은 결과·던지기

[P1] 선택 캐릭터 | 현재/최대 SP | 모드별 점수·말 수 | 팀 | 액티브 CD
  말1(id=0) | 상태 / 현재·이전 칸 / 남은칸 / 업기 / 패시브 CD / 스킬 가능 / 전체 CC
  (보드 위 말만)

[P2] …
… Pn까지 …

──── 특별 행동 ────
발동/잡기/완주/승리 등 발생 기록
```

- 플레이어마다 독립된 패널로 표시하고, 긴 말/CC 정보는 패널 폭에 맞춰 줄바꿈한다.
- 대기/완주한 말의 개별 상세 행은 숨긴다. 보드 위 말이 없으면 **보드 위 말 없음**을 표시한다.
- 플레이어 헤더는 보드 위 말이 없어도 유지한다.
- Classic: 완주/전체 말 수, 남은 말 수, 보드/대기 말 수.
- Escape: 실제 승리 매니저의 점수/목표 점수. 팀전이면 팀 점수로 표시한다.
- Classic은 기존 게임 규칙상 캐릭터 스킬을 사용하지 않아 SP가 0/0이다.
  로비의 스킬 설정값과 **실제스킬사용** 여부를 함께 표시해 이 차이를 구별한다.
- Random 맵도 선택값과 실제 로드된 맵을 구별한다.
- `말1(id=0)`은 사용자 번호와 코드 ID를 함께 표시한 것이다.
- 전체 CC: ID, 남은 소유자 턴, 값, 횟수, 출처 플레이어/말, 경로.
  CC 턴=0은 소비/명시적 해제까지 지속하는 효과를 포함한다.
- 업힌 말도 실제 보드 말로 표시한다. 유효 CC는 기존 선두 공유 규칙에 따른다.
- 검색은 플레이어 패널 전체를 남겨 말의 소유자를 함께 확인할 수 있다.

## 4. 특별 행동 기록과 삭제한 로그

특별 행동은 발생 순간의 라운드/턴/단계 및 unscaled time을 붙인다.

- 모든 액티브 요청 성공/실패, 실제 시전자/지정 대상, 스킬명/실패 이유.
- 액티브 효과와 패시브: 실제 시전자 → 대상 스택, 요청 효과/값/턴, 처리 결과, 최종 상태/위치.
- 분신 방어, 면역, 부품 구출.
- 일반 이동/스킬 공통 이동의 잡기: 공격 말 → 방어 스택, 잡힘/차단/분신 소모 판정.
- 모든 맵 특수 이벤트: MapManager의 공통 실행 지점에서 맵/칸/대상/설정값/성공 여부 기록.
  예제 LogMapSpecialTileEvent는 중복 출력하지 않는다.
- 완주, Escape 점수, 승리 판정, 타이브레이커, 시간/라운드 제한, 강제 결과 폐기 등 특별 판정.

삭제/정리:

- 정상 말 프리팹 적용, 마커 생성/hover, 목적지 결과 나열, 로비 시작 시 JSON 덤프.
- 캐릭터별 중복 성공 로그, 저수준 CC 덤프, SP/쿨다운 중복 로그.
- 일반 이동/윷 결과/초기 턴 순서의 별도 이벤트, SP 변경 이벤트 구독.
- 참조 없는 `InGame/test.cs`와 meta, 기존 디버거의 임의 이동 시험 메뉴/입력 필드.
- 기존 `LogAllPieceData()`는 수동 전체 상태 출력으로 유지한다.
- 경고/오류는 보존한다. 명시적 개발 검증기의 PASS/FAIL도 유지한다.

## 5. 성능과 범위

- 기본 0.5초 조회, 허용 0.25~10초. 게임 상태 조회는 매 프레임 실행하지 않는다.
- 기본 설정에서 상태 변화는 Console 항목을 추가하지 않는다.
- 단계 변화는 이벤트로 기록해 조회 사이에 지나간 단계도 보존한다(현재 턴 최근 16단계).
- 특별 행동 큐/화면 이력은 각각 최근 64개. 폭주 시 오래된 항목과 생략 수를 표시한다.
- Editor 창은 캐시 문자열만 읽는다. 내용/창 폭이 바뀔 때 패널 높이를 다시 계산한다.
- QA 정보 로그만 NoStacktrace, 경고/오류 설정 유지.
- Editor/Development Build에서만 수집. 일반 빌드에서는 수집 비활성 및 조건부 QA 호출 제거.
- 전용 상태 창은 Editor 전용이다. Development Build에는 특별 행동 Console과 수동 상태 출력 경로가 남는다.
- 일반 빌드 조건 컴파일을 확인했으며, 실제 플레이어 빌드/플랫폼 성능 측정은 이번 검사에 포함하지 않았다.
- Profiler 표식: `QA.StatePoll`.

## 6. 코드 변경 로그

| 파일 | 변경 |
|---|---|
| Scripts/InGame/PlayerDataDebugTester.cs | 모든 플레이어 고정 상태, 보드 위 말만 상세, 모드별 점수/말 수, 단계 이력, 전체 상태/특별 행동 별도 출력, SP 중복 구독 제거 |
| Scripts/InGame/QaDebugLog.cs + meta | 공통 조건부 이벤트 로거, 캐릭터/말/스택 식별, 액티브·패시브·잡기 형식 |
| Scripts/Editor/QaPlayingDebugWindow.cs + meta / Editor.meta | 자동 상태 탭, 플레이어별 패널/줄바꿈, 기본 Console 도킹, 검색/복사/일시정지, Inspector 바로가기 |
| Scripts/InGame/QaDebugVerification.cs + meta | 명시적 플레이 모드 QA 통합 검증, 기존 캐릭터 검증 연계 |
| Scripts/Managers/TestTurnManager.cs | 잔여 던지기 읽기 API 3개, 기존 정보 로그 공통화 |
| Scripts/Managers/TestGameManager.cs, TestWinConditionManager.cs, TestYutRuleManager.cs | 의미 있는 단발 이벤트 공통화 |
| Scripts/Managers/PieceMovementManager.cs | 일반 잡기 양쪽 ID/스택/판정, 완주 이벤트, 평범한 이동 로그 삭제 |
| Scripts/Managers/PlayerManager.cs, Scripts/InGame/PlayerController.cs | 프리팹 정상 적용 중복 로그 삭제 |
| Character/Char_Scripts/CharacterSkillRegistry.cs | 모든 액티브 요청 성공/실패 공통 관찰, 중복 SP/CD 로그 삭제 |
| Character/Char_Scripts/CharacterStatusBehaviour.cs | 설정 효과 적용을 공통 관찰해 패시브·액티브 실제 대상/결과 기록 |
| Character/Char_Scripts/CcEffectService.cs | 저수준 중복 덤프 삭제, 면역/부품 구출 기록 |
| Character/Char_Scripts/CharacterBoardUtility.cs | 캐릭터 공통 잡기 성공/차단 기록 |
| Character/Char_Info/CHAR_001_1, 001_2, 002, 005~010_Status.cs | 중복 정보 로그 삭제 |
| Character/Char_Info/CHAR_003_Status.cs | 실제 분신 방어 발동 기록 |
| Scripts/InGame/MoveMarker.cs, MoveDestinationSelector.cs | 불필요한 표현 계층 정보 로그 삭제 |
| Scripts/InGame/LobbySettingsDebugViewer.cs | 자동 JSON 덤프 제거, 수동 도구로 정리 |
| Scripts/InGame/MapManager.cs, MapSpecialTileEvent.cs, LogMapSpecialTileEvent.cs | 모든 맵 기믹의 공통 발동 기록 및 표시 이름, 예제 이벤트 중복 출력 제거 |
| Scripts/InGame/test.cs + meta | 참조 없는 출력 전용 샘플 삭제 |
| Scenes/InGameScene.unity | 기존 DataDebuger 연결 유지, 새 자동 조회 설정 3개 적용 |
| Character/Char_Scripts/CharacterSkillRuntimeVerifier.cs | 스택 전체 퇴장 후 재업기 준비 데이터 보완, 유가람 실패 상세 수치 추가 |
| Scripts/Editor/QaStartFlowVerification.cs + meta | StartScene의 실제 UI 진입을 관찰하는 읽기 전용 검사. 설정/캐릭터/말/턴을 생성하거나 변경하지 않음 |

## 7. 최신 검증 결과

### 정상 시작 흐름

두 경우 모두 실제 Unity UI에서 StartScene부터 로비/캐릭터 선택을 거쳐 진입했다.

- **Escape / 2인 / Basic:** 로비 설정·플레이어 수·선택 캐릭터·SP·상태 패널·턴 흐름·Console 상태 누적 방지 등 10개 PASS.
  이 실행의 보드 진입 관찰은 PENDING으로 남겼다.
- **Classic / 4인 / Random → Korean / 로비 턴 제한 60초:** 위 항목과 실제 보드 진입까지 **11개 PASS**.
  UI에서 윷을 던지고 2P의 첫 말을 이동시켜, P2 패널에 `말1(id=0)`, `Outer02`, 업기/CC 상태가 표시되는 것을 화면으로 확인했다.
- 떠 있던 상태 창을 Console 옆 탭으로 옮기는 수정 후 Edit Mode에서 실제 도킹을 확인했다.
  도킹 API는 Unity의 공개 `EditorWindow.GetWindow`/`ShowTab`을 사용한다.
- 최종 보완한 탭 선택/실제 스킬 사용 표시는 컴파일로 추가 확인했다.
- 최신 C# 런타임 / Editor / 일반 빌드 조건 컴파일: 오류·경고 없이 PASS.
- 검사 후 Play 종료, StartScene 복귀. 자동 검사 플래그 없음.

읽기 전용 재검사:
`StartScene`을 연 Edit Mode → `Tools > QA > Verify StartScene Flow (read only)` → 평소처럼 UI 진행.
원문은 `Temp/qa-start-flow-verification.log`에 저장된다. 이 검사는 플레이 행동을 대신 입력하지 않는다.

### 이전 검사와 미해결 사항

- 이전 QA 통합 검사 40 PASS / 1 연계 FAIL, 캐릭터 검사 121 PASS / 1 FAIL은 과거 구현의 참고 결과다.
- 남은 캐릭터 실패는 **CHAR_001_2 유가람 업기 SP 패시브**다.
  업힌 시전자의 이동 완료 콜백 제외와 SP 지급 시점 불일치가 남아 있다. 이번 진단 표시 변경으로 해결된 것은 아니다.
- 기존 InGame 전용 fixture 검증기는 명시적 메뉴/플래그로만 실행된다.
  최신 확장 fixture 전체를 이번 정상 시작 검사 결과로 취급하지 않는다.
- 이번 두 정상 플레이에서는 모든 캐릭터의 액티브/패시브/맵 기믹/완주를 각각 재발동시키지 않았다.
  해당 공통 로그 경로는 코드 검토했으며, 이전 검사 원문을 별도로 보존한다.
- 추가 관찰: Random에서 Korean이 선택된 Classic 실행에서 타일 외형이 표시되지 않았다.
  논리 좌표에 따른 말 이동은 성공했다. 맵 외형 구성은 추가 확인 대상이며 이번 QA 변경에서 수정하지 않았다.

참조: [Unity EditorWindow 구현](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/EditorWindow.cs).

## 8. 추가 요청 — 말별 업기/팀/골인 정보 (기존 항목 유지)

기존 말 상태 줄과 `남은칸`, 업기 그룹/선두, 쿨다운, CC 출력은 그대로 두고 그 아래에 상세 한 줄을 추가했다.

추가 항목:
- `팀`: 해당 말 소유 플레이어의 로비 팀 배정. 기존 플레이어 헤더와 같은 값을 말마다 표시한다.
- `업기역할`: 단독 / 리더 / 업힌 말.
- `리더말`: 실제 업은 말의 플레이어·말 번호·코드 ID. 보드에서 찾지 못하면 경고 문구를 붙인다.
- `업힌말`: 리더와 구분한 실제 업힌 말 목록.
- `업힌수`: 리더를 제외한 업힌 말 개수. 같은 그룹의 각 말에서 같은 수를 표시한다.
- `그룹전체`: 리더를 포함한 실제 보드 위 그룹 말 개수.
- `골인까지`: 지름길 분기에서 멈춰 지름길을 이용하는 최단 전진 칸 수. 참먹이를 통과하는 마지막 1칸을 포함한다.
  주사위 횟수나 CC/스킬/맵 기믹의 향후 효과는 예측하지 않는다.

예시(Outer01의 3개 말 그룹):
```text
말1(id=0) | 상태=InBoard, 위치=Outer01, … 남은칸=10, 업기그룹=0, 선두=말1, … CC=[]
  팀=TeamA, 골인까지=11칸(최단), 업기역할=리더, 리더말=P1/말1(id=0), 업힌말=[P1/말2(id=1), P1/말3(id=2)], 업힌수=2, 그룹전체=3
말2(id=1) | (기존 상태 줄)
  팀=TeamA, 골인까지=11칸(최단), 업기역할=업힌 말, 리더말=P1/말1(id=0), 업힌말=[P1/말2(id=1), P1/말3(id=2)], 업힌수=2, 그룹전체=3
```

기존 `남은칸`은 Escape 동점 판정용 고정 거리 점수다. 이 계산과 승리 판정은 변경하지 않았다.
새 `골인까지`는 별도의 표시 전용 조회이며 게임 이동/스킬/업기/골인 처리에 연결하지 않았다.
같은 칸의 단독 말이나 대기·완주한 말은 그룹 개수에 포함하지 않는다. 그룹 ID는 소유 플레이어 안에서 조회한다.

변경 코드:
- `PlayerDataDebugTester.AppendPieceDetails`: 기존 줄 아래에 상세 정보 추가.
- `PieceMovementManager.TryCountMinimumGoalSteps`: 원래 전진 타일 조회를 이용한 읽기 전용 계산 추가.
- `QaDebugVerification.cs / QaPieceFieldsVerification`: 씬/로비/실제 플레이 데이터와 분리된 일반 데이터 검사 추가.

검증:
- 추가 필드 검사 **18 PASS / 0 FAIL**. 리더/업힌 말/단독, 개수, 같은 칸의 비그룹 말, 대기 말 제외,
  누락 리더, 팀, 지름길·참먹이 경계 거리, 비정상 칸, 데이터 불변을 확인했다.
- 이전 코드 패치에서 변경 전 소스를 복원해 대조했다. 새 추가 부분을 제외한 기존 이동 매니저와 상태 출력 코드는 동일했다.
- 런타임/Editor/일반 빌드 조건 컴파일 모두 PASS.
- 이번 검사는 Play 진입 없이 고립된 테스트 데이터로 실행했다. StartScene 정상 진입 결과는 위의 이전 검사 기록이다.
- 재검사 메뉴: `Tools > QA > Verify Piece Status Fields`.
- 원문: `Temp/qa-piece-fields-verification.log`, `Temp/qa-piece-fields-scope.log`.
