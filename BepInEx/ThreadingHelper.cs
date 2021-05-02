using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using UnityEngine;

namespace BepInEx
{
	/// <summary>
	/// Provides methods for running code on other threads and synchronizing with the main thread.
	/// </summary>
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
			if (Chainloader.ConfigHideBepInExGOs.Value)
				go.hideFlags = HideFlags.HideAndDontSave;
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
			var result = new InvokeResult();

			object Invoke()
			{
				try
				{
					return method.DynamicInvoke(args);
				}
				catch (Exception ex)
				{
					result.ExceptionThrown = true;
					return ex;
				}
			}

			if (!InvokeRequired)
				result.Finish(Invoke(), true);
			else
				StartSyncInvoke(() => result.Finish(Invoke(), false));

			return result;
		}

		object ISynchronizeInvoke.EndInvoke(IAsyncResult result)
		{
			var invokeResult = (InvokeResult)result;
			invokeResult.AsyncWaitHandle.WaitOne();

			if (invokeResult.ExceptionThrown)
				throw (Exception)invokeResult.AsyncState;
			return invokeResult.AsyncState;
		}

		object ISynchronizeInvoke.Invoke(Delegate method, object[] args)
		{
			var invokeResult = ((ISynchronizeInvoke)this).BeginInvoke(method, args);
			return ((ISynchronizeInvoke)this).EndInvoke(invokeResult);
		}

		/// <summary>
		/// False if current code is executing on the main unity thread, otherwise True.
		/// Warning: Will return true before the first frame finishes (i.e. inside plugin Awake and Start methods).
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
			internal bool ExceptionThrown;
		}

		#endregion
	}

	/// <summary>
	/// Convenience extensions for utilizing multiple threads and using the <see cref="ThreadingHelper"/>.
	/// </summary>
	public static class ThreadingExtensions
	{
		/// <inheritdoc cref="RunParallel{TIn,TOut}(IList{TIn},Func{TIn,TOut},int)"/>
		public static IEnumerable<TOut> RunParallel<TIn, TOut>(this IEnumerable<TIn> data, Func<TIn, TOut> work, int workerCount = -1)
		{
			foreach (var result in RunParallel(data.ToList(), work))
				yield return result;
		}

		/// <summary>
		/// Apply a function to a collection of data by spreading the work on multiple threads.
		/// Outputs of the functions are returned to the current thread and yielded one by one.
		/// </summary>
		/// <typeparam name="TIn">Type of the input values.</typeparam>
		/// <typeparam name="TOut">Type of the output values.</typeparam>
		/// <param name="data">Input values for the work function.</param>
		/// <param name="work">Function to apply to the data on multiple threads at once.</param>
		/// <param name="workerCount">Number of worker threads. By default SystemInfo.processorCount is used.</param>
		/// <exception cref="TargetInvocationException">An exception was thrown inside one of the threads, and the operation was aborted.</exception>
		/// <exception cref="ArgumentException">Need at least 1 workerCount.</exception>
		public static IEnumerable<TOut> RunParallel<TIn, TOut>(this IList<TIn> data, Func<TIn, TOut> work, int workerCount = -1)
		{
			if (workerCount < 0)
				workerCount = Mathf.Max(2, Environment.ProcessorCount);
			else if (workerCount == 0)
				throw new ArgumentException("Need at least 1 worker", nameof(workerCount));

			var perThreadCount = Mathf.CeilToInt(data.Count / (float)workerCount);
			var doneCount = 0;

			var lockObj = new object();
			var are = new ManualResetEvent(false);
			IEnumerable<TOut> doneItems = null;
			Exception exceptionThrown = null;

			// Start threads to process the data
			for (var i = 0; i < workerCount; i++)
			{
				int first = i * perThreadCount;
				int last = Mathf.Min(first + perThreadCount, data.Count);
				ThreadPool.QueueUserWorkItem(
					_ =>
					{
						var results = new List<TOut>(perThreadCount);

						try
						{
							for (int dataIndex = first; dataIndex < last; dataIndex++)
							{
								if (exceptionThrown != null) break;
								results.Add(work(data[dataIndex]));
							}
						}
						catch (Exception ex)
						{
							exceptionThrown = ex;
						}

						lock (lockObj)
						{
							doneItems = doneItems == null ? results : results.Concat(doneItems);
							doneCount++;
							are.Set();
						}
					});
			}

			// Main thread waits for results and returns them until all threads finish
			while (true)
			{
				are.WaitOne();

				IEnumerable<TOut> toOutput;
				bool isDone;
				lock (lockObj)
				{
					toOutput = doneItems;
					doneItems = null;
					isDone = doneCount == workerCount;
				}

				if (toOutput != null)
				{
					foreach (var doneItem in toOutput)
						yield return doneItem;
				}

				if (isDone)
					break;
			}

			if (exceptionThrown != null)
				throw new TargetInvocationException("An exception was thrown inside one of the threads", exceptionThrown);
		}
	}
}
