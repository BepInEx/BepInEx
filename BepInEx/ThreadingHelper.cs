using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using BepInEx.Logging;
using UnityEngine;

namespace BepInEx
{
	/// <summary>
	/// Provides methods for running code on other threads and synchronizing with the main thread.
	/// </summary>
	[DefaultExecutionOrder(int.MinValue)]
	public sealed class ThreadingHelper : MonoBehaviour, ISynchronizeInvoke
	{
		private readonly object _invokeLock = new object();
		private Action _invokeList;
		private Thread _mainThread;

		/// <summary>
		/// Current instance of the helper.
		/// </summary>
		public static ThreadingHelper Instance { get; private set; }

		/// <summary>
		/// Gives methods for invoking delegates on the main unity thread, both synchronously and asynchronously.
		/// Can be used in many built-in framework types, for example <see cref="System.IO.FileSystemWatcher.SynchronizingObject"/> 
		/// and <see cref="System.Timers.Timer.SynchronizingObject"/> to make their events fire on the main unity thread.
		/// </summary>
		public static ISynchronizeInvoke SynchronizingObject => Instance;

		internal static void Initialize()
		{
			var go = new GameObject("BepInEx_ThreadingHelper");
			DontDestroyOnLoad(go);
			Instance = go.AddComponent<ThreadingHelper>();
		}

		/// <summary>
		/// Queue the delegate to be invoked on the main unity thread. Use to synchronize your threads.
		/// </summary>
		public void StartSyncInvoke(Action action)
		{
			if (action == null) throw new ArgumentNullException(nameof(action));

			lock (_invokeLock) _invokeList += action;
		}

		private void Update()
		{
			// The CurrentThread can change between Awake and later methods, it's safest to get it here.
			if (_mainThread == null)
				_mainThread = Thread.CurrentThread;

			// Safe to do outside of lock because nothing can remove callbacks, at worst we execute with 1 frame delay
			if (_invokeList == null) return;

			Action toRun;
			lock (_invokeLock)
			{
				toRun = _invokeList;
				_invokeList = null;
			}

			// Need to execute outside of the lock in case the callback itself calls Invoke we could deadlock
			// The invocation would also block any threads that call Invoke
			foreach (var action in toRun.GetInvocationList().Cast<Action>())
			{
				try
				{
					action();
				}
				catch (Exception ex)
				{
					LogInvocationException(ex);
				}
			}
		}

		/// <summary>
		/// Queue the delegate to be invoked on a background thread. Use this to run slow tasks without affecting the game.
		/// NOTE: Most of Unity API can not be accessed while running on another thread!
		/// </summary>
		/// <param name="action">
		/// Task to be executed on another thread. Can optionally return an Action that will be executed on the main thread.
		/// You can use this action to return results of your work safely. Return null if this is not needed.
		/// </param>
		public void StartAsyncInvoke(Func<Action> action)
		{
			void DoWork(object _)
			{
				try
				{
					var result = action();

					if (result != null)
						StartSyncInvoke(result);
				}
				catch (Exception ex)
				{
					LogInvocationException(ex);
				}
			}

			if (!ThreadPool.QueueUserWorkItem(DoWork))
				throw new NotSupportedException("Failed to queue the action on ThreadPool");
		}

		private static void LogInvocationException(Exception ex)
		{
			Logging.Logger.Log(LogLevel.Error, ex);
			if (ex.InnerException != null) Logging.Logger.Log(LogLevel.Error, "INNER: " + ex.InnerException);
		}

		#region ISynchronizeInvoke

		IAsyncResult ISynchronizeInvoke.BeginInvoke(Delegate method, object[] args)
		{
			object Invoke()
			{
				try { return method.DynamicInvoke(args); }
				catch (Exception ex) { return ex; }
			}

			var result = new InvokeResult();

			if (!InvokeRequired)
				result.Finish(Invoke(), true);
			else
				StartSyncInvoke(() => result.Finish(Invoke(), false));

			return result;
		}

		object ISynchronizeInvoke.EndInvoke(IAsyncResult result)
		{
			result.AsyncWaitHandle.WaitOne();

			if (result.AsyncState is Exception ex)
				throw ex;
			return result.AsyncState;
		}

		object ISynchronizeInvoke.Invoke(Delegate method, object[] args)
		{
			var invokeResult = ((ISynchronizeInvoke)this).BeginInvoke(method, args);
			return ((ISynchronizeInvoke)this).EndInvoke(invokeResult);
		}

		/// <summary>
		/// False if current code is executing on the main unity thread, otherwise True.
		/// Warning: Will return false before the first frame finishes (i.e. inside plugin Awake and Start methods).
		/// </summary>
		/// <inheritdoc />
		public bool InvokeRequired => _mainThread == null || _mainThread != Thread.CurrentThread;

		private sealed class InvokeResult : IAsyncResult
		{
			public InvokeResult()
			{
				AsyncWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
			}

			public void Finish(object result, bool completedSynchronously)
			{
				AsyncState = result;
				CompletedSynchronously = completedSynchronously;
				IsCompleted = true;
				((EventWaitHandle)AsyncWaitHandle).Set();
			}

			public bool IsCompleted { get; private set; }
			public WaitHandle AsyncWaitHandle { get; }
			public object AsyncState { get; private set; }
			public bool CompletedSynchronously { get; private set; }
		}

		#endregion
	}
}