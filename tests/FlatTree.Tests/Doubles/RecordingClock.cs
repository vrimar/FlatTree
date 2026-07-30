namespace FlatTree.Tests.Doubles;

/// <summary>
/// A <see cref="FakeClock"/> that also acts as a side-channel for teardown hooks: nodes append to
/// <see cref="Released"/> from <c>DoReset</c>/<c>OnTerminate</c>, so a test can assert that an
/// aborted subtree got a live context and cleaned up.
/// </summary>
public sealed class RecordingClock : IClock
{
    public long NowMs { get; private set; }

    public List<string> Released { get; } = new List<string>();

    public void Advance(long ms) => NowMs += ms;

    public void Release(string what) => Released.Add(what);
}
