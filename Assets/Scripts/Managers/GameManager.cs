using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public GameState gameState;

    // Hook win/lose UI (panels, sounds, next-level buttons) up to these in the Inspector
    [SerializeField] private UnityEvent onWin;
    [SerializeField] private UnityEvent onLose;

    // For code listeners, e.g. a UI script that shows different panels per state
    public static event Action<GameState> StateChanged;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ChangeState(GameState.GenerateGrid);
    }


    public void ChangeState(GameState newState) {

        gameState = newState;
        switch (newState)
        {
            case GameState.GenerateGrid:
                GridManager.Instance.GenerateGrid();
                break;

            case GameState.SpawnPlayerUnit:
                UnitManager.Instance.SpawnPlayers();
                break;

            case GameState.SpawnEnemyUnit:
                UnitManager.Instance.SpawnEnemies();
                break;

            case GameState.PlayerTurn:
                break;

            case GameState.EnemyTurn:
                break;

            case GameState.Win:
                HandleWin();
                break;

            case GameState.Lose:
                HandleLose();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }

        // Raised after the state's own handling, so listeners see the finished board
        StateChanged?.Invoke(newState);
     }


    // Tiles and arrows only accept clicks during PlayerTurn, so leaving it is enough to freeze input
    private void HandleWin()
    {
        UnitManager.Instance.ClearSelection();
        Debug.Log("Win: every Core Cell has been infected.");
        onWin?.Invoke();
    }

    private void HandleLose()
    {
        UnitManager.Instance.ClearSelection();
        Debug.Log($"Lose: the infection failed with {UnitManager.Instance.ActiveCoreCellCount} Core Cell(s) left.");
        onLose?.Invoke();
    }


    // For a Retry button on the win/lose UI
    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}


public enum GameState
{
    GenerateGrid = 0,
    SpawnPlayerUnit = 1,
    SpawnEnemyUnit = 2,
    PlayerTurn = 3,
    EnemyTurn = 4,
    Win = 5,
    Lose = 6,
}
