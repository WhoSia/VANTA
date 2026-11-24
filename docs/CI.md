# Windows CI

GitHub Actions is a read-only build/test worker. It never commits, pushes, tags or creates releases. Checkout credentials are not persisted. All source changes are made through the authenticated WhoSia account; verify author and committer on each new head. CI actor is separate from Git commit identity.

Windows CI builds WPF with .NET 10, runs core smoke checks, resolves FFmpeg in the ephemeral runner, runs a delayed-AAC integration fixture with Unicode paths, then publishes a framework-dependent preview artifact. The FFmpeg package is runner-only and not pinned yet; record its version in logs. Production dependency acquisition requires a separately verified policy. Windows desktop runtime and FFmpeg remain external dependencies.

CI does not prove interactive GUI usability, long-duration sync or perceptual sync. No release tag until acceptance gates pass.
