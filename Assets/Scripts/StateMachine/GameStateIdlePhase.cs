using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameStateIdlePhase : GameState
{
    private LevelManager levelManager;
    private int weaponsToSpawn = 3;

    public GameStateIdlePhase(StateManager stateManager) : base("IdlePhase", stateManager)
    {

    }


    public override GameState GetState()
    {
        return base.GetState();
    }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("Entered Idle State");
        levelManager = GameObject.FindGameObjectWithTag("LevelManager").GetComponent<LevelManager>();
        levelManager.InitializeFromMaskOnce();
        levelManager.SpawnWeaponsOnGround(weaponsToSpawn);
        levelManager.EnterCombatMode();
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("Exited Idle State");
    }

    public override void Update()
    {
        base.Update();

        //load the game state based on player input
    }

    private void OnEnable()
    {
        
    }

    private void OnDisable()
    {

    }


}
