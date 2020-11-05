using System;
using System.IO;
using System.Runtime.InteropServices;

namespace BepInEx.Unix
{
	internal class UnixStream : Stream
	{
		public override bool CanRead => Access == FileAccess.Read || Access == FileAccess.ReadWrite;
		public override bool CanSeek => false;
		public override bool CanWrite => Access == FileAccess.Write || Access == FileAccess.ReadWrite;
		public override long Length => throw new InvalidOperationException();

		public override long Position
		{
			get => throw new InvalidOperationException();
			set => throw new InvalidOperationException();
		}


		public FileAccess Access { get; }

		public IntPtr FileHandle { get; }

		public UnixStream(int fileDescriptor, FileAccess access)
		{
			Access = access;

			int newFd = UnixStreamHelper.dup(fileDescriptor);
			FileHandle = UnixStreamHelper.fdopen(newFd, access == FileAccess.Write ? "w" : "r");
		}


		public override void Flush()
		{
			UnixStreamHelper.fflush(FileHandle);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new InvalidOperationException();
		}

		public override void SetLength(long value)
		{
			throw new InvalidOperationException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			GCHandle gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

			var read = UnixStreamHelper.fread(new IntPtr(gcHandle.AddrOfPinnedObject().ToInt64() + offset), (IntPtr)count, (IntPtr)1, FileHandle);

			gcHandle.Free();

			return read.ToInt32();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			GCHandle gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

			UnixStreamHelper.fwrite(new IntPtr(gcHandle.AddrOfPinnedObject().ToInt64() + offset), (IntPtr)count, (IntPtr)1, FileHandle);

			gcHandle.Free();
		}

		private void ReleaseUnmanagedResources()
		{
			UnixStreamHelper.fclose(FileHandle);
		}

		protected override void Dispose(bool disposing)
		{
			ReleaseUnmanagedResources();
			base.Dispose(disposing);
		}

		~UnixStream()
		{
			Dispose(false);
		}
	}
}