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

			// TODO: set this per arch, and implement x86 E9 jmp
			const uint requiredBytes = 10 + 2;

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

			origInstructions.Add(Instruction.CreateBranch(Code.Jmp_rel32_64, lastInstr.NextIP));


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

			var jmpCode = new byte[requiredBytes];
			jmpCode[0] = 0x48;// \ 'MOV RAX,imm64'
			jmpCode[1] = 0xB8;// /
			ulong v = (ulong)patchedFunctionPtr.ToInt64();
			for (int i = 0; i < 8; i++, v >>= 8)
				jmpCode[2 + i] = (byte)v;
			jmpCode[10] = 0xFF;// \ JMP RAX
			jmpCode[11] = 0xE0;// /

			Marshal.Copy(jmpCode, 0, originalFunctionPtr, (int)requiredBytes);

			return newCode.Length;
		}


		private sealed class CodeWriterImpl : CodeWriter
		{
			readonly List<byte> allBytes = new List<byte>();
			public override void WriteByte(byte value) => allBytes.Add(value);
			public byte[] ToArray() => allBytes.ToArray();
		}
	}
}