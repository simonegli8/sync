using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentFTP;

namespace Johnshope.SyncLib {
	public class FtpClient: AsyncFtpClient {

		public FtpClient(string host, int port, int Index, SyncJob job, FtpConfig config = null) : base(host, port, config) {
			Job = job; this.Index = Index; Clients = 0;
		}
		public async Task ConnectAndInit()
		{
			await AutoConnect(Job.Cancel.Token);
			RootDirectory = await GetWorkingDirectory(Job.Cancel.Token);
		}

		public int Index { get; set; }

		public int Clients { get; set; }
		public SyncJob Job { get; set; }
		public string RootDirectory { get; private set; }
		public TimeSpan? TimeOffset { get; set; }

		public virtual string CorrectPath(string path)
		{
			if (path.StartsWith("/"))
			{
				if (RootDirectory.EndsWith("/")) path = RootDirectory + path.Substring(1);
				else path = RootDirectory + path;
			}
			return path;
		}

	}
}
