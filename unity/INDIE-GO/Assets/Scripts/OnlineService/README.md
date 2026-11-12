# OnlineService

Photon Fusion **2.1.4 / Host Mode**를 사용하는 방·대기실 서비스 계층이다. 방 생성, 방 목록 구독, 지정 방 참가, 참가자·준비 상태·팀·방 설정 동기화까지 제공한다. Photon Cloud는 방 검색과 연결 중계를 담당하고 방장 프로세스가 대기실 상태를 검증한다. 별도 전용 서버를 배포하는 구성은 아니다.

씬·패널·프리팹을 추가하지 않는다. 자동 실행 진입점, GameObject/컴포넌트 생성, 씬 로드도 없다. 생성자를 호출하는 것만으로 접속하지 않는다. 기존 게임/캐릭터/잡기 로직은 변경하지 않는다.

## 작업 위치

- 온라인 관련 변경은 `dev/PhotonFusion`에서 진행한다.
- 새 스크립트·에셋·문서는 모두 `Assets/Scripts/OnlineService/` 아래에 둔다.
- `Runtime/`: 화면과 독립적인 서비스, 상태·규칙, Fusion 어댑터.
- `ThirdParty/Photon/`: 설치된 SDK. 이 구현에서는 수정하지 않았다.
- `Validation~/`: Unity가 가져오지 않는 독립 검증 도구. 네트워크·씬 객체 없이 실행한다.

## 외부에서 연결할 지점

`OnlineLobbyService`와 `PhotonFusionTransport`는 일반 C# 클래스다. 추후 화면/애플리케이션 계층이 수명을 관리하며, **Unity 메인 스레드**에서 호출한다.

```csharp
using System;
using Fusion;
using YutArena.Online;

// acquireUnusedRunner는 외부에서 준비한 미사용 Runner를 반환하는 함수다.
// 여기에는 GameObject나 컴포넌트를 생성하는 구현을 포함하지 않는다.
OnlineLobbyService BuildService(Func<NetworkRunner> acquireUnusedRunner)
{
    var options = new OnlineOptions
    {
        AppVersion = "indie-go-lobby-v1",
        Region = "kr",
        LobbyName = "indie-go",
        ConnectionTimeoutSeconds = 30,
        ForceRelay = false
    };
    var transport = new PhotonFusionTransport(acquireUnusedRunner, options);
    return new OnlineLobbyService(transport, options.ConnectionTimeoutSeconds);
}
```

Fusion Runner는 종료/실패 후 재사용할 수 없다. 목록 접속에서 방 생성/참가로 전환할 때도 기존 연결을 종료하므로 공급자는 **접속마다 다른 미사용 Runner**를 반환해야 한다. 서비스는 `Shutdown(destroyGameObject: false)`로 네트워크만 종료한다. `PhotonFusionTransport.RunnerReleased` 이벤트 이후 객체 정리는 외부 소유자가 담당한다. 공급 함수 자체의 객체 생성 정책도 외부 계층의 책임이다. 현재 작업에서는 공급 함수나 씬 객체를 구현하지 않았다.

App ID는 설치된 `PhotonAppSettings`에서 읽고, 그 설정의 복사본에 리전·AppVersion을 적용한다. 원본 설정 에셋은 수정하지 않는다. 모든 플레이어의 App ID, AppVersion, 리전, LobbyName이 같아야 서로의 방을 찾을 수 있다. `ForceRelay = true`이면 직접 연결을 시도하지 않고 Photon Relay를 사용한다.

## API

| 호출 | 동작 |
| --- | --- |
| `BrowseRoomsAsync(token)` | Photon `ClientServer` 목록에 접속하고 변경 목록을 구독 |
| `CreateRoomAsync(request, token)` | UUID 식별자로 Host 방 생성, 방장 포함 2~4명 |
| `JoinRoomAsync(room.SessionId, nickname, token)` | 특정 방에 Client로 참가하고 방장의 등록 확인까지 대기 |
| `LeaveAsync()` | 접속 종료 및 목록·대기실 상태 초기화 |
| `SetReady(bool)` | 자신의 준비 상태 요청; 방장은 항상 준비 상태 |
| `SetTeam(1 또는 2)` | 팀전에서 자신의 팀 변경 요청; 팀당 최대 2명 |
| `SetNickname(string)` | 자신의 닉네임 변경 요청 |
| `UpdateRoomSettings(settings)` | 방장이 게임 설정·전체 팀 배정 변경 |
| `RequestLobbySync()` | 최신 대기실 상태 재요청 |
| `DisposeAsync()` | 접속 종료 및 콜백 구독 해제 |

```csharp
// 각 예시는 별도의 화면 동작에서 호출한다.
await service.BrowseRoomsAsync(token);

await service.CreateRoomAsync(new CreateOnlineRoom
{
    RoomName = "같이 할 사람",
    Nickname = "방장",
    Settings = LobbyRules.DefaultSettings(playerCount: 4, teamMode: true)
}, token);

// 방 생성 대신, 목록에서 선택한 방에 참가하는 경우:
await service.JoinRoomAsync(selectedRoom.SessionId, "참가자", token);
```

