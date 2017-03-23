﻿using LibGit2Sharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace GitItGUI.Core
{
	public delegate void RunExeCallbackMethod(string stdLine);

	static class Tools
	{
		public static bool IsBinaryFileData(Stream stream, bool disposeStream = false)
		{
			const int maxByteRead = 1024 * 1024 * 8;

			// if the file is to large consider a data file (8mb)
			if (stream.Length > maxByteRead)
			{
				if (disposeStream) stream.Dispose();
				return true;
			}

			// check for \0 characters and if they accure before the end of file, this is a data file
			int b = stream.ReadByte();
			while (b != -1)
			{
				if (b == 0 && stream.Position < maxByteRead)
				{
					if (disposeStream) stream.Dispose();
					return true;
				}

				b = stream.ReadByte();
			}

			if (disposeStream) stream.Dispose();
			return false;
		}

		public static bool IsBinaryFileData(string filename)
		{
			using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				return IsBinaryFileData(stream);
			}
		}

		public static bool IsGitLFSPtr(string data)
		{
			if (data.Length >= 1024) return false;
			var match = Regex.Match(data, @"version https://git-lfs.github.com/spec/v1.*oid sha256:.*size\s\n*", RegexOptions.Singleline);
			return match.Success;
		}

		public static bool IsSingleWord(string value)
		{
			foreach (char c in value)
			{
				switch (c)
				{
					case 'a':
					case 'b':
					case 'c':
					case 'd':
					case 'e':
					case 'f':
					case 'g':
					case 'h':
					case 'i':
					case 'j':
					case 'k':
					case 'l':
					case 'm':
					case 'n':
					case 'o':
					case 'p':
					case 'q':
					case 'r':
					case 's':
					case 't':
					case 'u':
					case 'v':
					case 'w':
					case 'x':
					case 'y':
					case 'z':

					case 'A':
					case 'B':
					case 'C':
					case 'D':
					case 'E':
					case 'F':
					case 'G':
					case 'H':
					case 'I':
					case 'J':
					case 'K':
					case 'L':
					case 'M':
					case 'N':
					case 'O':
					case 'P':
					case 'Q':
					case 'R':
					case 'S':
					case 'T':
					case 'U':
					case 'V':
					case 'W':
					case 'X':
					case 'Y':
					case 'Z':

					case '0':
					case '1':
					case '2':
					case '3':
					case '4':
					case '5':
					case '6':
					case '7':
					case '8':
					case '9':

					case '_':

						continue;
					default: return false;
				}
			}

			return true;
		}
	}
}
