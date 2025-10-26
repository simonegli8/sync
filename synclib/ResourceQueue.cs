using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading;
using System.Threading.Tasks;
using NeoSmart.AsyncLock;

namespace Johnshope.SyncLib {

	public class ResourceQueue<T>: System.Collections.Generic.LinkedList<T> {

		AutoResetEvent signal = new AutoResetEvent(false);
		AsyncAutoResetEvent signalAsync = new AsyncAutoResetEvent(false);
		public AsyncLock Lock = new AsyncLock();

		public void Enqueue(T entry) {
			using (var lock0 = Lock.Lock())
			{ 	
				var node = First;
				while (node != null && node.Value != null) node = node.Next;
				if (node == null) AddLast(entry);
				else AddBefore(node, entry);
			}
			signalAsync.Set();
			signal.Set();
		}

		public T Dequeue() {
			using (var lock0 = Lock.Lock())
			{
				if (base.Count > 0) {
					var entry = First;
					RemoveFirst();
					return entry.Value;
				} else return default(T);
			}
		}

		public T Dequeue(Func<T, bool> where) {
			using (var lock0 = Lock.Lock())
			{
				if (base.Count > 0) {
					var entry = First;
					while (entry != null && entry.Value != null && !where(entry.Value)) entry = entry.Next;
					if (entry == null) entry = First;
					Remove(entry);
					return entry.Value;
				} else return default(T);
			}
		}

		public async Task<T> DequeueAsync(Func<T, Task<bool>> where)
		{
			using (var lock0 = await Lock.LockAsync())
			{
				if (base.Count > 0)
				{
					var entry = First;
					while (entry != null && entry.Value != null && !(await where(entry.Value))) entry = entry.Next;
					if (entry == null) entry = First;
					Remove(entry);
					return entry.Value;
				}
				else return default(T);
			}
		}

		public event EventHandler Blocking;
		public event EventHandler Blocked;

		public T DequeueOrBlock() {
			do {
				using (var lock0 = Lock.Lock())
				{
					if (base.Count > 0) return Dequeue();
				}
				if (Blocking != null) Blocking(this, EventArgs.Empty);
				signal.WaitOne();
				if (Blocked != null) Blocked(this, EventArgs.Empty);
			} while (true);
		}
		public T DequeueOrBlock(Func<T, bool> where) {
			do {
				using (var lock0 = Lock.Lock())
				{
					if (base.Count > 0) return Dequeue(where);
				}
				if (Blocking != null) Blocking(this, EventArgs.Empty);
				signal.WaitOne();
				if (Blocked != null) Blocked(this, EventArgs.Empty);
			} while (true);
		}
		public async Task<T> DequeueOrBlockAsync()
		{
			do
			{
				using (var lock0 = await Lock.LockAsync())
				{
					if (base.Count > 0) return Dequeue();
				}
				if (Blocking != null) Blocking(this, EventArgs.Empty);
				await signalAsync.WaitAsync();
				if (Blocked != null) Blocked(this, EventArgs.Empty);
			} while (true);
		}
		public async Task<T> DequeueOrBlockAsync(Func<T, Task<bool>> where)
		{
			do
			{
				using (var lock0 = await Lock.LockAsync())
				{
					if (base.Count > 0) return await DequeueAsync(where);
				}
				if (Blocking != null) Blocking(this, EventArgs.Empty);
				await signalAsync.WaitAsync();
				if (Blocked != null) Blocked(this, EventArgs.Empty);
			} while (true);
		}

		public bool IsEmpty { get { lock (this) return base.Count == 0; } }
		public new int Count { get { lock (this) return base.Count; } }
	}
}
