# Character skill integration

캐릭터 프리팹과 스킬 구현은 `Scripts/Player` 안에서 완결되지만, 기존 인게임 매니저에는
이동 전·잡기 전 이벤트가 없습니다. 외부 시스템이 수정되는 시점에는 아래 단일 진입점만
호출해야 합니다. 캐릭터별 클래스를 직접 참조하지 않습니다.

## Required calls

- 이동 수 확정 전: `CharacterSkillRegistry.ModifyMoveCount(request)`
- 잡기 적용 전: `CharacterSkillRegistry.EvaluateIncomingCapture(request)`
- 추가 던지기 판정: `CharacterSkillRegistry.ShouldGrantExtraThrow(playerId, result, defaultValue)`
- 말이 보드에 진입: `CharacterSkillRegistry.NotifyPieceEnteredBoard(playerId, pieceId)`
- 말이 리타이어: `CharacterSkillRegistry.NotifyPieceRetired(playerId, pieceId)`
- 이동 완료: `CharacterSkillRegistry.NotifyMoveCompleted(record)`
- 잡기 완료: `CharacterSkillRegistry.NotifyCaptureCompleted(request)`
- 대상 선택 가능 여부: `CharacterSkillRegistry.IsTargetable(playerId, pieceId)`
- 액티브 실행: `CharacterSkillRegistry.TryUseActive(request)`

윷 확률과 턴 시작·종료 이벤트는 캐릭터 프리팹 등록 시 기존 공개 API에 자동 연결됩니다.

## Capture decisions

- `Proceed`: 기존 잡기 처리
- `Prevent`: 잡기 무효
- `LimitRetireToAttackingCount`: 공격측 말 수만큼만 대상 스택을 리타이어
- `ConsumeCloneWithoutBonus`: 실제 말 대신 분신 하나만 제거하고 추가 던지기 금지
- `ConvertToParts`: 잡지 않고 해당 칸에 부품 상태로 유지

SP, 현재 쿨타임, 액티브 사용 가능 여부는 캐릭터 데이터가 아니라 플레이어 시스템이
관리합니다. `CharacterSkillRegistry.SkillPointRequested`를 구독해 SP 획득 요청을 처리하고,
검증을 통과한 요청만 `TryUseActive`에 전달해야 합니다.