방 목록은 `RoomsChanged`로 전달되는 **전체 교체 목록**이다. 방 이름은 중복할 수 있고 실제 참가는 고유 `SessionId`로 한다. `CanJoin`은 목록 시점의 참고 값이다. 클릭 직후 방이 가득 차거나 사라질 수 있으므로 `JoinRoomAsync`의 실패도 처리해야 한다. 참가 실패 시 새 방을 자동 생성하지 않는다. 방을 생성하거나 참가한 상태에서는 먼저 `LeaveAsync`를 호출해야 다른 방이나 목록으로 이동할 수 있다.

## 이벤트와 데이터

- `StateChanged`: 접속 진행 상태. `InLobby`는 유효한 대기실 스냅샷을 확보한 뒤 발생한다.
- `RoomsChanged`: 방 목록 변경. 퇴장 시 빈 목록을 전달한다.
- `LobbyChanged`: 참가자/게임 설정 변경. 퇴장·접속 종료 시 `null`을 전달한다.
- `Error`: 접속 실패 또는 비동기 요청 거절. 구독자의 예외는 내부 상태 변경을 중단시키지 않는다.

`Rooms`는 읽기 전용이고 `Lobby` 및 이벤트의 대기실 데이터는 복사본이다. `OnlineLobbyPlayer.connectionId`는 Fusion 연결 ID이며, 게임의 `PlayerSlot`과 다르다. `slot`은 1부터 시작하는 게임 참가자 자리다. 다른 참가자가 퇴장해도 기존 자리 번호를 유지하고 다음 참가자가 빈 자리를 채운다.

클라이언트의 `SetReady`, `SetTeam`, `SetNickname` 반환은 전송 완료를 뜻한다. 실제 적용은 `LobbyChanged`, 거절은 `Error`로 확인한다. 네트워크 요청에 임의의 대상 플레이어 ID를 넣지 않으며, 방장은 Fusion이 전달한 발신자 ID로 대상자를 결정한다. 방 설정은 방장만 바꿀 수 있다.

방 정원은 생성 시 고정된다. 인원·팀·게임 설정 변경 시 모든 참가자의 준비를 해제하며, 방장은 준비 상태를 유지한다. 팀전은 기존 `TeamMode1`(2인 1조)과 `TeamMode2`(2대2), `playerTeams`를 보존한다. 전체 팀이 꽉 찬 경우 방장이 `UpdateRoomSettings`에 새 `playerTeams` 배열을 전달해 서로의 자리를 바꾸는 팀 배정을 할 수 있다.

`Lobby.CanStart`는 정원 충족, 전원 등록, 참가자 준비, 팀전 2:2 배정을 검사한다. `Lobby.ToGameStartSettings()`는 이 조건을 충족한 상태에서 기존 게임 설정으로 변환한다. **게임 시작·씬 전환·게임 중 동기화는 수행하지 않는다.**

## 실패·수명 정책

접속 메서드는 `CancellationToken`을 받으며 기본 30초 제한이 있다. 취소/실패 시 부분 연결과 상태를 정리한다. 여러 접속 작업을 동시에 요청하면 `Busy` 오류를 반환한다. 진행 중인 작업을 끝내려면 그 작업의 토큰을 취소하고 작업이 끝날 때까지 기다린 후 `LeaveAsync`/`DisposeAsync`를 호출한다. 토큰은 접속 작업에만 적용되며, 이미 성공한 연결을 이후 자동 종료시키지는 않는다.

방장 퇴장/연결 해제 시 대기실 연결을 종료하고 `Offline`으로 돌아간다. 방장 이전, 자동 재접속, 계정 인증, 친구·초대, 게임 중 재참가/복구는 이번 범위에 없다. 일반 참가자는 방이 열려 있고 빈 자리가 있으면 다시 참가할 수 있으며 새로운 연결 ID로 빈 자리를 배정받는다.

대기실 데이터는 작은 버전별 JSON 메시지를 Fusion reliable stream으로 전달한다. 별도 네트워크 프리팹이나 위빙할 NetworkBehaviour가 필요 없다. 참가 요청에는 프로토콜 표식을 사용하고, 크기 제한·요청 빈도 제한·설정 검증·증가하는 스냅샷 버전으로 잘못된 요청과 오래된 상태를 차단한다. 이 표식은 계정 인증 수단이 아니다. Host Mode의 방장은 신뢰 대상이다.

## 검증

PowerShell에서 다음을 실행한다. .NET 10 SDK와 현재 프로젝트에서 한 번 컴파일된 Photon SDK가 필요하다. Unity 설치 위치가 다르면 `-UnityEditorDirectory`로 지정할 수 있다.

```powershell
& './unity/INDIE-GO/Assets/Scripts/OnlineService/Validation~/Run-Validation.ps1'
```

검증은 설치된 실제 Fusion DLL을 참조해 어댑터를 포함한 코드를 컴파일하고, 메모리 전송 계층으로 서비스 동작을 검증한다. 게임 오브젝트, 테스트 씬, 실제 Photon 방을 만들지 않는다. 실제 다중 기기 접속·Relay·Photon Cloud 동작은 추후 외부 실행 계층을 연결한 후 검증해야 한다.
