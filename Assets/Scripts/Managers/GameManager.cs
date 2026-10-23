using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public GameState gameState;

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

            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }
     }
}


public enum GameState
{
    GenerateGrid = 0,
    SpawnPlayerUnit = 1,
    SpawnEnemyUnit = 2,
    PlayerTurn = 3,
    EnemyTurn = 4,
}