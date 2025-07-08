using System.Collections.Generic;
using System.Linq;

namespace CommentWrap
{
	internal class Lexer
	{
		public struct Token
		{
			public enum TokenType
			{
				Unknown = 0,

				// Frames will be normalized when compiling, if present
				Frame,                  // `//=`
				SpacedFrame,            // `// =`
				TitleFrame,             // `//=Any words`
				SpacedTitleFrame,       // `// =Any words`

				// Comment block itself can be spaced or not, separate from frame
				Text,                   // Anything that is not anything else
				EmptyLine,              // Empty comment lines (just `//` or whitespace)

				VocativeStart,          // A single word at start of comment content that has a colon after it. Should self-terminate (following tokens are VocativeText)
				VocativeText,           // Should terminate only after an EmptyLine

				BulletStart,            // A word that starts with "- " at beginning of comment content. Should self-terminate (following tokens are BulletText)
				BulletText,             // Should terminate only after an EmptyLine or new BulletStart

				VocativeBulletStart,    // A word that starts with "- " while in Vocative mode. Should self-terminate (following tokens are VocativeBulletText)
				VocativeBulletText      // Should terminate only after an EmptyLine or new VocativeBulletStart/BulletStart
			}

			public TokenType Type;
			public string Text;
		}

		public List<Token> Tokenize(List<string> lines)
		{
			var result = new List<Token>();
			tokenMode = TokenMode.Normal;

			// Reset frame consistency tracking
			initialFrameType = null;
			isFramedBlock = false;

			foreach (string line in lines)
			{
				// Update mode BEFORE parsing the line
				UpdateTokenModeFromLine(line);

				var lineTokens = LexLine(line);
				result.AddRange(lineTokens);
			}

			// Post-processing: ensure frame consistency
			result = EnsureFrameConsistency(result);

			return result;
		}

		public string ToDebugString(List<Token> tokens)
		{
			var output = new List<string>();

			for (int i = 0; i < tokens.Count; i++)
			{
				var token = tokens[i];
				string valueDisplay = string.IsNullOrEmpty(token.Text) ? "(empty)" :
									 $"'{token.Text}'";

				output.Add($"{i + 1:D3}: {token.Type,-20} | {valueDisplay}");
			}

			return string.Join("\n", output);
		}

		#region Private

		private enum TokenMode
		{
			Unknown = 0,
			Normal,
			Bullet,
			Vocative,
			VocativeBullet
		}

		private TokenMode tokenMode = TokenMode.Normal;
		private Token.TokenType? initialFrameType = null; // Frame consistency tracking
		private bool isFramedBlock = false;

		private List<Token> EnsureFrameConsistency(List<Token> tokens)
		{
			if (!isFramedBlock || !initialFrameType.HasValue)
			{
				return tokens;
			}

			// Check if the last token is a frame token
			bool hasClosingFrame = tokens.Count > 0 && IsFrameToken(tokens.Last().Type);

			if (!hasClosingFrame)
			{
				// Add appropriate closing frame
				Token.TokenType closingFrameType = GetNonTitleFrameType(initialFrameType.Value);
				tokens.Add(new Token { Type = closingFrameType, Text = "" });
			}

			return tokens;
		}

		private bool IsFrameToken(Token.TokenType tokenType)
		{
			return tokenType == Token.TokenType.Frame
				|| tokenType == Token.TokenType.SpacedFrame
				|| tokenType == Token.TokenType.TitleFrame
				|| tokenType == Token.TokenType.SpacedTitleFrame;
		}

		private Token.TokenType GetNonTitleFrameType(Token.TokenType frameType)
		{
			return frameType switch
			{
				Token.TokenType.Frame or Token.TokenType.TitleFrame => Token.TokenType.Frame,
				Token.TokenType.SpacedFrame or Token.TokenType.SpacedTitleFrame => Token.TokenType.SpacedFrame,
				_ => Token.TokenType.Frame
			};
		}

		private Token.TokenType GetConsistentFrameType(Token.TokenType detectedFrameType)
		{
			if (!initialFrameType.HasValue)
			{
				// This is the first frame - set the initial type
				initialFrameType = detectedFrameType;
				isFramedBlock = true;
				return detectedFrameType;
			}

			// Determine spacing consistency from the first frame
			bool shouldBeSpaced = initialFrameType == Token.TokenType.SpacedFrame
				|| initialFrameType == Token.TokenType.SpacedTitleFrame;

			// Determine if this frame is a title frame
			bool isTitle = detectedFrameType == Token.TokenType.TitleFrame
				|| detectedFrameType == Token.TokenType.SpacedTitleFrame;

			// Apply spacing consistency while preserving title vs non-title
			return (shouldBeSpaced, isTitle) switch
			{
				(true, true) => Token.TokenType.SpacedTitleFrame,
				(true, false) => Token.TokenType.SpacedFrame,
				(false, true) => Token.TokenType.TitleFrame,
				(false, false) => Token.TokenType.Frame
			};
		}

