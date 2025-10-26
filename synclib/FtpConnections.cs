using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using FluentFTP;

namespace Johnshope.SyncLib {

	public class FtpConnections {

		public SyncJob Job { get; set; }
		public Log Log  => Job.Log;

		Dictionary<string, ResourceQueue<FtpClient>> Queue = new Dictionary<string, ResourceQueue<FtpClient>>();
		Dictionary<string, int?> TimeOffsets = new Dictionary<string, int?>();
		Dictionary<string, List<FtpCapability>> Features = new Dictionary<string, List<FtpCapability>>();

		string Key(FtpClient ftp) { return ftp.Host + ":" + ftp.Port.ToString(); }
		string Key(Uri uri) { return uri.Host + ":" + uri.Port.ToString(); }

		int? Connections(Uri url) {
			var query = url.Query();
			int con;
			if (int.TryParse((query["connections"] ?? "").ToString(), out con)) return con;
			else return null;
		}

		string Proxy(Uri url) {
			var query = url.Query();
			string proxy = (query["proxy"] ?? "").ToString();
			return proxy;
		}

		TimeSpan? TimeOffset(Uri url) {
			var query = url.Query();
			int zone = 0;
			string zonestr = query["timezone"] as string;
			if (string.IsNullOrEmpty(zonestr)) return null;
			zonestr = zonestr.ToLower();
			if (zonestr == "z" || zonestr == "utc") return TimeSpan.FromHours(0);
			if (!int.TryParse(zonestr, out zone))
			{
				TimeZoneInfo timezone = null;
				try
				{
					timezone = TimeZoneInfo.FindSystemTimeZoneById(zonestr);
					return timezone.GetUtcOffset(DateTime.Now);
				}
				catch (TimeZoneNotFoundException ex) { }
				return null;
			}
			else return TimeSpan.FromHours(zone);
		}

		int clientIndex = 0;

		public string FTPTag(int n) { return "FTP" + n.ToString(); }

