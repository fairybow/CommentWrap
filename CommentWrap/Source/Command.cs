﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.Linq;

namespace CommentWrap
{
	[Command(PackageIds.MyCommand)]
	internal sealed class Command : BaseCommand<Command>
	{
		private readonly Extractor Extractor = new();
		private readonly Lexer Lexer = new();
		private readonly Formatter Formatter = new();

		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			// Get the active document
			var docView = await VS.Documents.GetActiveDocumentViewAsync();

			if (docView?.TextView == null)
			{
				//await VS.MessageBox.ShowWarningAsync("CommentWrap", "No active document found!");
				return;
			}

			// Get the text buffer and cursor position
			var textView = docView.TextView;
			var snapshot = textView.TextBuffer.CurrentSnapshot;
			var cursorPosition = textView.Caret.Position.BufferPosition;

			// Find the comment block at cursor position
			var rawBlock = Extractor.GetRawBlock(snapshot, cursorPosition);

			if (rawBlock == null)
			{
				//await VS.MessageBox.ShowWarningAsync("CommentWrap", "No comment block found at cursor position!");
				return;
			}

			if (rawBlock.Type == Extractor.RawBlock.BlockType.NoBlock)
			{
				//await VS.MessageBox.ShowWarningAsync("CommentWrap", "No block!");
				return;
			}

			if (rawBlock.Type == Extractor.RawBlock.BlockType.Malformed)
			{
				//await VS.MessageBox.ShowWarningAsync("CommentWrap", "Comment block sucks!");
				return;
			}

			var tokens = Lexer.Tokenize(rawBlock.Lines);
			var formatted = Formatter.Format(tokens, rawBlock.Type, rawBlock.BaseIndentation.Length);

			// Test display
			//await VS.MessageBox.ShowWarningAsync("CommentWrap", Lexer.ToDebugString(tokens));
			//await VS.MessageBox.ShowWarningAsync("CommentWrap", string.Join("\n", formatted));
			//File.WriteAllText("../output.txt", string.Join("\n", formatted));

			// Replace the original comment block with the formatted version
			ReplaceCommentBlock(textView, rawBlock, formatted);
		}

		private void ReplaceCommentBlock(ITextView textView, Extractor.RawBlock rawBlock, List<string> formattedLines)
		{
			var textBuffer = textView.TextBuffer;
			var snapshot = textBuffer.CurrentSnapshot;

			// Apply base indentation to all formatted lines
			var indentedLines = formattedLines.Select(line =>
				string.IsNullOrWhiteSpace(line) ? rawBlock.BaseIndentation.TrimEnd() + line.TrimStart()
												: rawBlock.BaseIndentation + line).ToList();

			// Get the original text for comparison
			var startLine = snapshot.GetLineFromLineNumber(rawBlock.StartLineNumber);
			var endLine = snapshot.GetLineFromLineNumber(rawBlock.EndLineNumber);
			var spanStart = startLine.Start;
			var spanEnd = endLine.EndIncludingLineBreak;
			var replaceSpan = new Span(spanStart.Position, spanEnd.Position - spanStart.Position);
			string originalText = snapshot.GetText(replaceSpan);

			// Join formatted lines with environment-appropriate line endings
			string formattedText = string.Join(Environment.NewLine, indentedLines);

			// If the original block didn't end with a line break, don't add one
			if (spanEnd != endLine.End)
			{
				formattedText += Environment.NewLine;
			}

			// Compare original and formatted text - if identical, don't make any changes
			if (originalText == formattedText) return; // No changes needed

			// Capture cursor position before replacement
			var cursorPosition = textView.Caret.Position.BufferPosition;

			// Calculate cursor position relative to block start
			int relativeLine = cursorPosition.GetContainingLine().LineNumber - rawBlock.StartLineNumber;
			int relativeColumn = cursorPosition.Position - cursorPosition.GetContainingLine().Start.Position;

			// Apply the replacement
			using var edit = textBuffer.CreateEdit();
			edit.Replace(replaceSpan, formattedText);
			var newSnapshot = edit.Apply();

			// Restore cursor position with simple clamping
			int newAbsoluteLine = Math.Max(0, Math.Min(rawBlock.StartLineNumber + relativeLine, newSnapshot.LineCount - 1));
			var targetLine = newSnapshot.GetLineFromLineNumber(newAbsoluteLine);
			int newColumn = Math.Min(relativeColumn, targetLine.Length);
			int newPosition = targetLine.Start.Position + newColumn;

			textView.Caret.MoveTo(new SnapshotPoint(newSnapshot, newPosition));
		}
	}
}
