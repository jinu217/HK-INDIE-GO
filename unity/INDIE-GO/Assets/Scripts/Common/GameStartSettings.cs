using System;

namespace YutArena.Common
{
    // 게임 모드 종류
    public enum GameMode
    {
        Classic,     // 기본 윷놀이 규칙
        Escape,      // 말을 탈출시켜 점수를 얻는 모드
        KillTheKing  // 추후 구현 예정인 킬 더 킹 모드
    }

    // 맵 종류
    public enum MapType
    {
        Basic,      // 기본 맵
        Grassland,  // 초원 맵
        Korean      // 한국 테마 맵
    }

    // 턴 제한 시간 적용 방식
    public enum TurnTimeMode
    {
        Limited,   // 제한 시간 사용
        Unlimited  // 제한 시간 없음
    }

    // 게임 시작 전에 로비/설정 UI에서 확정해야 하는 값 모음
    [Serializable]
    public class GameStartSettings
    {
        public GameMode gameMode;                   // 선택한 게임 모드
        public MapType mapType;                     // 선택한 맵 종류
        public MatchComposition matchComposition;   // 선택한 대전 구성

        public int playerCount;                     // 실제 참가 플레이어 수
        public int pieceCountPerPlayer;             // 플레이어 한 명이 사용하는 말 개수

        public int targetEscapeCount;               // Escape 모드에서 승리 또는 탈출에 필요한 말 수
        public int timeLimitMinutes;                // 게임 전체 제한 시간, 0이면 무제한으로 처리 가능

        public TurnTimeMode turnTimeMode;           // 턴 제한 시간 사용 방식

        public int throwTimeSeconds;                // 윷 던지기 단계의 기본 제한 시간
        public int actionTimeSeconds;               // 말 이동/스킬 사용 단계의 기본 제한 시간

        public bool useSkill;                       // 캐릭터 스킬 사용 여부
        public bool useItem;                        // 아이템 사용 여부
        public bool useSpecialTile;                 // 이벤트/함정/안전 칸 같은 특수 칸 사용 여부
    }
}
