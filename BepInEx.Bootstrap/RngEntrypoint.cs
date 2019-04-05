using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace BepInEx.Bootstrap
{
	[ComVisible(true)]
	public class RngEntrypoint : RandomNumberGenerator
	{
		private RNGCryptoServiceProvider InternalProvider { get; }

		static RngEntrypoint()
		{
			Entrypoint.Init();
		}

		public RngEntrypoint()
			=> InternalProvider = new RNGCryptoServiceProvider();

		public RngEntrypoint(string str)
			=> InternalProvider = new RNGCryptoServiceProvider(str);

		public RngEntrypoint(byte[] rgb)
			=> InternalProvider = new RNGCryptoServiceProvider(rgb);

		public RngEntrypoint(CspParameters cspParams)
			=> InternalProvider = new RNGCryptoServiceProvider(cspParams);

		public override void GetBytes(byte[] data)
			=> InternalProvider.GetBytes(data);

		public override void GetNonZeroBytes(byte[] data)
			=> InternalProvider.GetNonZeroBytes(data);
	}
}