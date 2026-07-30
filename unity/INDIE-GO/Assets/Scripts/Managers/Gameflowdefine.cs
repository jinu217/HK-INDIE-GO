using System;
using System.Collections.Generic;
using YutArena.Common;

namespace YutArena.Managers.GameProgress
{
    // ===================================================================
    // 게임 전체(메인화면 ~ 결과화면) 진행 단계를 나타내는 enum
    // enum = 정해진 값들 중 하나만 가질 수 있는 타입. "지금 게임이 몇 단계인지"를
    //        int(0,1,2..)로 관리하면 헷갈리니까 이름을 붙여서 관리하는 것
    // ===================================================================
    public enum GamePhase
    {
        None,           // 아직 아무 단계도 아님 (초기값용)
        MainMenu,       // 메인 화면
        Lobby,          // 대기실 (모드/맵/인원 선택)
        CharacterSelect,// 캐릭터 선택 화면
        InGame,         // 실제 플레이 중 (TurnManager가 이 상태에서 돌아감)
        Result          // 결과 화면 (승자 표시 등)
    }

    // 게임이 "왜" 끝났는지 종류
    public enum GameResultType
    {
        None,
        TeamWin,    // Escape 모드 - 목표 탈출 수 달성 / Classic 모드 - 팀 말 전체 완주
        TimeOver,   // 제한 시간이 다 되어서 종료된 경우
        Surrender   // 누군가 항복 버튼을 눌러서 종료된 경우
    }

    // 게임이 끝났을 때 결과를 담아서 여기저기(UI, GameManager 등)로 전달할 데이터 상자
    // [Serializable] = 유니티 인스펙터 창에 필드가 보이게 하거나, JSON으로 저장/전송할 수 있게 해주는 표시(속성)
    //                  기능상 필수는 아니지만 디버깅/저장할 때 편해서 데이터 클래스엔 거의 항상 붙임
    [Serializable]
    public class GameResultData
    {
        public GameResultType resultType;   // 어떤 이유로 끝났는지
        public TeamSlot winningTeam;        // 이긴 팀 (팀전일 때)
        public PlayerSlot winningPlayer;    // 이긴 플레이어 (솔로/개인전일 때)

        // 다인전(3팀 이상) 순위 시스템용: 1등부터 꼴찌까지 순서대로 담김 (winningTeam이 index 0)
        // 1vs1이나 2vs2처럼 팀이 2개뿐인 경우엔 [승리팀, 패배팀] 딱 2개만 들어감
        public List<TeamSlot> finalRanking = new List<TeamSlot>();
    }
}