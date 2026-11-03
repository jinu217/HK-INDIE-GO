using UnityEngine;

namespace YutArena.InGame
{
    /// <summary>연결/발동 확인용 예제입니다. 실제 게임 효과는 별도의 이벤트 스크립트에 작성합니다.</summary>
    [CreateAssetMenu(menuName = "YutArena/Map Events/Log", fileName = "LogMapSpecialTileEvent")]
    public sealed class LogMapSpecialTileEvent : MapSpecialTileEvent
    {
        [SerializeField] private string message = "특수 타일 이벤트 발동";
        public override string DebugLabel => message;

        public override void Execute(MapSpecialTileContext context)
        {
            // MapManager emits one common trace for every map event, including this example.
        }
    }
}