		private List<Token> LexLine(string line)
		{
			var tokens = new List<Token>();
			string trimmed = line.TrimStart();

			// Handle completely empty lines (no content or only whitespace)
			if (string.IsNullOrWhiteSpace(trimmed))
			{
				tokens.Add(new Token { Type = Token.TokenType.EmptyLine, Text = "" });
				return tokens;
			}

			// Handle comment lines with no actual content (just // with optional
			// whitespace)
			if (trimmed.StartsWith("//"))
			{
				string content = ExtractCommentContent(trimmed);
				if (string.IsNullOrWhiteSpace(content))
				{
					tokens.Add(new Token { Type = Token.TokenType.EmptyLine, Text = "" });
					return tokens;
				}
			}

			// Must be a comment line with content at this point
			if (!trimmed.StartsWith("//"))
			{
				tokens.Add(new Token { Type = Token.TokenType.Unknown, Text = line });
				return tokens;
			}

			// Check for frames first
			if (IsFrame(trimmed))
			{
				tokens.Add(LexFrame(trimmed));
				return tokens;
			}

			// Extract comment content (everything after // and optional space)
			string commentContent = ExtractCommentContent(trimmed);

			// Parse content into words
			return LexWordsInContent(commentContent);
		}

		private List<Token> LexWordsInContent(string content)
		{
			var tokens = new List<Token>();
			content = content.TrimStart();

			if (string.IsNullOrEmpty(content))
			{
				return tokens;
			}

			// Check for vocative (word followed by colon)
			if (IsVocativeStart(content))
			{
				string vocativeWord = ExtractVocativeWord(content);
				tokens.Add(new Token { Type = Token.TokenType.VocativeStart, Text = vocativeWord });

				// Parse remaining content after colon
				string afterColon = content.Substring(content.IndexOf(':') + 1).Trim();
				if (!string.IsNullOrEmpty(afterColon))
				{
					// Check if the vocative content starts with a bullet
					if (afterColon.StartsWith("- "))
					{
						// Parse as vocative bullet
						string bulletContent = afterColon.Substring(2).Trim();
						if (!string.IsNullOrEmpty(bulletContent))
						{
							var words = bulletContent.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

							if (words.Length > 0)
							{
								// First word is the vocative bullet start
								tokens.Add(new Token { Type = Token.TokenType.VocativeBulletStart, Text = words[0] });

								// Remaining words are vocative bullet text
								for (int i = 1; i < words.Length; i++)
								{
									tokens.Add(new Token { Type = Token.TokenType.VocativeBulletText, Text = words[i] });
								}
							}
						}
					}
					else
					{
						// Regular vocative text
						tokens.AddRange(TokenizeWords(afterColon, Token.TokenType.VocativeText));
					}
				}

				return tokens;
			}

			// Check for bullet
			if (content.StartsWith("- "))
			{
				var bulletType = (tokenMode == TokenMode.Vocative || tokenMode == TokenMode.VocativeBullet) ?
					Token.TokenType.VocativeBulletStart : Token.TokenType.BulletStart;

				// Parse content after bullet dash and space
				string afterBullet = content.Substring(2).Trim();
				if (!string.IsNullOrEmpty(afterBullet))
				{
					var words = afterBullet.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

					if (words.Length > 0)
					{
						// First word is the bullet start
						tokens.Add(new Token { Type = bulletType, Text = words[0] });

						// Remaining words are bullet text
						var textType = (tokenMode == TokenMode.Vocative || tokenMode == TokenMode.VocativeBullet) ?
							Token.TokenType.VocativeBulletText : Token.TokenType.BulletText;

						for (int i = 1; i < words.Length; i++)
						{
							tokens.Add(new Token { Type = textType, Text = words[i] });
						}
					}
				}

				return tokens;
			}

			// Regular text content
			return TokenizeWords(content, GetCurrentTextTokenType());
		}

		private List<Token> TokenizeWords(string text, Token.TokenType tokenType)
		{
			var tokens = new List<Token>();

			// Split on whitespace but preserve word boundaries
			var words = text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

			foreach (string word in words)
			{
				tokens.Add(new Token { Type = tokenType, Text = word });
			}

			return tokens;
		}

		private Token.TokenType GetCurrentTextTokenType()
		{
			return tokenMode switch
			{
				TokenMode.Bullet => Token.TokenType.BulletText,
				TokenMode.Vocative => Token.TokenType.VocativeText,
				TokenMode.VocativeBullet => Token.TokenType.VocativeBulletText,
				_ => Token.TokenType.Text
			};
		}

		private bool IsFrame(string trimmed)
		{
			return trimmed.StartsWith("//=") || trimmed.StartsWith("// =");
		}

