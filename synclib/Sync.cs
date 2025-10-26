using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Johnshope.SyncLib;

public static class Sync
{
	public static SyncJob Add(string src, string dest)
	{
		var job = new SyncJob();
		job.Mode = CopyMode.Add;
		job.Directory(new Uri(src), new Uri(dest));
		return job;
	}

	public static SyncJob Update(string src, string dest)
	{
		var job = new SyncJob();
		job.Mode = CopyMode.Update;
		job.Directory(new Uri(src), new Uri(dest));
		return job;
	}

	public static SyncJob Clone(string src, string dest)
	{
		var job = new SyncJob();
		job.Mode = CopyMode.Clone;
		job.Directory(new Uri(src), new Uri(dest));
		return job;
	}
}
