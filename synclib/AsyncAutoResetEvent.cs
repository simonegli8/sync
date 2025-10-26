using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class AsyncAutoResetEvent
{
	private static readonly Task _completed = Task.FromResult(true);
	private readonly Queue<TaskCompletionSource<bool>> _waits = new();
	private bool _signaled;

	public AsyncAutoResetEvent(bool initialState = false)
	{
		_signaled = initialState;
	}

	public Task WaitAsync()
	{
		lock (_waits)
		{
			if (_signaled)
			{
				_signaled = false;
				return _completed; // release immediately
			}

			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			_waits.Enqueue(tcs);
			return tcs.Task;
		}
	}

	public void Set()
	{
		TaskCompletionSource<bool>? toRelease = null;
		lock (_waits)
		{
			if (_waits.Count > 0)
				toRelease = _waits.Dequeue();
			else if (!_signaled)
				_signaled = true;
		}

		toRelease?.SetResult(true);
	}
}
