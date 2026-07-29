using System.Collections.Generic;
using UnityEngine;
using YutArena.Common;
//보드에서 이동하기 위해 위치 판정 위한 각 줄별로 리스트로 선언
//각 위치마다 고정좌표를 주고, 해당 좌표로의 이동으로 애니메이션 처리 생각 중
//윷 판의 종류는 달라지더라도 각 위치는 고정이기 때문에 효율적으로 보임

namespace YutArena.InGame
{
    public class BoardList
    {
        public BoardTileId[] FirstBoardList = new BoardTileId[]
        {
        BoardTileId.Start,      // 0
        BoardTileId.Outer01,    // 1
        BoardTileId.Outer02,    // 2
        BoardTileId.Outer03,    // 3
        BoardTileId.Outer04,    // 4
        BoardTileId.Corner01,   // 5
        };

        public BoardTileId[] SecondBoardList = new BoardTileId[]
        {
        BoardTileId.Outer05,    // 6
        BoardTileId.Outer06,    // 7
        BoardTileId.Outer07,    // 8
        BoardTileId.Outer08,    // 9
        BoardTileId.Corner02,   // 10
        };

        public BoardTileId[] ThirdBoardList = new BoardTileId[]
        {
        BoardTileId.Outer09,    // 11
        BoardTileId.Outer10,    // 12
        BoardTileId.Outer11,    // 13
        BoardTileId.Outer12,    // 14
        BoardTileId.Corner03,   // 15
        };

        public BoardTileId[] FourthBoardList = new BoardTileId[]
        {
        BoardTileId.Outer13,    // 16
        BoardTileId.Outer14,    // 17
        BoardTileId.Outer15,    // 18
        BoardTileId.Outer16,    // 19
        BoardTileId.Corner04,   // 20
        };

        public BoardTileId[] CenterBoardList = new BoardTileId[]
        {
        BoardTileId.Center,     // 21

        BoardTileId.Inner01,    // 22
        BoardTileId.Inner02,    // 23
        BoardTileId.Inner03,    // 24
        BoardTileId.Inner04,    // 25

        BoardTileId.Inner05,    // 26
        BoardTileId.Inner06,    // 27
        BoardTileId.Inner07,    // 28
        BoardTileId.Inner08     // 29
        };
    }
}

