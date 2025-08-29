using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStateMainMenu : GameState
{
    public GameStateMainMenu(StateManager stateManager) : base("MainMenu", stateManager)
    {

    }

    public override GameState GetState()
    {
        return base.GetState();
    }

    public override void OnEnter()
    {
        base.OnEnter();
        GameEventManager.MainMenuEvent += startGame;
        Debug.Log("Entered Main Menu");
    }

    public override void OnExit()
    {
        base.OnExit();
        GameEventManager.MainMenuEvent -= startGame;
        Debug.Log("Exited Main Menu");
    }

    //Press any key to navigate from cover page
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

    private void startGame()
    {
        StateManager.SetNewState(StateManager.IdlePhase);
        GameObject.Find("MainMenu").SetActive(false);
    }
}
