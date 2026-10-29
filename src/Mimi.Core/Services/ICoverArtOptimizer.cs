namespace Mimi.Services;

/// <summary>
/// Creates the fixed-size cover-art variants used by the app from an
/// already-cached image file. This is used to upgrade cache entries created
/// before resized variants existed without rescanning the audio files.
/// </summary>
public interface ICoverArtOptimizer
{
    /// <returns>
    /// The canonical large-variant path, or <see langword="null"/> when the
    /// source cannot be read, decoded, resized, or written.
    /// </returns>
    string? CreateResizedVariants(string coverArtPath);
}
