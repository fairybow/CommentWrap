using System.Collections.Generic;

namespace CommentWrap
{
	internal class Formatter
	{
		public List<string> Format(List<Parser.Token> tokens, Extractor.RawBlock.BlockType blockType, int baseIndentationLength)
		{
			var result = new List<string>();

			// Calculate vocative indent based on longest vocative
			int vocativeIndent = CalculateVocativeIndent(tokens);

			// Determine comment prefix and max line length
			string commentPrefix = blockType == Extractor.RawBlock.BlockType.Spaced ? "// " : "//";
			int maxLineLength = Math.Max(30, MAX_CHARS - baseIndentationLength);
			int availableTextWidth = maxLineLength - commentPrefix.Length;

			var currentLine = new List<string>();
			int currentIndent = 0;

			for (int i = 0; i < tokens.Count; i++)
			{
				var token = tokens[i];

				switch (token.Type)
				{
					case Parser.Token.TokenType.Frame:
						FinishCurrentLine(result, currentLine, commentPrefix, currentIndent);
						result.Add("//" + new string('=', maxLineLength - 2));  // -2 for "//"
						currentIndent = 0;
						break;

					case Parser.Token.TokenType.SpacedFrame:
						FinishCurrentLine(result, currentLine, commentPrefix, currentIndent);
						result.Add("// " + new string('=', maxLineLength - 3));  // -3 for "// "
						currentIndent = 0;
						break;

					case Parser.Token.TokenType.TitleFrame:
						FinishCurrentLine(result, currentLine, commentPrefix, currentIndent);
						result.Add(CreateTitleFrame(token.Text, false, maxLineLength));
						currentIndent = 0;
						break;

					case Parser.Token.TokenType.SpacedTitleFrame:
						FinishCurrentLine(result, currentLine, commentPrefix, currentIndent);
						result.Add(CreateTitleFrame(token.Text, true, maxLineLength));
						currentIndent = 0;
						break;

					case Parser.Token.TokenType.EmptyLine:
						FinishCurrentLine(result, currentLine, commentPrefix, currentIndent);
						result.Add(commentPrefix.TrimEnd());
						currentIndent = 0;
						break;

					case Parser.Token.TokenType.VocativeStart:
						FinishCurrentLine(result, currentLine, commentPrefix, currentIndent);
						// Vocative starts at beginning of line (no indent)
						string paddedVocative = (token.Text + ":").PadRight(vocativeIndent);
						currentLine.Add(paddedVocative);
						currentIndent = 0; // No indent for the vocative line itself
						break;

					case Parser.Token.TokenType.BulletStart:
						FinishCurrentLine(result, currentLine, commentPrefix, currentIndent);
						// Regular bullet: "// - word"
						currentLine.Add("- " + token.Text);
						currentIndent = 0; // No indent for the bullet line itself
						break;

					case Parser.Token.TokenType.VocativeBulletStart:
						// Check if this is the first vocative bullet on the vocative line
						bool isFirstBulletOnVocativeLine = currentLine.Count > 0 &&
							i > 0 && tokens[i - 1].Type == Parser.Token.TokenType.VocativeStart;

						if (isFirstBulletOnVocativeLine)
						{
							// First bullet continues the vocative line
							currentLine.Add("- " + token.Text);
						}
						else
						{
							// Subsequent bullets start new lines at vocative indent
							FinishCurrentLine(result, currentLine, commentPrefix, currentIndent);
							string indentSpaces = new string(' ', vocativeIndent);
							currentLine.Add(indentSpaces + "- " + token.Text);
							currentIndent = 0;
						}
						break;

					case Parser.Token.TokenType.Text:
					case Parser.Token.TokenType.VocativeText:
					case Parser.Token.TokenType.BulletText:
					case Parser.Token.TokenType.VocativeBulletText:
						AddTextToken(result, currentLine, token.Text, commentPrefix,
								   GetAppropriateIndent(token.Type, vocativeIndent),
								   availableTextWidth, ref currentIndent);
						break;

					default:
						// Skip unknown tokens
						break;
				}
			}

			// Finish any remaining line
			FinishCurrentLine(result, currentLine, commentPrefix, currentIndent);

			return result;
		}

