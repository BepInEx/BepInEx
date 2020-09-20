using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using Iced.Intel;
using MonoMod.RuntimeDetour;

namespace BepInEx.IL2CPP
{
	public static class DetourGenerator
	{
		private static ManualLogSource logger = Logger.CreateLogSource("DetourGen");

		public static void Disassemble(ManualLogSource logSource, IntPtr memoryPtr, int size)
		{
			byte[] data = new byte[size];
			Marshal.Copy(memoryPtr, data, 0, size);

			var formatter = new NasmFormatter();
			var output = new StringOutput();
			var codeReader = new ByteArrayCodeReader(data);
			var decoder = Decoder.Create(64, codeReader);
			decoder.IP = (ulong)memoryPtr.ToInt64();
			while (codeReader.CanReadByte)
			{
				decoder.Decode(out var instr);
				formatter.Format(instr, output);
				logSource.LogDebug($"{instr.IP:X16} {output.ToStringAndReset()}");

				if (instr.Code == Code.Jmp_rm64 && instr.Immediate32 == 0) // && instr.IsIPRelativeMemoryOperand && instr.IPRelativeMemoryAddress = 6
				{
					byte[] address = new byte[8];

					for (int i = 0; i < 8; i++)
						address[i] = (byte)codeReader.ReadByte();

					logSource.LogDebug($"{(instr.IP + (ulong)instr.Length):X16} db 0x{BitConverter.ToUInt64(address, 0):X16}");
					decoder.IP += 8;
				}
			}
		}

		public static int GetDetourLength(Architecture arch)
			=> arch == Architecture.X64 ? 14 : 5;

		/// <summary>
		/// Writes a detour on <see cref="functionPtr"/> to redirect to <see cref="detourPtr"/>.
		/// </summary>
		/// <param name="functionPtr">The pointer to the function to apply the detour to.</param>
		/// <param name="detourPtr">The pointer to the function to redirect to.</param>
		/// <param name="architecture">The architecture of the current platform.</param>
		/// <param name="minimumLength">The minimum amount of length that the detour should consume. If the generated redirect is smaller than this, the remaining space is padded with NOPs.</param>
		public static void ApplyDetour(IntPtr functionPtr, IntPtr detourPtr, Architecture architecture, int minimumLength = 0)
		{
			byte[] jmp = GenerateAbsoluteJump(detourPtr, functionPtr, architecture);

			Marshal.Copy(jmp, 0, functionPtr, jmp.Length);

			// Fill remaining space with NOP instructions
			for (int i = jmp.Length; i < minimumLength; i++)
				Marshal.WriteByte(functionPtr + i, 0x90);
		}

		public static IntPtr CreateTrampolineFromFunction(IntPtr originalFuncPointer, out int trampolineLength, out int jmpLength)
		{
			byte[] instructionBuffer = new byte[32];
			Marshal.Copy(originalFuncPointer, instructionBuffer, 0, 32);

			var trampolinePtr = DetourHelper.Native.MemAlloc(80);

			DetourHelper.Native.MakeWritable(trampolinePtr, 80);

			var arch = IntPtr.Size == 8 ? Architecture.X64 : Architecture.X86;

			int minimumTrampolineLength = GetDetourLength(arch);

			CreateTrampolineFromFunction(instructionBuffer, originalFuncPointer, trampolinePtr, minimumTrampolineLength, arch, out trampolineLength, out jmpLength);

			DetourHelper.Native.MakeExecutable(originalFuncPointer, 32);
			DetourHelper.Native.MakeExecutable(trampolinePtr, (uint)trampolineLength);

			return trampolinePtr;
		}
		
