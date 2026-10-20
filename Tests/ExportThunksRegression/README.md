# Export thunk regression tests

Regression coverage for `INativeDetour.FollowExportThunks` (BepInEx issue
[#1403](https://github.com/BepInEx/BepInEx/issues/1403)).

`FollowExportThunks` decodes PE export stubs (`E9 rel32` jumps and `FF 25` indirect jumps) so Dobby
is handed the real function prologue instead of the stub. Those instruction shapes are x86/x64
encodings: on any other architecture (e.g. Apple Silicon / ARM64), those byte values do not
identify an x86 export stub. Leave the original pointer unchanged for the detour provider.

## What the tests check

The test calls the **real production method** (`private static nint FollowExportThunks(nint)`,
reached via reflection) against synthetic stubs written into unmanaged memory allocated with
`Marshal.AllocHGlobal`. Machine code is never executed, no detour provider (Dobby/Funchook) is
constructed, and `INativeDetour.Create` is never called.

| Check | x86/x64 expectation | non-x86/x64 expectation |
| --- | --- | --- |
| `null` pointer | `0` | `0` |
| Ordinary body (`55 48 89 E5 C3`, no branch) | unchanged | unchanged |
| `E9 rel32` forward jump, target inside the buffer | resolved target | original pointer |
| `E9 rel32` backward jump, target inside the buffer | resolved target | original pointer |
| `FF 25` RIP-relative indirect jump (64-bit only) | resolved target | original pointer |
| `FF 25` absolute32 indirect jump (real 32-bit x86 only) | resolved target | - |
| Mixed `E9 -> FF 25 -> E9` chain | resolved target | original pointer |
| Hop budget: 9 chained `E9` jumps | 9th thunk returned undecoded (8-hop limit) | original pointer |

All jump and pointer targets live inside the same allocated buffer, so a build without the
architecture gate stays readable while decoding (no `SIGSEGV` based evidence). On non-x86/x64 the
ungated build resolves every stub to the synthetic target instead of the original pointer, which is
exactly what the "original pointer" expectations fail on.

BepInEx paths are redirected (`Paths.SetExecutablePath` with `bepinRootPath` pointing into a
throwaway temp directory) before the production type is first touched, because the type initializer
binds `ConfigFile.CoreConfig`. No game files and no real BepInEx installation are read or written;
the temp directory is deleted on exit.

## Running

Run from the repository root:

```sh
dotnet run --project Tests/ExportThunksRegression/ExportThunksRegression.csproj -c Debug
```

Exit codes: `0` all checks passed, `1` at least one check failed, `2` the production method could
not be located (infrastructure problem).

Platform boundaries:

* **Apple Silicon (arm64)** - all stubs must be returned unchanged.
* **Rosetta / x64 process** - build with the SDK above, then execute the built DLL with an x64
  .NET 6 runtime: `arch -x86_64 /path/to/x64-dotnet Tests/ExportThunksRegression/bin/Debug/net6.0/ExportThunksRegression.dll`.
  Rosetta must be available. Alternatively, `arch -x86_64 dotnet run --project Tests/ExportThunksRegression/ExportThunksRegression.csproj`
  requires an x64 SDK as well as a compatible runtime. The stub checks must resolve to their targets.
* **32-bit x86** - the `FF 25` absolute32 check runs only here, in a real 32-bit process. The
  RIP-relative check is skipped. The operand is never encoded from a 64-bit process.
* Windows and Linux have not been exercised by the current test run; the managed logic is
  platform-independent apart from the architecture dispatch above.

The project is intentionally **not** part of `BepInEx.sln`; it references
`Runtimes/Unity/BepInEx.Unity.IL2CPP/BepInEx.Unity.IL2CPP.csproj` directly and has no additional
NuGet packages of its own.
