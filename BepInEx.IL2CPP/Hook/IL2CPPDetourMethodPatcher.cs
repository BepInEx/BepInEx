using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using UnhollowerBaseLib;
using UnhollowerBaseLib.Runtime;

namespace BepInEx.IL2CPP.Hook
{
	public unsafe class IL2CPPDetourMethodPatcher : MethodPatcher
	{
		private static readonly MethodInfo IL2CPPToManagedStringMethodInfo
			= AccessTools.Method(typeof(UnhollowerBaseLib.IL2CPP), nameof(UnhollowerBaseLib.IL2CPP.Il2CppStringToManaged));

		private static readonly MethodInfo ManagedToIL2CPPStringMethodInfo
			= AccessTools.Method(typeof(UnhollowerBaseLib.IL2CPP), nameof(UnhollowerBaseLib.IL2CPP.ManagedStringToIl2Cpp));

		private static readonly MethodInfo ObjectBaseToPtrMethodInfo
			= AccessTools.Method(typeof(UnhollowerBaseLib.IL2CPP), nameof(UnhollowerBaseLib.IL2CPP.Il2CppObjectBaseToPtr));

		private static readonly MethodInfo ReportExceptionMethodInfo
			= AccessTools.Method(typeof(IL2CPPDetourMethodPatcher), nameof(ReportException));


		private static readonly ManualLogSource DetourLogger = Logger.CreateLogSource("Detour");

		private FastNativeDetour nativeDetour;

		private Il2CppMethodInfo* originalNativeMethodInfo;
		private Il2CppMethodInfo* modifiedNativeMethodInfo;

		/// <summary>
		/// Constructs a new instance of <see cref="NativeDetour"/> method patcher.
		/// </summary>
		/// <param name="original"></param>
		public IL2CPPDetourMethodPatcher(MethodBase original) : base(original)
		{
			Init();
		}

		private void Init()
		{
			// Get the native MethodInfo struct for the target method

			originalNativeMethodInfo = (Il2CppMethodInfo*)
				(IntPtr)UnhollowerUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(Original).GetValue(null);

			// Create a trampoline from the original target method

			var trampolinePtr = DetourGenerator.CreateTrampolineFromFunction(originalNativeMethodInfo->methodPointer, out _, out _);

			// Create a modified native MethodInfo struct to point towards the trampoline

			modifiedNativeMethodInfo = (Il2CppMethodInfo*)Marshal.AllocHGlobal(Marshal.SizeOf<Il2CppMethodInfo>());
			Marshal.StructureToPtr(*originalNativeMethodInfo, (IntPtr)modifiedNativeMethodInfo, false);

			modifiedNativeMethodInfo->methodPointer = trampolinePtr;
		}

		/// <inheritdoc />
		public override DynamicMethodDefinition PrepareOriginal()
		{
			return null;
		}

		/// <inheritdoc />
		public override MethodBase DetourTo(MethodBase replacement)
		{
			// Unpatch an existing detour if it exists

			nativeDetour?.Dispose();

			// Generate a new DMD of the modified unhollowed method, and apply harmony patches to it

			var copiedDmd = CopyOriginal();

			HarmonyManipulator.Manipulate(copiedDmd.OriginalMethod, copiedDmd.OriginalMethod.GetPatchInfo(), new ILContext(copiedDmd.Definition));


			// Generate the MethodInfo instances

			var managedHookedMethod = copiedDmd.Generate();
			var unmanagedTrampolineMethod = GenerateNativeToManagedTrampoline(managedHookedMethod).Generate();


			// Apply a detour from the unmanaged implementation to the patched harmony method

			var unmanagedDelegateType = DelegateTypeFactory.instance.CreateDelegateType(unmanagedTrampolineMethod,
				CallingConvention.Cdecl);

			var detourPtr = Marshal.GetFunctionPointerForDelegate(unmanagedTrampolineMethod.CreateDelegate(unmanagedDelegateType));

			nativeDetour = new FastNativeDetour(originalNativeMethodInfo->methodPointer, detourPtr);

			nativeDetour.Apply();

			// TODO: Add an ILHook for the original unhollowed method to go directly to managedHookedMethod
			// Right now it goes through three times as much interop conversion as it needs to, when being called from managed side

			return managedHookedMethod;
		}