		/// <summary>
		/// Reads assembly from <see cref="functionPtr"/> (at least <see cref="minimumTrampolineLength"/> bytes), and writes it to <see cref="trampolinePtr"/> plus a jmp to continue execution.
		/// </summary>
		/// <param name="instructionBuffer">The buffer to copy assembly from.</param>
		/// <param name="functionPtr">The pointer to the function to copy assembly from.</param>
		/// <param name="trampolinePtr">The pointer to write the trampoline assembly to.</param>
		/// <param name="arch">The architecture of the current platform.</param>
		/// <param name="minimumTrampolineLength">Copies at least this many bytes of assembly from <see cref="functionPtr"/>.</param>
		/// <param name="trampolineLength">Returns the total length of the trampoline, in bytes.</param>
		/// <param name="jmpLength">Returns the length of the jmp at the end of the trampoline, in bytes.</param>
		public static void CreateTrampolineFromFunction(byte[] instructionBuffer, IntPtr functionPtr, IntPtr trampolinePtr, int minimumTrampolineLength, Architecture arch, out int trampolineLength, out int jmpLength)
		{
			// Decode original function up until we go past the needed bytes to write the jump to patchedFunctionPtr

			var codeReader = new ByteArrayCodeReader(instructionBuffer);
			var decoder = Decoder.Create(arch == Architecture.X64 ? 64 : 32, codeReader);
			decoder.IP = (ulong)functionPtr.ToInt64();

			uint totalBytes = 0;
			var origInstructions = new InstructionList();
			while (codeReader.CanReadByte)
			{
				decoder.Decode(out var instr);

				if (instr.IsIPRelativeMemoryOperand)
				{
					// TODO: AssemlberRegisters not needed, figure out what props to actually change
					// TODO: Check if it's better to use InternalOp0Kind (and other similar props) instead of normal ones
					// TODO: Probably need to check if the target is within the trampoline boundaries and thus shouldn't be fixed
					logger.LogDebug($"Got ptr with relative memory operand: {instr}");
					var addr = instr.IPRelativeMemoryAddress;
					logger.LogDebug($"Address: {addr:X}");
					instr.MemoryBase = Register.None;
					var op = AssemblerRegisters.__byte_ptr[addr].ToMemoryOperand(64);
					instr.Op0Kind = OpKind.Memory;
					instr.MemoryBase = op.Base;
					instr.MemoryIndex = op.Index;
					instr.MemoryIndexScale = op.Scale;
					instr.MemoryDisplSize = op.DisplSize;
					instr.MemoryDisplacement = (uint)op.Displacement;
					instr.IsBroadcast = op.IsBroadcast;
					instr.SegmentPrefix = op.SegmentPrefix;
					logger.LogDebug($"After edit: {instr}");
				}
				
				origInstructions.Add(instr);
				
				totalBytes += (uint)instr.Length;
				if (instr.Code == Code.INVALID)
					throw new Exception("Found garbage");
				if (totalBytes >= minimumTrampolineLength)
					break;

				switch (instr.FlowControl)
				{
					case FlowControl.Next:
						break;

					case FlowControl.UnconditionalBranch:
						if (instr.Op0Kind == OpKind.NearBranch64)
						{
							var target = instr.NearBranchTarget;
						}

						break;
					//goto default;
					case FlowControl.Interrupt:// eg. int n
						break;

					case FlowControl.IndirectBranch:// eg. jmp reg/mem
					case FlowControl.ConditionalBranch:// eg. je, jno, etc
					case FlowControl.Return:// eg. ret
					case FlowControl.Call:// eg. call method
					case FlowControl.IndirectCall:// eg. call reg/mem
					case FlowControl.XbeginXabortXend:
					case FlowControl.Exception:// eg. ud0
					default:
						throw new Exception("Not supported by this simple example - " + instr.FlowControl);
				}
			}
			if (totalBytes < minimumTrampolineLength)
				throw new Exception("Not enough bytes!");

			if (origInstructions.Count == 0)
				throw new Exception("Not enough instructions!");


			ref readonly var lastInstr = ref origInstructions[origInstructions.Count - 1];

			if (lastInstr.FlowControl != FlowControl.Return)
			{
				Instruction detourInstruction;

				if (arch == Architecture.X64)
				{
					detourInstruction = Instruction.CreateBranch(Code.Jmp_rel32_64, lastInstr.NextIP);
				}
				else
				{
					detourInstruction = Instruction.CreateBranch(Code.Jmp_rel32_32, lastInstr.NextIP);
				}

				origInstructions.Add(detourInstruction);
			}


			// Generate trampoline from instruction list

			var codeWriter = new CodeWriterImpl();
			ulong relocatedBaseAddress = (ulong)trampolinePtr;
			var block = new InstructionBlock(codeWriter, origInstructions, relocatedBaseAddress);

			bool success = BlockEncoder.TryEncode(decoder.Bitness, block, out var errorMessage, out var result);

			if (!success)
			{
				throw new Exception(errorMessage);
			}

			// Write generated trampoline

			var newCode = codeWriter.ToArray();
			Marshal.Copy(newCode, 0, trampolinePtr, newCode.Length);


			jmpLength = newCode.Length - (int)totalBytes;
			trampolineLength = newCode.Length;
		}

		public static byte[] GenerateAbsoluteJump(IntPtr targetAddress, IntPtr currentAddress, Architecture arch)
		{
			byte[] jmpBytes;

			if (arch == Architecture.X64)
			{
				jmpBytes = new byte[]
				{
					0xFF, 0x25, 0x00, 0x00, 0x00, 0x00,				// FF25 00000000: JMP [RIP+6]
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00	// Absolute destination address
				};

				Array.Copy(BitConverter.GetBytes(targetAddress.ToInt64()), 0, jmpBytes, 6, 8);
			}
			else
			{
				jmpBytes = new byte[]
				{
					0xE9,					// E9: JMP rel destination
					0x00, 0x00, 0x00, 0x00	// Relative destination address
				};

				Array.Copy(BitConverter.GetBytes(targetAddress.ToInt32() - (currentAddress.ToInt32() + 5)), 0, jmpBytes, 1, 4);
			}

			return jmpBytes;
		}


		private sealed class CodeWriterImpl : CodeWriter
		{
			readonly List<byte> allBytes = new List<byte>();
			public override void WriteByte(byte value) => allBytes.Add(value);
			public byte[] ToArray() => allBytes.ToArray();
		}
	}
}