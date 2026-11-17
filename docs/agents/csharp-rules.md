# C# rules

- Always use braces `{}` for control statements, including `if`, `else`, `for`, `foreach`, and `while`, even when the block contains a single statement. The rule is also configured in `.editorconfig` with `csharp_prefer_braces = true`.
- Separate independent stages of logic with a blank line, for example argument validation, data retrieval, performing an operation, and preparing the result. Do not insert a blank line between closely related statements.
