using UnityEngine;
//cc정보 정의

namespace YutArena.InGame
{
    public enum CcDefine
    {
        None = 0,
        //말의 기본 상태

        Stun,
        //혹시 몰라 추가해둔 스턴(스킬 사용 불가, 이동 불가)

        Silence,
        //혹시 몰라  추가해둔 침묵(스킬 사용 불가, 이동 가능)

        Retire,
        //추가 턴 부여 안하고, 말이 잡힌 상태

        Kill
        //추가 턴 부여하고, 말이 잡힌 상태


        //확장 가능

        //기연이가 말들의 상태 확인 한 후 Kill이면 진행중인 플레이어에게 추가 턴 부여하고 CC상태를 None으로 변경
        //기연이가 말들의 상태 확인 한 후 Retire이면 진행중인 플레이어에게 추가 턴 부여는 하지 않고 CC상태를 None으로 변경
        //Retire, Kill과 같이 말이 보드에서 아예 나가는 경우는 내가 BoardPosition 설정을 할 예정
        //기연이는 CC기 남은 턴 관리(Stun,Silence등등), 그에 따른 CC기 초기화, CC기 남은 턴 감소, 추가 턴 부여만 관리 해주면 될 듯함
    }
}