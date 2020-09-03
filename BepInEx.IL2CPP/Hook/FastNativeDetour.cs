using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Hook
{
	public class FastNativeDetour : IDetour
	{
		protected byte[] BackupBytes { get; set; }

		public bool IsValid { get; protected set; } = true;
		public bool IsApplied { get; protected set; }


		public IntPtr OriginalFuncPtr { get; protected set; }
		public IntPtr DetourFuncPtr { get; protected set; }


		public IntPtr TrampolinePtr { get; protected set; } = IntPtr.Zero;
		public int TrampolineSize { get; protected set; } = 0;

		protected MethodInfo TrampolineMethod { get; set; }


		public FastNativeDetour(IntPtr originalFuncPtr, IntPtr detourFuncPtr)
		{
			OriginalFuncPtr = originalFuncPtr;
			DetourFuncPtr = detourFuncPtr;

			// TODO: This may not be safe during undo if the method is smaller than 20 bytes
			BackupBytes = new byte[20];
			Marshal.Copy(originalFuncPtr, BackupBytes, 0, 20);
		}


		public void Apply()
		{
			Apply(null);
		}


		public void Apply(ManualLogSource debuggerLogSource)
		{
			if (IsApplied)
				return;

			int trampolineLength;

			if (debuggerLogSource == null)
				TrampolinePtr = TrampolineGenerator.Generate(OriginalFuncPtr, DetourFuncPtr, out trampolineLength);
			else
				TrampolinePtr = TrampolineGenerator.Generate(debuggerLogSource, OriginalFuncPtr, DetourFuncPtr, out trampolineLength);

			TrampolineSize = trampolineLength;

			IsApplied = true;
		}

		public void Undo()
		{
			Marshal.Copy(BackupBytes, 0, OriginalFuncPtr, BackupBytes.Length);

			DetourHelper.Native.MemFree(TrampolinePtr);

			TrampolinePtr = IntPtr.Zero;
			TrampolineSize = 0;

			IsApplied = false;
		}

		public void Free()
		{
			IsValid = false;
		}

		public MethodBase GenerateTrampoline(MethodBase signature = null)
		{
			if (TrampolineMethod == null)
			{
				TrampolineMethod = DetourHelper.GenerateNativeProxy(TrampolinePtr, signature);
			}

			return TrampolineMethod;
		}

		public T GenerateTrampoline<T>() where T : Delegate
		{
			if (!typeof(Delegate).IsAssignableFrom(typeof(T)))
				throw new InvalidOperationException($"Type {typeof(T)} not a delegate type.");

			return GenerateTrampoline(typeof(T).GetMethod("Invoke")).CreateDelegate(typeof(T)) as T;
		}

		public void Dispose()
		{
			if (!IsValid)
				return;

			Undo();
			Free();
		}
	}
}