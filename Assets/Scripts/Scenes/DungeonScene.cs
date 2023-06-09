using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DungeonScene : BaseScene
{
    [SerializeField]
    Transform playerSpawn;

    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Dungeon;  // 타입 설정

        Camera.main.gameObject.GetOrAddComponent<CameraController>().SetPlayer(Managers.Game.GetPlayer());

        Managers.Game.GetPlayer().transform.position = playerSpawn.position;
    }

    public override void Clear()
    {
        
    }
}
