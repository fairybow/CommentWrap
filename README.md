# CommentWrap

A comment-wrapping Visual Studio extension.

Activated by pressing `Alt+Q` with the cursor inside the comment.

(Currently, it only works on `//` comments.)

The purpose is to turn stuff like this:

```
// =TITLE
//         This is some text. It will be long so we can demonstrate the wrapping at 80 characters.
//
// - This is a bullet that is going to use text that is over the 80 character limit for text in a line.
//
// Bugs: Bugs are listed here, and this line will also go on for over 80 characters.
// 
//      Description: Handles network connections and data transmission for the application. Manages connection pooling, retry logic, and error handling.
//
// TODOs: - Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.
// - Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.
// - Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.
```

into this:

```
// ==== TITLE =================================================================
// This is some text. It will be long so we can demonstrate the wrapping at 80
// characters.
//
// - This is a bullet that is going to use text that is over the 80 character
//   limit for text in a line.
//
// Bugs:        Bugs are listed here, and this line will also go on for over 80
//              characters.
//
// Description: Handles network connections and data transmission for the
//              application. Manages connection pooling, retry logic, and error
//              handling.
//
// TODOs:       - Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed
//                do eiusmod tempor incididunt ut labore et dolore magna aliqua.
//              - Ut enim ad minim veniam, quis nostrud exercitation ullamco
//                laboris nisi ut aliquip ex ea commodo consequat.
//              - Duis aute irure dolor in reprehenderit in voluptate velit esse
//                cillum dolore eu fugiat nulla pariatur.
// =============================================================================
```
