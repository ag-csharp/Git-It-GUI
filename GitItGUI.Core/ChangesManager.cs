﻿using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GitItGUI.Core
{
	public enum FileStates
	{
		ModifiedInWorkdir,
		ModifiedInIndex,
		NewInWorkdir,
		NewInIndex,
		DeletedFromWorkdir,
		DeletedFromIndex,
		RenamedInWorkdir,
		RenamedInIndex,
		TypeChangeInWorkdir,
		TypeChangeInIndex,
		Conflicted
	}

	public class FileState
	{
		public string filename;
		public FileStates state;

		public FileState(string filename, FileStates state)
		{
			this.filename = filename;
			this.state = state;
		}

		public bool IsStaged()
		{
			switch (state)
			{
				case FileStates.NewInIndex:
				case FileStates.DeletedFromIndex:
				case FileStates.ModifiedInIndex:
				case FileStates.RenamedInIndex:
				case FileStates.TypeChangeInIndex:
					return true;

				
				case FileStates.NewInWorkdir:
				case FileStates.DeletedFromWorkdir:
				case FileStates.ModifiedInWorkdir:
				case FileStates.RenamedInWorkdir:
				case FileStates.TypeChangeInWorkdir:
				case FileStates.Conflicted:
					return false;
			}

			throw new Exception("Unsuported state: " + state);
		}
	}

	public enum MergeBinaryFileResults
	{
		Error,
		Cancel,
		UseTheirs,
		KeepMine,
		RunMergeTool
	}

	public enum MergeFileAcceptedResults
	{
		Yes,
		No
	}

	public enum SyncMergeResults
	{
		Succeeded,
		Conflicts,
		Error
	}

	public static class ChangesManager
	{
		public delegate bool AskUserToResolveConflictedFileCallbackMethod(FileState fileState, bool isBinaryFile, out MergeBinaryFileResults result);
		public static event AskUserToResolveConflictedFileCallbackMethod AskUserToResolveConflictedFileCallback;

		public delegate bool AskUserIfTheyAcceptMergedFileCallbackMethod(FileState fileState, out MergeFileAcceptedResults result);
		public static event AskUserIfTheyAcceptMergedFileCallbackMethod AskUserIfTheyAcceptMergedFileCallback;

		private static List<FileState> fileStates;
		public static bool changesExist {get; private set;}
		public static bool changesStaged {get; private set;}

		private static bool isSyncMode;

		public static FileState[] GetFileChanges()
		{
			return fileStates.ToArray();
		}

		private static bool FileStateExists(string filename)
		{
			return fileStates.Exists(x => x.filename == filename);
		}

		internal static bool Refresh()
		{
			try
			{
				changesExist = false;
				changesStaged = false;
				fileStates = new List<FileState>();
				bool changesFound = false;
				var repoStatus = RepoManager.repo.RetrieveStatus();
				foreach (var fileStatus in repoStatus)
				{
					if (fileStatus.FilePath == Settings.repoUserSettingsFilename) continue;

					changesFound = true;
					bool stateHandled = false;
					var state = fileStatus.State;
					if ((state & FileStatus.ModifiedInWorkdir) != 0)
					{
						if (!FileStateExists(fileStatus.FilePath)) fileStates.Add(new FileState(fileStatus.FilePath, FileStates.ModifiedInWorkdir));
						stateHandled = true;
					}

					if ((state & FileStatus.ModifiedInIndex) != 0)
					{
						if (!FileStateExists(fileStatus.FilePath)) fileStates.Add(new FileState(fileStatus.FilePath, FileStates.ModifiedInIndex));
						stateHandled = true;
						changesStaged = true;
					}

					if ((state & FileStatus.NewInWorkdir) != 0)
					{
						if (!FileStateExists(fileStatus.FilePath)) fileStates.Add(new FileState(fileStatus.FilePath, FileStates.NewInWorkdir));
						stateHandled = true;
					}

					if ((state & FileStatus.NewInIndex) != 0)
					{
						if (!FileStateExists(fileStatus.FilePath)) fileStates.Add(new FileState(fileStatus.FilePath, FileStates.NewInIndex));
						stateHandled = true;
						changesStaged = true;
					}

					if ((state & FileStatus.DeletedFromWorkdir) != 0)
					{
						if (!FileStateExists(fileStatus.FilePath)) fileStates.Add(new FileState(fileStatus.FilePath, FileStates.DeletedFromWorkdir));
						stateHandled = true;
					}

					if ((state & FileStatus.DeletedFromIndex) != 0)
					{
						if (!FileStateExists(fileStatus.FilePath)) fileStates.Add(new FileState(fileStatus.FilePath, FileStates.DeletedFromIndex));
						stateHandled = true;
						changesStaged = true;
					}

					if ((state & FileStatus.RenamedInWorkdir) != 0)
					{
						if (!FileStateExists(fileStatus.FilePath)) fileStates.Add(new FileState(fileStatus.FilePath, FileStates.RenamedInWorkdir));
						stateHandled = true;
					}

					if ((state & FileStatus.RenamedInIndex) != 0)
					{
						if (!FileStateExists(fileStatus.FilePath)) fileStates.Add(new FileState(fileStatus.FilePath, FileStates.RenamedInIndex));
						stateHandled = true;
						changesStaged = true;
					}

					if ((state & FileStatus.TypeChangeInWorkdir) != 0)
					{
						if (!FileStateExists(fileStatus.FilePath)) fileStates.Add(new FileState(fileStatus.FilePath, FileStates.TypeChangeInWorkdir));
						stateHandled = true;
					}

					if ((state & FileStatus.TypeChangeInIndex) != 0)
					{
						if (!FileStateExists(fileStatus.FilePath)) fileStates.Add(new FileState(fileStatus.FilePath, FileStates.TypeChangeInIndex));
						stateHandled = true;
						changesStaged = true;
					}

					if ((state & FileStatus.Conflicted) != 0)
					{
						if (!FileStateExists(fileStatus.FilePath)) fileStates.Add(new FileState(fileStatus.FilePath, FileStates.Conflicted));
						stateHandled = true;
					}

					if ((state & FileStatus.Ignored) != 0)
					{
						stateHandled = true;
					}

					if ((state & FileStatus.Unreadable) != 0)
					{
						string fullpath = RepoManager.repoPath + Path.DirectorySeparatorChar + fileStatus.FilePath;
						if (File.Exists(fullpath))
						{
							// disable readonly if this is the cause
							var attributes = File.GetAttributes(fullpath);
							if ((attributes & FileAttributes.ReadOnly) != 0) File.SetAttributes(fullpath, FileAttributes.Normal);
							else
							{
								Debug.LogError("Problem will file read (please fix and refresh)\nCause: " + fileStatus.FilePath);
								continue;
							}

							// check to make sure file is now readable
							attributes = File.GetAttributes(fullpath);
							if ((attributes & FileAttributes.ReadOnly) != 0) Debug.LogError("File is not readable (you will need to fix the issue and refresh\nCause: " + fileStatus.FilePath);
							else Debug.LogError("Problem will file read (please fix and refresh)\nCause: " + fileStatus.FilePath);
						}
						else
						{
							Debug.LogError("Expected file doesn't exist: " + fileStatus.FilePath);
						}

						stateHandled = true;
					}

					if (!stateHandled)
					{
						Debug.LogError("Unsuported File State: " + state);
					}
				}

				if (!changesFound) Debug.Log("No Changes, now do some stuff!");
				else changesExist = true;
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to update file status: " + e.Message, true);
				return false;
			}
		}

		public static FileState[] GetFileStatuses()
		{
			return fileStates.ToArray();
		}

		public static bool FilesAreStaged()
		{
			foreach (var state in fileStates)
			{
				if (state.state == FileStates.DeletedFromIndex || state.state == FileStates.ModifiedInIndex ||
					state.state == FileStates.NewInIndex || state.state == FileStates.RenamedInIndex || state.state == FileStates.TypeChangeInIndex)
				{
					return true;
				}
			}

			return false;
		}

		public static bool FilesAreUnstaged()
		{
			foreach (var state in fileStates)
			{
				if (state.state == FileStates.DeletedFromWorkdir || state.state == FileStates.ModifiedInWorkdir ||
					state.state == FileStates.NewInWorkdir|| state.state == FileStates.RenamedInWorkdir || state.state == FileStates.TypeChangeInWorkdir)
				{
					return true;
				}
			}

			return false;
		}

		public static object GetQuickViewData(FileState fileState)
		{
			try
			{
				// check if file still exists
				string fullPath = RepoManager.repoPath + Path.DirectorySeparatorChar + fileState.filename;
				if (!File.Exists(fullPath))
				{
					return "<< File Doesn't Exist >>";
				}

				// if new file just grab local data
				if (fileState.state == FileStates.NewInWorkdir || fileState.state == FileStates.NewInIndex || fileState.state == FileStates.Conflicted)
				{
					string value;
					if (!Tools.IsBinaryFileData(fullPath))
					{
						using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.None))
						using (var reader = new StreamReader(stream))
						{
							value= reader.ReadToEnd();
						}
					}
					else
					{
						value = "<< Binary File >>";
					}

					return value;
				}

				// check if binary file
				var file = RepoManager.repo.Index[fileState.filename];
				var blob = RepoManager.repo.Lookup<Blob>(file.Id);
				if (blob.IsBinary || Tools.IsBinaryFileData(fullPath))
				{
					return "<< Binary File >>";
				}

				// check for text types
				if (fileState.state == FileStates.ModifiedInWorkdir)
				{
					var patch = RepoManager.repo.Diff.Compare<Patch>(new List<string>(){fileState.filename});// use this for details about change

					string content = patch.Content;

					var match = Regex.Match(content, @"@@.*?(@@).*?\n(.*)", RegexOptions.Singleline);
					if (match.Success && match.Groups.Count == 3) content = match.Groups[2].Value.Replace("\\ No newline at end of file\n", "");

					// remove meta data stage 2
					bool search = true;
					while (search)
					{
						patch = RepoManager.repo.Diff.Compare<Patch>(new List<string>() {fileState.filename});
						match = Regex.Match(content, @"(@@.*?(@@).*?\n)", RegexOptions.Singleline);
						if (match.Success && match.Groups.Count == 3)
						{
							content = content.Replace(match.Groups[1].Value, Environment.NewLine + "<<< ----------- SECTION ----------- >>>" + Environment.NewLine);
						}
						else
						{
							search = false;
						}
					}

					return content;
				}
				else if (fileState.state == FileStates.ModifiedInIndex ||
					fileState.state == FileStates.DeletedFromWorkdir || fileState.state == FileStates.DeletedFromIndex ||
					fileState.state == FileStates.RenamedInWorkdir || fileState.state == FileStates.RenamedInIndex ||
					fileState.state == FileStates.TypeChangeInWorkdir || fileState.state == FileStates.TypeChangeInIndex)
				{
					return blob.GetContentText();
				}
				else
				{
					Debug.LogError("Unsuported FileStatus: " + fileState.filename, true);
					return null;
				}
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to refresh quick view: " + e.Message, true);
				return null;
			}
		}

		public static bool DeleteUntrackedUnstagedFile(FileState fileState, bool refresh)
		{
			try
			{
				if (fileState.state != FileStates.NewInWorkdir) return false;
				string filePath = RepoManager.repoPath + Path.DirectorySeparatorChar + fileState.filename;
				if (File.Exists(filePath)) File.Delete(filePath);
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to delete item: " + e.Message, true);
				return false;
			}

			if (refresh) RepoManager.Refresh();
			return true;
		}

		public static bool DeleteUntrackedUnstagedFiles(bool refresh)
		{
			try
			{
				foreach (var fileState in fileStates)
				{
					if (fileState.state != FileStates.NewInWorkdir) continue;
					string filePath = RepoManager.repoPath + Path.DirectorySeparatorChar + fileState.filename;
					if (File.Exists(filePath)) File.Delete(filePath);
				}
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to delete item: " + e.Message, true);
				return false;
			}

			if (refresh) RepoManager.Refresh();
			return true;
		}

		public static bool StageFile(FileState fileState, bool refresh)
		{
			try
			{
				if (!GitCommander.Repository.Stage(fileState.filename)) throw new Exception(GitCommander.Repository.lastError);
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to stage item: " + e.Message, true);
				return false;
			}

			if (refresh) RepoManager.Refresh();
			return true;
		}

		public static bool UnstageFile(FileState fileState, bool refresh)
		{
			try
			{
				if (!GitCommander.Repository.Unstage(fileState.filename)) throw new Exception(GitCommander.Repository.lastError);
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to unstage item: " + e.Message, true);
				return false;
			}

			if (refresh) RepoManager.Refresh();
			return true;
		}

		public static bool RevertAll()
		{
			try
			{
				if (!GitCommander.Repository.RevertAllChanges()) throw new Exception(GitCommander.Repository.lastError);
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to reset: " + e.Message);
				return false;
			}

			RepoManager.Refresh();
			return true;
		}

		public static bool RevertFile(FileState fileState)
		{
			if (fileState.state != FileStates.ModifiedInIndex && fileState.state != FileStates.ModifiedInWorkdir &&
				fileState.state != FileStates.DeletedFromIndex && fileState.state != FileStates.DeletedFromWorkdir)
			{
				Debug.LogError("This file is not modified or deleted", true);
				return false;
			}

			try
			{
				if (!GitCommander.Repository.RevertFile(BranchManager.activeBranchCommander.fullname, fileState.filename)) throw new Exception(GitCommander.Repository.lastError);
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to reset file: " + e.Message);
				return false;
			}

			RepoManager.Refresh();
			return true;
		}

		public static bool CommitStagedChanges(string commitMessage)
		{
			try
			{
				if (!GitCommander.Repository.Commit(commitMessage)) throw new Exception(GitCommander.Repository.lastError);
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to commit: " + e.Message);
				return false;
			}

			RepoManager.Refresh();
			return true;
		}

		public static SyncMergeResults Pull(StatusUpdateCallbackMethod statusCallback)
		{
			var result = SyncMergeResults.Error;

			try
			{
				if (!BranchManager.IsTracking())
				{
					Debug.LogWarning("Branch is not tracking a remote!", true);
					return SyncMergeResults.Error;
				}

				// check for git settings file not in repo history
				RepoManager.DeleteRepoSettingsIfUnCommit();
				
				// pull changes
				void stdCallback(string line)
				{
					if (statusCallback != null) statusCallback(line);
				}

				void stdErrorCallback(string line)
				{
					if (statusCallback != null) statusCallback(line);
				}

				result = GitCommander.Repository.Pull(stdCallback, stdErrorCallback) ? SyncMergeResults.Succeeded : SyncMergeResults.Error;
				result = ConflictsExist() ? SyncMergeResults.Conflicts : result;

				if (result == SyncMergeResults.Conflicts) Debug.LogWarning("Merge failed, conflicts exist (please resolve)", true);
				else if (result == SyncMergeResults.Succeeded) Debug.Log("Pull Succeeded!", !isSyncMode);
				else Debug.Log("Pull Error!", !isSyncMode);
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to pull: " + e.Message, true);
				Filters.GitLFS.statusCallback = null;
				return SyncMergeResults.Error;
			}

			Filters.GitLFS.statusCallback = null;
			if (!isSyncMode) RepoManager.Refresh();
			return result;
		}

		public static bool Push(StatusUpdateCallbackMethod statusCallback)
		{
			try
			{
				if (!BranchManager.IsTracking())
				{
					Debug.LogWarning("Branch is not tracking a remote!", true);
					return false;
				}

				void stdCallback(string line)
				{
					if (statusCallback != null) statusCallback(line);
				}

				if (GitCommander.Repository.Push(stdCallback, stdCallback)) Debug.Log("Push Succeeded!", !isSyncMode);
				else throw new Exception(GitCommander.Repository.lastError);
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to push: " + e.Message, true);
				Filters.GitLFS.statusCallback = null;
				return false;
			}

			Filters.GitLFS.statusCallback = null;
			if (!isSyncMode) RepoManager.Refresh();
			return true;
		}

		public static SyncMergeResults Sync(StatusUpdateCallbackMethod statusCallback)
		{
			if (statusCallback != null) statusCallback("Syncing Started...");
			isSyncMode = true;
			var result = Pull(statusCallback);
			bool pushPass = false;
			if (result == SyncMergeResults.Succeeded) pushPass = Push(statusCallback);
			isSyncMode = false;
			
			if (result != SyncMergeResults.Succeeded || !pushPass)
			{
				Debug.LogError("Failed to Sync changes", true);
				return result;
			}
			else
			{
				Debug.Log("Sync succeeded!", true);
			}
			
			RepoManager.Refresh();
			return result;
		}

		public static bool ConflictsExist()
		{
			try
			{
				if (!GitCommander.Repository.GetConflitedFiles(out var states)) throw new Exception(GitCommander.Repository.lastError);
				return states.Length != 0;
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to get file conflicts: " + e.Message);
				return false;
			}
		}

		public static bool ResolveAllConflicts(bool refresh = true)
		{
			foreach (var fileState in fileStates)
			{
				if (fileState.state == FileStates.Conflicted && !ResolveConflict(fileState, false))
				{
					Debug.LogError("Resolve conflict failed (aborting pending)", true);
					if (refresh) RepoManager.Refresh();
					return false;
				}
			}

			if (refresh) RepoManager.Refresh();
			return true;
		}
		
		public static bool ResolveConflict(FileState fileState, bool refresh)
		{
			bool wasModified = false;
			string fullPath = RepoManager.repoPath + Path.DirectorySeparatorChar + fileState.filename;
			string fullPathBase = fullPath+".base", fullPathOurs = null, fullPathTheirs = null;
			void DeleteTempMergeFiles()
			{
				if (File.Exists(fullPathBase)) File.Delete(fullPathBase);
				if (File.Exists(fullPathOurs)) File.Delete(fullPathOurs);
				if (File.Exists(fullPathTheirs)) File.Delete(fullPathTheirs);
			}

			try
			{
				// make sure file needs to be resolved
				if (fileState.state != FileStates.Conflicted)
				{
					Debug.LogError("File not in conflicted state: " + fileState.filename, true);
					return false;
				}

				// save local temp files
				if (!GitCommander.Repository.SaveConflictedFile(fileState.filename, GitCommander.FileConflictSources.Ours, out fullPathOurs)) throw new Exception(GitCommander.Repository.lastError);
				if (!GitCommander.Repository.SaveConflictedFile(fileState.filename, GitCommander.FileConflictSources.Theirs, out fullPathTheirs)) throw new Exception(GitCommander.Repository.lastError);
				fullPathOurs = RepoManager.repoPath + Path.DirectorySeparatorChar + fullPathOurs;
				fullPathTheirs = RepoManager.repoPath + Path.DirectorySeparatorChar + fullPathTheirs;

				// check if files are binary (if so open select binary file tool)
				if (Tools.IsBinaryFileData(fullPathOurs) || Tools.IsBinaryFileData(fullPathTheirs))
				{
					// open merge tool
					MergeBinaryFileResults mergeBinaryResult;
					if (AskUserToResolveConflictedFileCallback != null && AskUserToResolveConflictedFileCallback(fileState, true, out mergeBinaryResult))
					{
						switch (mergeBinaryResult)
						{
							case MergeBinaryFileResults.Error: Debug.LogWarning("Error trying to resolve file: " + fileState.filename, true);
								DeleteTempMergeFiles();
								return false;

							case MergeBinaryFileResults.Cancel:
								DeleteTempMergeFiles();
								return false;

							case MergeBinaryFileResults.KeepMine: File.Copy(fullPathOurs, fullPath, true); break;
							case MergeBinaryFileResults.UseTheirs: File.Copy(fullPathTheirs, fullPath, true); break;
							default: Debug.LogWarning("Unsuported Response: " + mergeBinaryResult, true);
								DeleteTempMergeFiles();
								return false;
						}
					}
					else
					{
						Debug.LogError("Failed to resolve file: " + fileState.filename, true);
						return false;
					}

					// delete temp files
					DeleteTempMergeFiles();

					// stage and finish
					if (!GitCommander.Repository.Stage(fileState.filename)) throw new Exception(GitCommander.Repository.lastError);
					if (refresh) RepoManager.Refresh();
					return true;
				}
			
				// copy base and parse
				File.Copy(fullPath, fullPathBase, true);
				string baseFile = File.ReadAllText(fullPath);
				var match = Regex.Match(baseFile, @"(<<<<<<<\s*\w*[\r\n]*).*(=======[\r\n]*).*(>>>>>>>\s*\w*[\r\n]*)", RegexOptions.Singleline);
				if (match.Success && match.Groups.Count == 4)
				{
					baseFile = baseFile.Replace(match.Groups[1].Value, "").Replace(match.Groups[2].Value, "").Replace(match.Groups[3].Value, "");
					File.WriteAllText(fullPathBase, baseFile);
				}

				// hash base file
				byte[] baseHash = null;
				using (var md5 = MD5.Create())
				{
					using (var stream = File.OpenRead(fullPathBase))
					{
						baseHash = md5.ComputeHash(stream);
					}
				}

				// start external merge tool
				MergeBinaryFileResults mergeFileResult;
				if (AskUserToResolveConflictedFileCallback != null && AskUserToResolveConflictedFileCallback(fileState, false, out mergeFileResult))
				{
					switch (mergeFileResult)
					{
						case MergeBinaryFileResults.Error: Debug.LogWarning("Error trying to resolve file: " + fileState.filename, true);
							DeleteTempMergeFiles();
							return false;

						case MergeBinaryFileResults.Cancel:
							DeleteTempMergeFiles();
							return false;

						case MergeBinaryFileResults.KeepMine: File.Copy(fullPathOurs, fullPathBase, true); break;
						case MergeBinaryFileResults.UseTheirs: File.Copy(fullPathTheirs, fullPathBase, true); break;

						case MergeBinaryFileResults.RunMergeTool:
							using (var process = new Process())
							{
								process.StartInfo.FileName = AppManager.mergeToolPath;
								if (AppManager.mergeDiffTool == MergeDiffTools.Meld) process.StartInfo.Arguments = string.Format("\"{0}\" \"{1}\" \"{2}\"", fullPathOurs, fullPathBase, fullPathTheirs);
								else if (AppManager.mergeDiffTool == MergeDiffTools.kDiff3) process.StartInfo.Arguments = string.Format("\"{0}\" \"{1}\" \"{2}\"", fullPathOurs, fullPathBase, fullPathTheirs);
								else if (AppManager.mergeDiffTool == MergeDiffTools.P4Merge) process.StartInfo.Arguments = string.Format("\"{0}\" \"{1}\" \"{2}\" \"{0}\"", fullPathBase, fullPathOurs, fullPathTheirs);
								else if (AppManager.mergeDiffTool == MergeDiffTools.DiffMerge) process.StartInfo.Arguments = string.Format("\"{0}\" \"{1}\" \"{2}\"", fullPathOurs, fullPathBase, fullPathTheirs);
								process.StartInfo.WindowStyle = ProcessWindowStyle.Maximized;
								if (!process.Start())
								{
									Debug.LogError("Failed to start Merge tool (is it installed?)", true);
									DeleteTempMergeFiles();
									return false;
								}

								process.WaitForExit();
							}
							break;

						default: Debug.LogWarning("Unsuported Response: " + mergeFileResult, true);
							DeleteTempMergeFiles();
							return false;
					}
				}
				else
				{
					Debug.LogError("Failed to resolve file: " + fileState.filename, true);
					DeleteTempMergeFiles();
					return false;
				}

				// get new base hash
				byte[] baseHashChange = null;
				using (var md5 = MD5.Create())
				{
					using (var stream = File.OpenRead(fullPathBase))
					{
						baseHashChange = md5.ComputeHash(stream);
					}
				}

				// check if file was modified
				if (!baseHashChange.SequenceEqual(baseHash))
				{
					wasModified = true;
					File.Copy(fullPathBase, fullPath, true);
					if (!GitCommander.Repository.Stage(fileState.filename)) throw new Exception(GitCommander.Repository.lastError);
				}

				// check if user accepts the current state of the merge
				if (!wasModified)
				{
					MergeFileAcceptedResults result;
					if (AskUserIfTheyAcceptMergedFileCallback != null && AskUserIfTheyAcceptMergedFileCallback(fileState, out result))
					{
						switch (result)
						{
							case MergeFileAcceptedResults.Yes:
								File.Copy(fullPathBase, fullPath, true);
								if (!GitCommander.Repository.Stage(fileState.filename)) throw new Exception(GitCommander.Repository.lastError);
								wasModified = true;
								break;

							case MergeFileAcceptedResults.No:
								break;

							default: Debug.LogWarning("Unsuported Response: " + result, true); return false;
						}
					}
					else
					{
						Debug.LogError("Failed to ask user if file was resolved: " + fileState.filename, true);
						DeleteTempMergeFiles();
						return false;
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogError("Failed to resolve file: " + e.Message, true);
				DeleteTempMergeFiles();
				return false;
			}
			
			// finish
			DeleteTempMergeFiles();
			if (refresh) RepoManager.Refresh();
			return wasModified;
		}

		public static bool OpenDiffTool(FileState fileState)
		{
			string fullPath = RepoManager.repoPath + Path.DirectorySeparatorChar + fileState.filename;
			string fullPathOrig = null;
			void DeleteTempDiffFiles()
			{
				if (File.Exists(fullPathOrig)) File.Delete(fullPathOrig);
			}

			try
			{
				// get selected item
				if (fileState.state != FileStates.ModifiedInIndex && fileState.state != FileStates.ModifiedInWorkdir)
				{
					Debug.LogError("This file is not modified", true);
					return false;
				}

				// get info and save orig file
				if (!GitCommander.Repository.SaveOriginalFile(fileState.filename, out fullPathOrig)) throw new Exception(GitCommander.Repository.lastError);
				fullPathOrig = RepoManager.repoPath + Path.DirectorySeparatorChar + fullPathOrig;

				// open diff tool
				using (var process = new Process())
				{
					process.StartInfo.FileName = AppManager.mergeToolPath;
					process.StartInfo.Arguments = string.Format("\"{0}\" \"{1}\"", fullPathOrig, fullPath);
					process.StartInfo.WindowStyle = ProcessWindowStyle.Maximized;
					if (!process.Start())
					{
						Debug.LogError("Failed to start Diff tool (is it installed?)", true);
						DeleteTempDiffFiles();
						return false;
					}

					process.WaitForExit();
				}
			}
			catch (Exception ex)
			{
				Debug.LogError("Failed to start Diff tool: " + ex.Message, true);
				DeleteTempDiffFiles();
				return false;
			}

			// finish
			DeleteTempDiffFiles();
			return true;
		}
	}
}
