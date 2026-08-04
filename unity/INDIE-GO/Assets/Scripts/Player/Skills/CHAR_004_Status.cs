using System;
using Unity.Mathematics;
using UnityEngine;
using YutArena.Common;
public class CHAR_004_Status : MonoBehaviour
{
    [SerializeField]
    private CharacterData characterData;
    private void Start()
    {
        
    }

    void Update()
    {
        
    }

    private void OnEnable()
    {
        InitStatus();
        Generate3DModel();
        //YutManager.Yutresult += PassiveSkill;
    }
    private void OnDisable()
    {
        //YutManager.Yutresult -= PassiveSkill;
    }
    public void PassiveSkill(YutResult result)
    {
        if (YutResult.Do == result || YutResult.Mo == result)
        {
            Debug.Log($"[{characterData.char_Name}] 패시브 발동");
            //+1 턴
        }
    }

    private void InitStatus()
    {
        if (characterData == null) return;
        
    }

    private void Generate3DModel()
    {
        if (characterData != null && characterData.visualModelPrefab != null)
        {
            GameObject spawnModel = Instantiate(characterData.visualModelPrefab, this.transform);

            spawnModel.transform.localPosition = Vector3.zero;
            spawnModel.transform.localRotation = Quaternion.identity;

            Debug.Log("3D Models spawn success");
        }
    }
}
