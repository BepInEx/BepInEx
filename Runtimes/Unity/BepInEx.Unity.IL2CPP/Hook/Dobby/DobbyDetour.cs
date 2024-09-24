namespace BepInEx.Unity.IL2CPP.Hook.Dobby;

internal class DobbyDetour : BaseNativeDetour<DobbyDetour>
{
    public DobbyDetour(nint originalMethodPtr, nint targetMethodPtr) : base(originalMethodPtr, targetMethodPtr) { }

    protected override void ApplyImpl() => DobbyLib.Commit(Source);

    protected override unsafe void PrepareImpl()
    {
        nint trampolinePtr = 0;
        DobbyLib.Prepare(Source, Target, &trampolinePtr);
        OrigEntrypoint = trampolinePtr;
        HasOrigEntrypoint = true;
    }

    protected override void UndoImpl() => DobbyLib.Destroy(Source);

    protected override void FreeImpl() { }
}
