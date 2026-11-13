using UnityEngine.InputSystem;
using YutArena.Common;

namespace YutArena.UI
{
    public static class SettingsBindings
    {
        public static readonly string[] Labels = { "윷 던지기", "말 선택", "이동할 칸·갈림길 선택", "스킬 사용", "스킬 대상 지정", "취소·뒤로가기", "캐릭터 설명 보기", "채팅 열기" };
        public static readonly string[] Secondary = { "던지기 버튼 클릭", "—", "—", "스킬 버튼 클릭", "—", "빈 공간 클릭", "—", "—" };
        public static string[] Defaults() => GameInput.DefaultPaths();
        public static string[] Load() => GameInput.LoadBindingPaths();
        public static void Save(string[] values) => GameInput.SaveBindings(values);
        public static string Display(string path)
        {
            if (path == "<Mouse>/leftButton") return "마우스 좌클릭";
            if (path == "<Mouse>/rightButton") return "마우스 우클릭";
            if (path == "<Mouse>/middleButton") return "마우스 휠클릭";
            return string.IsNullOrEmpty(path) ? "미지정" : InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
    }
}
