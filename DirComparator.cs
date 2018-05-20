using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DirContentCompare {

	public delegate void StatusChange(DirComparatorStatus status);
	public delegate void Complete(DirComparatorResult result);

	class DirComparator {

		DirectoryInfo leftDir;
		DirectoryInfo rightDir;
		Regex ignore;
		public event StatusChange StatusChanged;
		public event Complete Completed;

		public DirComparator(DirectoryInfo leftDir, DirectoryInfo rightDir, string ignorePattern) {
			this.leftDir = leftDir;
			this.rightDir = rightDir;
			if (ignorePattern != "") {
				ignore = new Regex(ignorePattern);
			}
		}

		public async Task<DirComparatorResult> CompareAsync() {
			StatusChanged?.Invoke(new DirComparatorStatus() { Action = DirComparatorAction.Listing, Side = DirComparatorSide.LeftFolder });
			FileInfo[] left = await Task.Run(() => leftDir.GetFiles("*", SearchOption.AllDirectories));
			StatusChanged?.Invoke(new DirComparatorStatus() { Action = DirComparatorAction.Listing, Side = DirComparatorSide.RightFolder });
			FileInfo[] right = await Task.Run(() => rightDir.GetFiles("*", SearchOption.AllDirectories));

			//FILTERING
			if (ignore != null) {
				left = await Task.Run(() => Filter(left, DirComparatorSide.LeftFolder));
				right = await Task.Run(() => Filter(right, DirComparatorSide.RightFolder));
			}

			//HASH
			var lefthashtask = Task.Run(() => Hash(left, DirComparatorSide.LeftFolder));
			var righthashtask = Task.Run(() => Hash(right, DirComparatorSide.RightFolder));
			await Task.WhenAll(lefthashtask, righthashtask);

			// COMPARE

			var comparetask = await Task.Run(() => Compare(lefthashtask.Result.Files, righthashtask.Result.Files));

			var result = new DirComparatorResult() {
				Common = DirComparatorFileInfo.ConvertAllToArray(comparetask.Common.Values, DirComparatorFileInfo.FileType.Common),
				Left = DirComparatorFileInfo.ConvertAllToArray(comparetask.Left.Values, DirComparatorFileInfo.FileType.Unique),
				DuplicateLeft = lefthashtask.Result.Duplicates.ToArray(),
				Right = DirComparatorFileInfo.ConvertAllToArray(comparetask.Right.Values, DirComparatorFileInfo.FileType.Unique),
				DuplicateRight = righthashtask.Result.Duplicates.ToArray()
			};

			Completed?.Invoke(result);
			return result;
		}

		private FileInfo[] Filter(FileInfo[] input, DirComparatorSide side) {
			int totalops = input.Length;
			int currentop = 0;
			List<FileInfo> files = new List<FileInfo>();
			foreach (FileInfo file in input) {
				StatusChanged?.Invoke(new DirComparatorStatus() { Action = DirComparatorAction.Filtering, Side = side, CurentOperation = currentop, TotalOperations = totalops, CurrentFile = file });
				if (!ignore.IsMatch(file.FullName)) {
					files.Add(file);
				}
				currentop++;
			}
			return files.ToArray();
		}

		private HashResult Hash(FileInfo[] input, DirComparatorSide side) {
			MD5 md5 = MD5.Create();
			int totalops = input.Length;
			int currentop = 0;
			Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();
			List<DirComparatorDuplicateFileInfo> duplicates = new List<DirComparatorDuplicateFileInfo>();
			foreach (FileInfo file in input) {
				currentop++;
				StatusChanged?.Invoke(new DirComparatorStatus() { Side = side, Action = DirComparatorAction.Hashing, CurrentFile = file, CurentOperation = currentop, TotalOperations = totalops });
				string hash = Convert.ToBase64String(md5.ComputeHash(file.OpenRead()));
				if (files.ContainsKey(hash)) {
					duplicates.Add(new DirComparatorDuplicateFileInfo() { Type = DirComparatorFileInfo.FileType.Duplicate, File = files[hash], Duplicate = file });
				} else {
					files.Add(hash, file);
				}
			}
			return new HashResult() { Files = files, Duplicates = duplicates };
		}

		private CompareResult Compare(Dictionary<string, FileInfo> leftFiles, Dictionary<string, FileInfo> rightFiles) {
			int totalops = leftFiles.Count;
			int currentop = 0;
			Dictionary<string, FileInfo> commonFiles = new Dictionary<string, FileInfo>();
			foreach (KeyValuePair<string, FileInfo> pair in new Dictionary<string, FileInfo>(leftFiles)) {
				currentop++;
				StatusChanged?.Invoke(new DirComparatorStatus() { Side = DirComparatorSide.BothFolders, Action = DirComparatorAction.Comparing, CurrentFile = pair.Value, CurentOperation = currentop, TotalOperations = totalops });
				if (rightFiles.ContainsKey(pair.Key)) {
					commonFiles.Add(pair.Key, pair.Value);
					leftFiles.Remove(pair.Key);
					rightFiles.Remove(pair.Key);
				}
			}
			return new CompareResult() {
				Common = commonFiles,
				Left = leftFiles,
				Right = rightFiles
			};
		}

		private struct HashResult {

			public Dictionary<string, FileInfo> Files { get; set; }
			public List<DirComparatorDuplicateFileInfo> Duplicates { get; set; }
		}

		private struct CompareResult {

			public Dictionary<string, FileInfo> Common { get; set; }
			public Dictionary<string, FileInfo> Left { get; set; }
			public Dictionary<string, FileInfo> Right { get; set; }
		}
	}

	public struct DirComparatorResult {

		public DirComparatorFileInfo[] Common { get; set; }
		public DirComparatorFileInfo[] Left { get; set; }
		public DirComparatorDuplicateFileInfo[] DuplicateLeft { get; set; }
		public DirComparatorFileInfo[] Right { get; set; }
		public DirComparatorDuplicateFileInfo[] DuplicateRight { get; set; }
	}

	public class DirComparatorFileInfo {
		public enum FileType { Common, Unique, Duplicate }

		public FileInfo File { get; set; }
		public FileType Type { get; set; }

		public virtual string TypeAbbr { get { return Type.ToString().Substring(0, 1); } }
		public virtual string FileName { get { return File.Name; } }
		public virtual string FullName { get { return File.FullName; } }

		public static DirComparatorFileInfo[] ConvertAllToArray(IEnumerable<FileInfo> files, FileType type) {
			return Array.ConvertAll(files.ToArray(), file => { return new DirComparatorFileInfo() { File = file, Type = type }; });
		}
	}

	public class DirComparatorDuplicateFileInfo : DirComparatorFileInfo {

		public FileInfo Duplicate { get; set; }

		public override string FileName { get { return File.Name + " <=> " + Duplicate.Name; } }
		public override string FullName { get { return File.FullName + " <=> " + Duplicate.FullName; } }

	}

	public enum DirComparatorAction { Listing, Filtering, Hashing, Comparing }
	public enum DirComparatorSide { LeftFolder, RightFolder, BothFolders }

	public struct DirComparatorStatus {
		public DirComparatorAction Action { get; set; }
		public DirComparatorSide Side { get; set; }
		public FileInfo CurrentFile { get; set; }
		public int CurentOperation { get; set; }
		public int TotalOperations { get; set; }

		public override string ToString() {
			if (CurrentFile == null) {
				return Action.ToString() + " " + Side;
			}
			return Action.ToString() + " " + Side + " " + Math.Round((double)CurentOperation / TotalOperations * 100d) + "% " + CurentOperation + "/" + TotalOperations + " " + CurrentFile.Name;
		}
	}
}
