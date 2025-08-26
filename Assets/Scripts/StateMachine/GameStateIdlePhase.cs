using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameStateIdlePhase : GameState
{

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