		public async Task<FtpClient> Open(Uri url) {
			var queue = Queue[Key(url)];
			var path = url.Path();
			var ftp = await queue.DequeueOrBlockAsync(async client => await client.GetWorkingDirectory(Job.Cancel.Token) == client.CorrectPath(path));
			try {
				if (ftp == null) {
					ftp = new FtpClient(url.Host, url.Port, ++clientIndex, Job);
					if (!string.IsNullOrEmpty(url.UserInfo))
					{
						if (url.UserInfo.Contains(':'))
						{
							var user = url.UserInfo.Split(':');
							ftp.Credentials = new NetworkCredential(user[0], user[1]);
						}
						else
						{
							ftp.Credentials = new NetworkCredential(url.UserInfo, string.Empty);
						}
					}
					else
					{
						ftp.Credentials = new NetworkCredential("Anonymous", "anonymous");
					}
					await ftp.ConnectAndInit();
					if (Job.Verbose) {
						/*ftp.ClientRequest += new EventHandler<FtpRequestEventArgs>((sender, args) => {
							lock (Log) { Log.YellowLabel(FTPTag(ftp.Index) + "> "); Log.Text(args.Request.Text); }
						});
						ftp.ServerResponse += new EventHandler<FtpResponseEventArgs>((sender, args) => {
							lock (Log) { Log.Label(FTPTag(ftp.Index) + ": "); Log.Text(args.Response.RawText); }
						});*/
					}
					if (url.Query()["passive"] != null || url.Query()["active"] == null) ftp.Config.DataConnectionType = FluentFTP.FtpDataConnectionType.AutoPassive;
					else ftp.Config.DataConnectionType = FluentFTP.FtpDataConnectionType.AutoPassive;
					ftp.Config.VerifyMethod = FluentFTP.FtpVerifyMethod.Checksum;
				
					// set encoding
					List<FtpCapability> features = new();
					if (!Features.TryGetValue(Key(url), out features))
					{
						features = ftp.Capabilities;
						Features.Add(Key(url), features);
					}
					// set encoding
					if (url.Query()["old"] == null)
					{
						if (features.Contains(FtpCapability.UTF8))
						{
							ftp.Encoding = Encoding.UTF8;
							var reply = await ftp.Execute("OPTS UTF8 ON");
						}
						else
						{
							ftp.Encoding = Encoding.ASCII;
						}
					}
					else
					{
						ftp.Encoding = Encoding.ASCII;
					}

					// get server local time offset
					var offset = TimeOffset(url);
					if (offset.HasValue) ftp.TimeOffset = offset;
					else if (!ftp.TimeOffset.HasValue)
					{
						using (var lock0 = await queue.Lock.LockAsync())
						{
							var offsetclient = queue.FirstOrDefault(client => client != null && client.TimeOffset.HasValue);
							if (offsetclient != null) ftp.TimeOffset = offsetclient.TimeOffset;
							else {
								// determine time offset
								ftp.Config.ServerTimeZone = TimeZoneInfo.Utc;
								ftp.Config.ClientTimeZone = TimeZoneInfo.Utc;
								var tmppath = Path.GetTempFileName();
								var remotepath = Path.GetDirectoryName(tmppath);
								File.Create(tmppath);
								var now = DateTime.Now;
								await ftp.UploadFile(tmppath, remotepath, token:Job.Cancel.Token);
								var time = await ftp.GetModifiedTime(remotepath, Job.Cancel.Token);
								await ftp.DeleteFile(tmppath, Job.Cancel.Token);
								ftp.TimeOffset = TimeSpan.FromMinutes(Math.Round((time - now).TotalMinutes));
							}
						}
					}
					// change path
					path = ftp.CorrectPath(url.Path());
					/*if (url.Query()["raw"] != null && ftp.IsCompressionEnabled) ftp.CompressionOff();
					if (url.Query()["zip"] != null && ftp.IsCompressionEnabled) ftp.CompressionOn();*/
					if (await ftp.GetWorkingDirectory() != path)
					{
						try
						{
							await ftp.SetWorkingDirectory(path);
						}
						catch (Exception ex)
						{
							await ftp.CreateDirectory(path);
							await ftp.SetWorkingDirectory(path);
						}
						if (await ftp.GetWorkingDirectory() != ftp.CorrectPath(url.Path()))
							throw new Exception(string.Format("Cannot change to correct path {0}.", url.Path()));
					}
				}
				else {
					if (!ftp.IsConnected) await ftp.Connect(true);
				}
				ftp.Clients++;
				if (ftp.Clients != 1) throw new Exception("FTP connection is opened by multiple clients.");
			} catch (Exception e) {
				Log.Exception(e);
			}
			if (ftp.Clients != 1 || await ftp.GetWorkingDirectory() != ftp.CorrectPath(url.Path())) 
				throw new Exception("FTP connection open postcondition failed.");
			return ftp;
		}

		public void Pass(FtpClient client) {
			if (client == null || client.Clients != 1) throw new Exception("FTP connection pass precondition failed.");
			client.Clients--;
			Queue[Key(client)].Enqueue(client);
		}

		public int Count(Uri url) { if (url.IsFile) return 1; else return Queue[Key(url)].Count; }

		public int Allocate(Uri url) { if (!url.IsFile) { Queue[Key(url)] = new ResourceQueue<FtpClient>(); var n = Connections(url) ?? 10; var i = n; while (i-- > 0) Queue[Key(url)].Enqueue(null); return n; } return 1; }

		public async Task Close() {
			foreach (var queue in Queue.Values) {
				while (queue.Count > 0) {
					var ftp = queue.Dequeue();
					if (ftp != null) {
						if (ftp.IsConnected) await ftp.Disconnect(Job.Cancel.Token);
						ftp.Dispose();
					}
				}
			}
		}
	}
}