		private Token LexFrame(string trimmed)
		{
			Token.TokenType detectedFrameType;
			string title = "";

			if (trimmed.StartsWith("// ="))
			{
				string afterFrame = trimmed.Substring(4);
				if (string.IsNullOrEmpty(afterFrame) || afterFrame.All(c => c == '=' || char.IsWhiteSpace(c)))
				{
					// No title, just frame with optional equals
					detectedFrameType = Token.TokenType.SpacedFrame;
				}
				else
				{
					// Extract title (text before next equal sign, truncated to 71 chars)
					title = ExtractFrameTitle(afterFrame, 71);
					detectedFrameType = Token.TokenType.SpacedTitleFrame;
				}
			}
			else if (trimmed.StartsWith("//="))
			{
				string afterFrame = trimmed.Substring(3);
				if (string.IsNullOrEmpty(afterFrame) || afterFrame.All(c => c == '=' || char.IsWhiteSpace(c)))
				{
					// No title, just frame with optional equals
					detectedFrameType = Token.TokenType.Frame;
				}
				else
				{
					// Extract title (text before next equal sign, truncated to 72 chars)
					title = ExtractFrameTitle(afterFrame, 72);
					detectedFrameType = Token.TokenType.TitleFrame;
				}
			}
			else
			{
				return new Token { Type = Token.TokenType.Unknown, Text = trimmed };
			}

			// Apply frame consistency rules
			Token.TokenType consistentFrameType = GetConsistentFrameType(detectedFrameType);

			return new Token { Type = consistentFrameType, Text = title };
		}

		private string ExtractFrameTitle(string content, int maxLength)
		{
			// Skip any leading equals signs to find the start of the title
			int titleStart = 0;
			while (titleStart < content.Length && content[titleStart] == '=')
			{
				titleStart++;
			}

			// Find the end of the title (next equal sign after the title, or end of
			// content)
			int titleEnd = content.Length;
			for (int i = titleStart; i < content.Length; i++)
			{
				if (content[i] == '=')
				{
					titleEnd = i;
					break;
				}
			}

			string title;
			if (titleEnd > titleStart)
			{
				// Extract the title between the leading and trailing equals
				title = content.Substring(titleStart, titleEnd - titleStart);
			}
			else
			{
				// No title found (all equals or empty)
				title = "";
			}

			// Trim whitespace and truncate to max length
			title = title.Trim();
			if (title.Length > maxLength)
			{
				title = title.Substring(0, maxLength);
			}

			return title;
		}

		private string ExtractCommentContent(string trimmed)
		{
			if (trimmed.StartsWith("// "))
			{
				return trimmed.Substring(3);
			}
			else if (trimmed.StartsWith("//"))
			{
				return trimmed.Substring(2);
			}
			return trimmed;
		}

		private bool IsVocativeStart(string content)
		{
			var colonIndex = content.IndexOf(':');
			if (colonIndex <= 0) return false;

			string beforeColon = content.Substring(0, colonIndex).Trim();

			// Must be a single word: letters and hyphens only, no spaces
			return !string.IsNullOrEmpty(beforeColon)
				&& !beforeColon.Contains(' ')
				&& beforeColon.All(c => char.IsLetter(c) || c == '-');
		}

		private string ExtractVocativeWord(string content)
		{
			var colonIndex = content.IndexOf(':');
			return content.Substring(0, colonIndex).Trim();
		}

		private void UpdateTokenModeFromLine(string line)
		{
			string trimmed = line.TrimStart();

			// Empty lines are the ONLY thing that resets mode to Normal
			if (string.IsNullOrWhiteSpace(trimmed) || trimmed == "//")
			{
				tokenMode = TokenMode.Normal;
				return;
			}

			// Non-comment lines don't change mode
			if (!trimmed.StartsWith("//")) return;

			string content = ExtractCommentContent(trimmed).TrimStart();

			// Check for vocative start (single word + colon at line beginning)
			if (IsVocativeStart(content))
			{
				// Check if this vocative line also contains a bullet
				string afterColon = content.Substring(content.IndexOf(':') + 1).Trim();
				if (afterColon.StartsWith("- "))
				{
					tokenMode = TokenMode.VocativeBullet;
				}
				else
				{
					tokenMode = TokenMode.Vocative;
				}
				return;
			}

			// Check for bullet start
			if (content.StartsWith("- "))
			{
				// If we're already in a vocative context, bullets become vocative bullets
				if (tokenMode == TokenMode.Vocative || tokenMode == TokenMode.VocativeBullet)
				{
					tokenMode = TokenMode.VocativeBullet;
				}
				else
				{
					tokenMode = TokenMode.Bullet;
				}
				return;
			}

			// Everything else: maintain current mode
			// This includes:
			// - Continuation text for bullets/vocatives (regardless of indentation)
			// - Regular text lines
			// - Frame lines
			// - Any other content

			// No mode change - stay in current mode until we hit an empty line
		}

		#endregion Private
	}
}
