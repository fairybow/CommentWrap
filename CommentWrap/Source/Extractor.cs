using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;

namespace CommentWrap
{
	internal class Extractor
	{
		public class RawBlock
		{
			public enum BlockType { Malformed = 0, NoBlock, Flush, Spaced }
			public readonly BlockType Type;

			public List<string> Lines { get; set; }
			public int StartLineNumber { get; set; }
			public int EndLineNumber { get; set; }
			public string BaseIndentation { get; set; }

			public RawBlock(List<string> lines, int startLineNumber, int endLineNumber, string baseIndentation)
			{
				Lines = lines;
				StartLineNumber = startLineNumber;
				EndLineNumber = endLineNumber;
				BaseIndentation = baseIndentation;
				Type = GetBlockType(); // Initialize after Lines is set
			}

			#region Private

			private BlockType GetBlockType()
			{
				// The subsequent lines follow the pattern set by the first line (not including any frame)

				if (Lines.Count > 0)
				{
					var trimmed = Lines.First().TrimStart();

					if (trimmed.StartsWith("// ="))
					{
						if (Lines.Count < 2) return	BlockType.Malformed;

						var trimmed_2 = Lines.ElementAt(1).TrimStart();
						if (trimmed_2.StartsWith("// ")) return BlockType.Spaced;
						if (trimmed_2.StartsWith("//")) return BlockType.Flush;
					}

					if (trimmed.StartsWith("//="))
					{
						if (Lines.Count < 2) return BlockType.Malformed;

						var trimmed_2 = Lines.ElementAt(1).TrimStart();
						if (trimmed_2.StartsWith("// ")) return BlockType.Spaced;
						if (trimmed_2.StartsWith("//")) return BlockType.Flush;
					}

					if (trimmed.StartsWith("// ")) return BlockType.Spaced;
					if (trimmed.StartsWith("//")) return BlockType.Flush;
				}

				return BlockType.NoBlock;
			}

			#endregion Private
		}

		public RawBlock GetRawBlock(ITextSnapshot snapshot, SnapshotPoint position)
		{
			var currentLine = snapshot.GetLineFromPosition(position);
			var currentLineNumber = currentLine.LineNumber;

			if (!IsCommentLine(currentLine.GetText())) return null;

			// Find the start of the comment block (scan upward)
			int startLineNumber = currentLineNumber;

			while (startLineNumber > 0)
			{
				var prevLine = snapshot.GetLineFromLineNumber(startLineNumber - 1);
				if (!IsCommentLine(prevLine.GetText())) break;
				startLineNumber--;
			}

			// Find the end of the comment block (scan downward)
			int endLineNumber = currentLineNumber;

			while (endLineNumber < snapshot.LineCount - 1)
			{
				var nextLine = snapshot.GetLineFromLineNumber(endLineNumber + 1);
				if (!IsCommentLine(nextLine.GetText())) break;
				endLineNumber++;
			}

			// Extract all lines in the comment block and detect base indentation
			var commentLines = new List<string>();
			string baseIndentation = "";

			for (int i = startLineNumber; i <= endLineNumber; i++)
			{
				var line = snapshot.GetLineFromLineNumber(i);
				string lineText = line.GetText();
				commentLines.Add(lineText);

				// Detect base indentation from the first non-empty comment line
				if (string.IsNullOrEmpty(baseIndentation) && !string.IsNullOrWhiteSpace(lineText))
				{
					baseIndentation = GetIndentationFromLine(lineText);
				}
			}

			return new RawBlock(commentLines, startLineNumber, endLineNumber, baseIndentation);
		}

		#region Private

		private bool IsCommentLine(string lineText)
		{
			// For now, // but not /// or /*
			var trimmed = lineText.TrimStart();
			if (string.IsNullOrWhiteSpace(trimmed)) return false;
			if (trimmed.StartsWith("//") && !trimmed.StartsWith("///")) return true;

			return false;
		}

		private string GetIndentationFromLine(string line)
		{
			int indentEnd = 0;
			while (indentEnd < line.Length && char.IsWhiteSpace(line[indentEnd]))
			{
				indentEnd++;
			}
			return line.Substring(0, indentEnd);
		}

		#endregion Private
	}
}
