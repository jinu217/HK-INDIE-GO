# 캐릭터 스킬 통합

캐릭터 프리팹은 `PlayerController` 자식으로 말 수만큼 생성되고, 각 프리팹의
`CharacterStatusBehaviour`가 플레이어/말 ID 조합으로 자동 등록된다.

## 현재 자동 연결된 실행 지점

- 윷 확률 변경과 추가 던지기 판정
- 이동 전 이동량 변경
- 이동 경로와 보드 진입/이동 완료 알림
- 잡기 전 대상 지정 가능 여부와 방어 패시브 판정
- 잡기 후 리타이어/잡기 성공 알림
- 플레이어 턴 시작/종료 알림
- 선택한 캐릭터 프리팹을 플레이어당 말 수만큼 생성

일반 이동은 `PieceMoveCommand`로 요청하고 `PieceMoveResult`로 결과를 받는다.
UI와 연출은 상태를 다시 추측하지 말고 `PieceMovementManager.MoveResolved` 또는
반환된 결과의 경로, 완주 수, 잡기 목록을 사용한다.

## 액티브 호출

UI가 추가되면 플레이어 시스템에서 SP와 사용 가능 여부를 먼저 검사한 다음
`CharacterSkillRegistry.TryUseActive(request)`를 호출한다. 캐릭터 코드는 SP나
쿨타임을 저장하거나 차감하지 않는다.

`CharacterSkillRegistry.SkillPointRequested`는 캐릭터가 SP 획득을 요청하는
이벤트다. 실제 SP 상태 변경은 플레이어 시스템이 담당한다.

## 잡기 판정

- `Proceed`: 정상 잡기
- `Prevent`: 잡기 무효
- `LimitRetireToAttackingCount`: 공격 말 수만큼만 리타이어
- `ConsumeCloneWithoutBonus`: 분신만 제거하고 추가 던지기 없음
- `ConvertToParts`: 로봇을 해당 칸의 부품 상태로 전환

설치형 아이템 시스템은 아직 존재하지 않으므로 오크의 설치 아이템 무시는
아이템 시스템 도입 시 `PieceMoveCommand`의 이동 컨텍스트에 연결해야 한다.
