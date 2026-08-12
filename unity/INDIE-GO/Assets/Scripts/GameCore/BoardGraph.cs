using YutArena.Common;

namespace YutArena.GameCore
{
    /// <summary>
    /// The single source of truth for board connectivity. This class is free
    /// from scene objects, so movement rules can be unit-tested without Unity.
    /// </summary>
    public static class BoardGraph
    {
        public static BoardTileId GetNextForward(
            BoardTileId current,
            BoardTileId previous,
            bool chooseShortcut)
        {
            if (current == BoardTileId.None) return BoardTileId.Outer01;
            if (chooseShortcut && current == BoardTileId.Corner01) return BoardTileId.Inner01;
            if (chooseShortcut && current == BoardTileId.Corner02) return BoardTileId.Inner05;
            if (chooseShortcut && current == BoardTileId.Center) return BoardTileId.Inner07;

            switch (current)
            {
                case BoardTileId.Outer01: return BoardTileId.Outer02;
                case BoardTileId.Outer02: return BoardTileId.Outer03;
                case BoardTileId.Outer03: return BoardTileId.Outer04;
                case BoardTileId.Outer04: return BoardTileId.Corner01;
                case BoardTileId.Corner01: return BoardTileId.Outer05;
                case BoardTileId.Outer05: return BoardTileId.Outer06;
                case BoardTileId.Outer06: return BoardTileId.Outer07;
                case BoardTileId.Outer07: return BoardTileId.Outer08;
                case BoardTileId.Outer08: return BoardTileId.Corner02;
                case BoardTileId.Corner02: return BoardTileId.Outer09;
                case BoardTileId.Outer09: return BoardTileId.Outer10;
                case BoardTileId.Outer10: return BoardTileId.Outer11;
                case BoardTileId.Outer11: return BoardTileId.Outer12;
                case BoardTileId.Outer12: return BoardTileId.Corner03;
                case BoardTileId.Corner03: return BoardTileId.Outer13;
                case BoardTileId.Outer13: return BoardTileId.Outer14;
                case BoardTileId.Outer14: return BoardTileId.Outer15;
                case BoardTileId.Outer15: return BoardTileId.Outer16;
                case BoardTileId.Outer16: return BoardTileId.None;
                case BoardTileId.Inner01: return BoardTileId.Inner02;
                case BoardTileId.Inner02: return BoardTileId.Center;
                case BoardTileId.Inner03: return BoardTileId.Inner04;
                case BoardTileId.Inner04: return BoardTileId.Corner03;
                case BoardTileId.Inner05: return BoardTileId.Inner06;
                case BoardTileId.Inner06: return BoardTileId.Center;
                case BoardTileId.Inner07: return BoardTileId.Inner08;
                case BoardTileId.Inner08: return BoardTileId.None;
                case BoardTileId.Center:
                    return previous == BoardTileId.Inner02
                        ? BoardTileId.Inner03
                        : BoardTileId.Inner07;
                default: return BoardTileId.None;
            }
        }

        public static BoardTileId GetNextBackward(BoardTileId current, BoardTileId previous)
        {
            switch (current)
            {
                case BoardTileId.None:
                    if (previous == BoardTileId.Outer01) return BoardTileId.Outer16;
                    if (previous == BoardTileId.Inner08) return BoardTileId.Inner08;
                    if (previous == BoardTileId.Outer16) return BoardTileId.Outer16;
                    return BoardTileId.None;
                case BoardTileId.Outer01: return BoardTileId.None;
                case BoardTileId.Outer02: return BoardTileId.Outer01;
                case BoardTileId.Outer03: return BoardTileId.Outer02;
                case BoardTileId.Outer04: return BoardTileId.Outer03;
                case BoardTileId.Corner01: return BoardTileId.Outer04;
                case BoardTileId.Outer05: return BoardTileId.Corner01;
                case BoardTileId.Outer06: return BoardTileId.Outer05;
                case BoardTileId.Outer07: return BoardTileId.Outer06;
                case BoardTileId.Outer08: return BoardTileId.Outer07;
                case BoardTileId.Corner02: return BoardTileId.Outer08;
                case BoardTileId.Outer09: return BoardTileId.Corner02;
                case BoardTileId.Outer10: return BoardTileId.Outer09;
                case BoardTileId.Outer11: return BoardTileId.Outer10;
                case BoardTileId.Outer12: return BoardTileId.Outer11;
                case BoardTileId.Corner03:
                    return previous == BoardTileId.Inner04 ? BoardTileId.Inner04 : BoardTileId.Outer12;
                case BoardTileId.Outer13: return BoardTileId.Corner03;
                case BoardTileId.Outer14: return BoardTileId.Outer13;
                case BoardTileId.Outer15: return BoardTileId.Outer14;
                case BoardTileId.Outer16: return BoardTileId.Outer15;
                case BoardTileId.Inner01: return BoardTileId.Corner01;
                case BoardTileId.Inner02: return BoardTileId.Inner01;
                case BoardTileId.Inner03: return BoardTileId.Center;
                case BoardTileId.Inner04: return BoardTileId.Inner03;
                case BoardTileId.Inner05: return BoardTileId.Corner02;
                case BoardTileId.Inner06: return BoardTileId.Inner05;
                case BoardTileId.Inner07: return BoardTileId.Center;
                case BoardTileId.Inner08: return BoardTileId.Inner07;
                case BoardTileId.Center:
                    return previous == BoardTileId.Inner03 || previous == BoardTileId.Inner02
                        ? BoardTileId.Inner02
                        : BoardTileId.Inner06;
                default: return BoardTileId.None;
            }
        }
    }
}
