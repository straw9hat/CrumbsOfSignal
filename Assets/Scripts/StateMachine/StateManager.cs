using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class StateManager : MonoBehaviour
{
    //Declare all game states
    public GameStateMainMenu MainMenu;
    public GameStateIdlePhase IdlePhase;
    public GameStateCombatPhase CombatPhase;
    public GameStateDeath Death;

    protected GameState CurState;

    private void Awake()
    {
        // Initialize all states' objects
        MainMenu = new GameStateMainMenu(this);
        IdlePhase = new GameStateIdlePhase(this);
        CombatPhase = new GameStateCombatPhase(this);
        Death = new GameStateDeath(this);
    }

    // Start is called before the first frame update
    protected void Start()
    {
        //Set initial state
        CurState = GetInitialState();
        if (CurState != null)
        {
            CurState.OnEnter();
        }
    }

    // Update is called once per frame
    protected void Update()
    {
        if (CurState != null)
        {
            CurState.Update();
        }
    }

    //Transition method to switch states
    public void SetNewState(GameState NewState)
    {
        CurState.OnExit();
        CurState = NewState;
        CurState.OnEnter();
    }

    protected GameState GetInitialState()
    {
        return MainMenu;
    }

    public void OnEnable()
    {
        GameEventManager.IdlePhaseEvent += dummy;

    }

    public void OnDisable()
    {
        GameEventManager.IdlePhaseEvent -= dummy;
    }

    public GameState GetCurrentState()
    {
        return CurState;
    }
    private void dummy()
    {

    }
}
