using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStateDeath : GameState
{
    public GameStateDeath(StateManager stateManager) : base("Death", stateManager)
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
    }

    public override void Update()
    {
        base.Update();
        //press escape to quit game
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            exitGame();
        }
    }

    private void OnEnable()
    {

    }

    private void OnDisable()
    {

    }

    //method to quit game
    private void exitGame()
    {
        Application.Quit();
    }

}
