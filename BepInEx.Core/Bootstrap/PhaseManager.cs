using System;
using BepInEx.Logging;

namespace BepInEx.Core.Bootstrap;

/// <summary>
///     The manager class that allows to start phases and to listen when a phase starts
/// </summary>
public class PhaseManager
{
    /// <summary>
    ///     The current instance of the phase manager
    /// </summary>
    public static PhaseManager Instance { get; } = new();

    private PhaseManager() { }
    
    /// <summary>
    ///     The current phase
    /// </summary>
    public string CurrentPhase { get; private set; }

    /// <summary>
    ///     Occurs when a phase starts
    /// </summary>
    public event Action<string> OnPhaseStarted;

    /// <summary>
    ///     Starts a phase
    /// </summary>
    /// <param name="phase">The name of the phase</param>
    /// <seealso cref="BepInPhases"/>
    public void StartPhase(string phase)
    {
        Logger.Log(LogLevel.Message, "Started phase " + phase);
        CurrentPhase = phase;
        OnPhaseStarted?.Invoke(phase);
        Logger.Log(LogLevel.Message, "Ended phase " + phase);
    }
}
