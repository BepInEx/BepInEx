using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Iced.Intel;

namespace BepInEx.IL2CPP
{
	public static class TrampolineGenerator
	{
		public static int Generate(IntPtr originalFunctionPtr, IntPtr patchedFunctionPtr, IntPtr trampolineFunctionPtr, int bitness)
		{
			byte[] instructionBuffer = new byte[80];

			Marshal.Copy(originalFunctionPtr, instructionBuffer, 0, 80);

			// Decode original function up until we go past the needed bytes to write the jump to patchedFunctionPtr

			var generatedJmp = GenerateAbsoluteJump(patchedFunctionPtr, originalFunctionPtr, bitness == 64);


			uint requiredBytes = (uint)generatedJmp.Length;

			var codeReader = new ByteArrayCodeReader(instructionBuffer);
			var decoder = Decoder.Create(bitness, codeReader);
			decoder.IP = (ulong)originalFunctionPtr.ToInt64();

			uint totalBytes = 0;
			var origInstructions = new InstructionList();
			while (codeReader.CanReadByte)
			{
				decoder.Decode(out var instr);
				origInstructions.Add(instr);
				totalBytes += (uint)instr.Length;
				if (instr.Code == Code.INVALID)
					throw new Exception("Found garbage");
				if (totalBytes >= requiredBytes)
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
						goto default;

					case FlowControl.IndirectBranch:// eg. jmp reg/mem
					case FlowControl.ConditionalBranch:// eg. je, jno, etc
					case FlowControl.Return:// eg. ret
					case FlowControl.Call:// eg. call method
					case FlowControl.IndirectCall:// eg. call reg/mem
					case FlowControl.Interrupt:// eg. int n
					case FlowControl.XbeginXabortXend:
					case FlowControl.Exception:// eg. ud0
					default:
						throw new Exception("Not supported by this simple example - " + instr.FlowControl);
				}
			}
			if (totalBytes < requiredBytes)
				throw new Exception("Not enough bytes!");

			if (origInstructions.Count == 0)
				throw new Exception("Not enough instructions!");


			ref readonly var lastInstr = ref origInstructions[origInstructions.Count - 1];

			if (lastInstr.FlowControl != FlowControl.Return)
			{
				if (bitness == 64)
				{
					origInstructions.Add(Instruction.CreateBranch(Code.Jmp_rel32_64, lastInstr.NextIP));
				}
				else
				{
					origInstructions.Add(Instruction.CreateBranch(Code.Jmp_rel32_32, lastInstr.NextIP));
				}
			}


			// Generate trampoline from instruction list

			var codeWriter = new CodeWriterImpl();
			ulong relocatedBaseAddress = (ulong)trampolineFunctionPtr;
			var block = new InstructionBlock(codeWriter, origInstructions, relocatedBaseAddress);

			bool success = BlockEncoder.TryEncode(decoder.Bitness, block, out var errorMessage, out var result);
			if (!success)
			{
				throw new Exception(errorMessage);
			}

			// Write generated trampoline
			
			var newCode = codeWriter.ToArray();
			Marshal.Copy(newCode, 0, trampolineFunctionPtr, newCode.Length);


			// Overwrite the start of trampolineFunctionPtr with a jump to patchedFunctionPtr

			Marshal.Copy(generatedJmp, 0, originalFunctionPtr, (int)requiredBytes);

			return newCode.Length;
		}

		private static byte[] GenerateAbsoluteJump(IntPtr address, IntPtr currentAddress, bool x64)
		{
			byte[] jmpBytes;

			if (x64)
			{
				jmpBytes = new byte[]
				{
					0xFF, 0x25, 0x00, 0x00, 0x00, 0x00,				// FF25 00000000: JMP [RIP+6]
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00	// Absolute destination address
				};

				Array.Copy(BitConverter.GetBytes(address.ToInt64()), 0, jmpBytes, 6, 8);
			}
			else
			{
				jmpBytes = new byte[]
				{
					0xE9,					// E9: JMP rel destination
					0x00, 0x00, 0x00, 0x00	// Relative destination address
				};

				Array.Copy(BitConverter.GetBytes(currentAddress.ToInt32() - address.ToInt32()), 0, jmpBytes, 1, 4);
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