		/// <inheritdoc />
		public override DynamicMethodDefinition CopyOriginal()
		{
			var dmd = new DynamicMethodDefinition(Original);
			dmd.Definition.Name = "UnhollowedWrapper_" + dmd.Definition.Name;

			var cursor = new ILCursor(new ILContext(dmd.Definition));


			// Remove il2cpp_object_get_virtual_method

			if (cursor.TryGotoNext(x => x.MatchLdarg(0),
				x => x.MatchCall(typeof(UnhollowerBaseLib.IL2CPP), nameof(UnhollowerBaseLib.IL2CPP.Il2CppObjectBaseToPtr)),
				x => x.MatchLdsfld(out _),
				x => x.MatchCall(typeof(UnhollowerBaseLib.IL2CPP), nameof(UnhollowerBaseLib.IL2CPP.il2cpp_object_get_virtual_method))))
			{
				cursor.RemoveRange(4);
			}
			else
			{
				cursor.Goto(0)
					.GotoNext(x => x.MatchLdsfld(UnhollowerUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(Original)))
					.Remove();
			}

			// Replace original IL2CPPMethodInfo pointer with a modified one that points to the trampoline

			cursor
				.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I8, ((IntPtr)modifiedNativeMethodInfo).ToInt64())
				.Emit(Mono.Cecil.Cil.OpCodes.Conv_I);

			return dmd;
		}

		/// <summary>
		/// A handler for <see cref="PatchManager.ResolvePatcher"/> that checks if a method doesn't have a body
		/// (e.g. it's icall or marked with <see cref="DynDllImportAttribute"/>) and thus can be patched with
		/// <see cref="NativeDetour"/>.
		/// </summary>
		/// <param name="sender">Not used</param>
		/// <param name="args">Patch resolver arguments</param>
		///
		public static void TryResolve(object sender, PatchManager.PatcherResolverEventArgs args)
		{
			if (args.Original.DeclaringType?.IsSubclassOf(typeof(Il2CppObjectBase)) == true)
				args.MethodPatcher = new IL2CPPDetourMethodPatcher(args.Original);
		}

		private DynamicMethodDefinition GenerateNativeToManagedTrampoline(MethodInfo targetManagedMethodInfo)
		{
			// managedParams are the unhollower types used on the managed side
			// unmanagedParams are IntPtr references that are used by IL2CPP compiled assembly

			var managedParams = Original.GetParameters().Select(x => x.ParameterType).ToArray();
			var unmanagedParams = new Type[managedParams.Length + 2]; // +1 for thisptr at the start, +1 for methodInfo at the end
			// TODO: Check if this breaks for static IL2CPP methods


			unmanagedParams[0] = typeof(IntPtr);
			unmanagedParams[unmanagedParams.Length - 1] = typeof(Il2CppMethodInfo*);
			Array.Copy(managedParams.Select(ConvertManagedTypeToIL2CPPType).ToArray(), 0,
				unmanagedParams, 1, managedParams.Length);

			var managedReturnType = AccessTools.GetReturnedType(Original);
			var unmanagedReturnType = ConvertManagedTypeToIL2CPPType(managedReturnType);

			var dmd = new DynamicMethodDefinition("(il2cpp -> managed) " + Original.Name,
				unmanagedReturnType,
				unmanagedParams
			);

			var il = dmd.GetILGenerator();

			il.BeginExceptionBlock();


			// Declare a list of variables to dereference back to the original pointers.
			// This is required due to the needed unhollower type conversions, so we can't directly pass some addresses as byref types

			LocalBuilder[] indirectVariables = new LocalBuilder[managedParams.Length];

			if (!Original.IsStatic)
			{
				// Load thisptr as arg0

				il.Emit(OpCodes.Ldarg_0);
				EmitConvertArgumentToManaged(il, Original.DeclaringType, out _);
			}

			for (int i = 0; i < managedParams.Length; ++i)
			{
				il.Emit(OpCodes.Ldarg_S, i + 1);
				EmitConvertArgumentToManaged(il, managedParams[i], out indirectVariables[i]);
			}

			// Run the managed method

			il.Emit(OpCodes.Call, targetManagedMethodInfo);


			// Store the managed return type temporarily (if there was one)

			LocalBuilder managedReturnVariable = null;

			if (managedReturnType != typeof(void))
			{
				managedReturnVariable = il.DeclareLocal(managedReturnType);
				il.Emit(OpCodes.Stloc, managedReturnVariable);
			}


			// Convert any managed byref values into their relevant IL2CPP types, and then store the values into their relevant dereferenced pointers

			for (int i = 0; i < managedParams.Length; ++i)
			{
				if (indirectVariables[i] == null)
					continue;

				il.Emit(OpCodes.Ldarg_S, i + 1);
				il.Emit(OpCodes.Ldloc, indirectVariables[i]);

				EmitConvertManagedTypeToIL2CPP(il, managedParams[i].GetElementType());

				il.Emit(OpCodes.Stind_I);
			}

			// Handle any lingering exceptions

			il.BeginCatchBlock(typeof(Exception));

			il.Emit(OpCodes.Call, ReportExceptionMethodInfo);

			il.EndExceptionBlock();

			// Convert the return value back to an IL2CPP friendly type (if there was a return value), and then return

			if (managedReturnVariable != null)
			{
				il.Emit(OpCodes.Ldloc, managedReturnVariable);
				EmitConvertManagedTypeToIL2CPP(il, managedReturnType);
			}

			il.Emit(OpCodes.Ret);

			return dmd;
		}

