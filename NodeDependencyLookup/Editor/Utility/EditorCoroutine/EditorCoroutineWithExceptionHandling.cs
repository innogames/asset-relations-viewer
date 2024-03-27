using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.EditorCoroutine
{
	/// <summary>
	/// EditorCoroutine which handles exceptions.
	/// If an exception happened it will stop.
	/// There is also a EditorCoroutine package from Unity,
	/// but there the coroutine will continue even though an exception has been thrown
	/// </summary>
	public class EditorCoroutineWithExceptionHandling
	{
		private IEnumerator cacheUpdateEnumerator;
		private readonly Stack<IEnumerator> coroutineStack = new Stack<IEnumerator>(64);
		private Action<Exception> onExceptionCallback;
		private bool isRunning;

		public void Start(IEnumerator enumerator, Action<Exception> onException = null)
		{
			isRunning = true;
			coroutineStack.Push(enumerator);
			onExceptionCallback = onException;
			EditorApplication.update += MoveNext;
		}

		public void Stop()
		{
			if (!isRunning)
			{
				return;
			}

			isRunning = false;
			EditorApplication.update -= MoveNext;
		}

		public bool IsRunning() => isRunning;

		private void MoveNext()
		{
			try
			{
				IterateCoroutine();
			}
			catch (Exception e)
			{
				Stop();
				EditorApplication.update -= IterateCoroutine;
				Debug.LogException(e);
				onExceptionCallback?.Invoke(e);
			}
		}

		private void IterateCoroutine()
		{
			if (coroutineStack.Count == 0)
			{
				EditorApplication.update -= IterateCoroutine;
				return;
			}

			var enumerator = coroutineStack.Peek();

			if (!enumerator.MoveNext())
			{
				coroutineStack.Pop();
			}

			if (enumerator.Current is IEnumerator childEnumerator)
			{
				coroutineStack.Push(childEnumerator);
			}
		}
	}
}
