namespace BepInEx.IL2CPP.Hook.Dobby;

internal class DobbyDetour : BaseNativeDetour<DobbyDetour>
{
    public DobbyDetour(nint originalMethodPtr, nint detourMethodPtr) : base(originalMethodPtr, detourMethodPtr) { }

    protected override void ApplyImpl() => DobbyLib.Commit(OriginalMethodPtr);

    protected override unsafe void PrepareImpl()
    {
        nint trampolinePtr = 0;
        DobbyLib.Prepare(OriginalMethodPtr, DetourMethodPtr, &trampolinePtr);
        TrampolinePtr = trampolinePtr;
    }

    protected override void UndoImpl() => DobbyLib.Destroy(OriginalMethodPtr);

    protected override void FreeImpl() { }
}