		#region Private

		private const int MAX_CHARS = 80;

		private int GetAppropriateIndent(Parser.Token.TokenType tokenType, int vocativeIndent)
		{
			return tokenType switch
			{
				Parser.Token.TokenType.VocativeText => vocativeIndent,
				Parser.Token.TokenType.BulletText => 2,  // Align under first word of bullet
				Parser.Token.TokenType.VocativeBulletText => vocativeIndent + 2,
				_ => 0
			};
		}

		private void AddTextToken(List<string> result, List<string> currentLine, string text,
			string commentPrefix, int continuationIndent, int availableTextWidth,
			ref int currentIndent)
		{
			// Build what the line would look like if we add this word
			string currentContent = string.Join("", currentLine); // Don't add spaces here - handle manually

			// Determine if we need to add a space before this text
			bool needsSpace = currentContent.Length > 0 && !currentContent.EndsWith(" ");
			string spacer = needsSpace ? " " : "";
			string testContent = currentContent + spacer + text;

			// Check if adding this word would exceed the line limit
			if (currentIndent + testContent.Length > availableTextWidth && currentLine.Count > 0)
			{
				// Need to wrap - finish current line and start new one
				FinishCurrentLine(result, currentLine, commentPrefix, currentIndent);
				currentLine.Add(text);
				currentIndent = continuationIndent; // Use appropriate continuation indent
			}
			else
			{
				// Can fit on current line
				if (currentLine.Count > 0)
				{
					// Add space and word to last element (only if needed)
					int lastIndex = currentLine.Count - 1;
					currentLine[lastIndex] = currentLine[lastIndex] + spacer + text;
				}
				else
				{
					// First word on line
					currentLine.Add(text);
				}
			}
		}

		private int CalculateVocativeIndent(List<Parser.Token> tokens)
		{
			int maxVocativeLength = 0;

			foreach (var token in tokens)
			{
				if (token.Type == Parser.Token.TokenType.VocativeStart)
				{
					int length = token.Text.Length + 1; // +1 for colon
					if (length > maxVocativeLength)
					{
						maxVocativeLength = length;
					}
				}
			}

			return maxVocativeLength > 0 ? maxVocativeLength + 1 : 0; // +1 for space after colon
		}

		private string CreateTitleFrame(string title, bool spaced, int maxLineLength)
		{
			if (spaced)
			{
				// Format: "// ==== TITLE ===="
				string prefix = "// ==== ";
				string suffix = " ====";
				int availableForTitle = maxLineLength - prefix.Length - suffix.Length;

				if (title.Length > availableForTitle)
				{
					title = title.Substring(0, availableForTitle);
				}

				int remainingEquals = maxLineLength - prefix.Length - title.Length - 1; // -1 for space before equals
				if (remainingEquals < 4) remainingEquals = 4; // Changed from 3 to 4

				return prefix + title + " " + new string('=', remainingEquals - 1);
			}
			else
			{
				// Format: "//====TITLE===="
				string prefix = "//===="; // Changed from "//===" to "//===="
				int availableForTitle = maxLineLength - prefix.Length - 4; // Changed from -3 to -4 for closing ====

				if (title.Length > availableForTitle)
				{
					title = title.Substring(0, availableForTitle);
				}

				int remainingEquals = maxLineLength - prefix.Length - title.Length;
				if (remainingEquals < 4) remainingEquals = 4; // Changed from 3 to 4

				return prefix + title + new string('=', remainingEquals);
			}
		}

		private void FinishCurrentLine(List<string> result, List<string> currentLine,
			string commentPrefix, int currentIndent)
		{
			if (currentLine.Count > 0)
			{
				string indentSpaces = new string(' ', currentIndent);
				string lineContent = string.Join("", currentLine);
				result.Add(commentPrefix + indentSpaces + lineContent);
				currentLine.Clear();
			}
		}

		#endregion Private
	}
}
