using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStateCombatPhase : GameState
{
    private LevelManager levelManager;
    private Transform player;
    public GameStateCombatPhase(StateManager stateManager) : base("CombatPhase", stateManager)
    {
        
    }

    public override GameState GetState()
    {
        return base.GetState();
    }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("Entered Combat Phase state");
        levelManager = GameObject.FindGameObjectWithTag("LevelManager").GetComponent<LevelManager>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        levelManager.SpawnEnemiesForCombat(player);
        levelManager.ManageCombatPhase();

    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("Exited Combat Phase state");
    }

    public override void Update()
    {
        base.Update();
    }

    private void OnEnable()
    {

    }

    private void OnDisable()
    {

    }
}