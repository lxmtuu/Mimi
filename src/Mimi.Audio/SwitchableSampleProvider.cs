using NAudio.Wave;

namespace Mimi.Services;

/// <summary>
/// Stable sample source placed between the per-track decoder chain and the
/// long-lived volume/output chain. Consecutive tracks with the same wave
/// format can be swapped underneath an already-initialized Windows audio
/// output instead of disposing and reopening that output for every song.
/// The caller must only install sources whose format matches
/// <see cref="WaveFormat"/>.
/// </summary>
internal sealed class SwitchableSampleProvider : ISampleProvider
{
    private readonly object sourceGate = new();
    private ISampleProvider? source;

    public SwitchableSampleProvider(ISampleProvider source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    public void SetSource(ISampleProvider newSource)
    {
        ArgumentNullException.ThrowIfNull(newSource);

        lock (sourceGate)
            source = newSource;
    }

    public void ClearSource()
    {
        lock (sourceGate)
            source = null;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (sourceGate)
            return source?.Read(buffer, offset, count) ?? 0;
    }
}
