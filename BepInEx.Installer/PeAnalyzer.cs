using System;
using System.IO;

namespace BepInEx.Installer
{
	internal static class PEAnalyzer
	{
		public enum MachineType
		{
			Native = 0,
			I386 = 0x014c,
			Itanium = 0x0200,
			x64 = 0x8664
		}

		public static MachineType GetMachineType(string fileName)
		{
			const int PE_POINTER_OFFSET = 60;
			const int MACHINE_OFFSET = 4;
			byte[] data = new byte[4096];
			using (Stream s = new FileStream(fileName, FileMode.Open, FileAccess.Read))
			{
				s.Read(data, 0, 4096);
			}

			// dos header is 64 bytes, last element, long (4 bytes) is the address of the PE header
			int PE_HEADER_ADDR = BitConverter.ToInt32(data, PE_POINTER_OFFSET);
			int machineUint = BitConverter.ToUInt16(data, PE_HEADER_ADDR + MACHINE_OFFSET);
			return (MachineType)machineUint;
		}
	}
}