using YutArena.Common;
using UnityEngine;

//namespace YutArena.Managers{
    public class test : MonoBehaviour
    {
        YutResult yutResult = YutResult.Gae;
        private void Start()
        {
            Debug.Log((int)yutResult);
            Debug.Log(yutResult);
        }
    }
//}


