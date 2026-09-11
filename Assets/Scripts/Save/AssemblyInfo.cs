using System.Runtime.CompilerServices;

// Lets edit-mode tests drive save-file versioning directly instead of forcing a public setter
// that gameplay code has no business calling.
[assembly: InternalsVisibleTo("Frieren.Tests.EditMode")]
