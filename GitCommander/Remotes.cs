﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitCommander
{
	public class Remote
	{
		public string name, url;
	}

	public static partial class Repository
	{
		public static bool GetRemotes(out Remote[] remotes)
		{
			var remotesList = new List<Remote>();
			void stdCallback(string line)
			{
				var remote = new Remote() {name = line};
				remotesList.Add(remote);
			}

			string error;
			lastResult = Tools.RunExe("git", "remote show", null, out error, stdCallback);
			lastError = error;

			if (!string.IsNullOrEmpty(lastError))
			{
				remotes = null;
				return false;
			}

			// get remote urls
			foreach (var remote in remotesList)
			{
				lastResult = Tools.RunExe("git", string.Format("git config --get remote.{0}.url", remote.name), null, out error);
				lastError = error;

				if (!string.IsNullOrEmpty(lastError) || !string.IsNullOrEmpty(lastResult))
				{
					remotes = null;
					return false;
				}

				remote.url = lastResult;
			}
			
			remotes = remotesList.ToArray();
			return true;
		}
	}
}
