using NUnit.Framework;

// Every test here is an out-of-process build in its own temporary folder, so nothing shares
// state and everything can run at once. ParallelScope.All rather than Children: it makes the
// fixtures parallelizable with each other, not just the tests within one.
[assembly: Parallelizable(ParallelScope.All)]
