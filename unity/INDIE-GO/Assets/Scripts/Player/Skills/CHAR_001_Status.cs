using System;
using UnityEngine;
using YutArena.Common;
public class CHAR_001_Status : MonoBehaviour
{
    [SerializeField]
    private CharacterData characterData;
    void Start()
    {
        
    }

    void Update()
    {
        //passive_skill(인게임매니저 싱글톤 참조);
    }

    public void passive_skill(YutResult result)
    {
        if (YutResult.Do == result || YutResult.Mo == result)
        {
            //+1 턴
        }
    }
}