		private static void ReportException(Exception ex)
		{
			DetourLogger.LogError(ex.ToString());
		}

		private static Type ConvertManagedTypeToIL2CPPType(Type managedType)
		{
			if (managedType.IsByRef)
			{
				Type directType = managedType.GetElementType();
				if (directType == typeof(string) || directType.IsSubclassOf(typeof(Il2CppObjectBase)))
				{
					return typeof(IntPtr*);
				}
			}
			else if (managedType == typeof(string) || managedType.IsSubclassOf(typeof(Il2CppObjectBase)))
			{
				return typeof(IntPtr);
			}

			return managedType;
		}

		private static void EmitConvertManagedTypeToIL2CPP(ILGenerator il, Type returnType)
		{
			if (returnType == typeof(string))
			{
				il.Emit(OpCodes.Call, ManagedToIL2CPPStringMethodInfo);
			}
			else if (!returnType.IsValueType && returnType.IsSubclassOf(typeof(Il2CppObjectBase)))
			{
				il.Emit(OpCodes.Call, ObjectBaseToPtrMethodInfo);
			}
		}

		private static void EmitConvertArgumentToManaged(ILGenerator il, Type managedParamType, out LocalBuilder variable)
		{
			variable = null;

			if (managedParamType.IsValueType) // don't need to convert blittable types
				return;

			void EmitCreateIl2CppObject()
			{
				Label endLabel = il.DefineLabel();
				Label notNullLabel = il.DefineLabel();

				il.Emit(OpCodes.Dup);
				il.Emit(OpCodes.Brtrue_S, notNullLabel);

				il.Emit(OpCodes.Pop);
				il.Emit(OpCodes.Ldnull);
				il.Emit(OpCodes.Br_S, endLabel);

				il.MarkLabel(notNullLabel);
				il.Emit(OpCodes.Newobj, AccessTools.DeclaredConstructor(managedParamType, new[] { typeof(IntPtr) }));

				il.MarkLabel(endLabel);
			}

			void HandleTypeConversion(Type originalType)
			{
				if (originalType == typeof(string))
				{
					il.Emit(OpCodes.Call, IL2CPPToManagedStringMethodInfo);
				}
				else if (originalType.IsSubclassOf(typeof(Il2CppObjectBase)))
				{
					EmitCreateIl2CppObject();
				}
			}

			if (managedParamType.IsByRef)
			{
				Type directType = managedParamType.GetElementType();

				variable = il.DeclareLocal(directType);

				il.Emit(OpCodes.Ldind_I);

				HandleTypeConversion(directType);

				il.Emit(OpCodes.Stloc, variable);
				il.Emit(OpCodes.Ldloca, variable);
			}
			else
			{
				HandleTypeConversion(managedParamType);
			}
		}
	}
}