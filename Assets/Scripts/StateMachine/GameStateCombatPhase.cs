using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStateCombatPhase : GameState
{
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