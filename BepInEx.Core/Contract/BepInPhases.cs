namespace BepInEx;

/// <summary>
///     A container class for standard BepInEx phase names
/// </summary>
public static class BepInPhases
{
    /// <summary>
    ///     A phase the occurs the earliest possible moment once basic systems have been initialised
    /// </summary>
    public const string Entrypoint = "Entrypoint";
    
    /// <summary>
    ///     A phase that occurs right before loading game assemblies
    /// </summary>
    public const string BeforeGameAssembliesLoaded = "BeforeGameAssembliesLoaded";
    
    /// <summary>
    ///     A phase that occurs after the game assemblies were loaded
    /// </summary>
    public const string AfterGameAssembliesLoaded = "AfterGameAssembliesLoaded";
    
    /// <summary>
    ///     A phase that occurs once the game has been fully initialised
    /// </summary>
    public const string GameInitialised = "GameInitialised";
}
