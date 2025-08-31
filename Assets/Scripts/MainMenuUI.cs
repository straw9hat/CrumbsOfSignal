using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private StateManager m_stateManager;
    public void startGame()
    {
        GameStateIdlePhase initialIdle = new GameStateIdlePhase(m_stateManager);
        m_stateManager.SetNewState(initialIdle);
    }
}
