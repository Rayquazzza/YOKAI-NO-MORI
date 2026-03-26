using System;

public interface IGameStateService : IDisposableService
{
    public event Action<EGameState> OnGameStateChanged;

    public void ChangeGameState(EGameState newGameState);

    public EGameState GetCurrentGameState();


}
