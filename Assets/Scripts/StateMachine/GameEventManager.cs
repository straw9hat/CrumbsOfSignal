using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameEventManager
{
    //Static Unity events
    public static event UnityAction MainMenuEvent;
    public static event UnityAction IdlePhaseEvent;
    public static event UnityAction CombatPhaseEvent;
    public static event UnityAction DeathEvent;

    //Static event triggers
    public static void OnMainMenu()
    {
        MainMenuEvent?.Invoke();
    }

    public static void OnRoundStart()
    {
        IdlePhaseEvent?.Invoke();
    }

    public static void OnCombat()
    {
        CombatPhaseEvent?.Invoke();
    }

    public static void OnDeath()
    {
        DeathEvent?.Invoke();
    }
}
