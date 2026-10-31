namespace TinyHex.Tests.TestSupport;

/// <summary>
/// Writes the given bytes to a temporary file that is deleted on dispose.
/// </summary>
public sealed class TempFile : IDisposable
{
    public string Path { get; }

    public TempFile(byte[] content)
    {
        Path = System.IO.Path.GetTempFileName();
        File.WriteAllBytes(Path, content);
    }

    public static TempFile WithSequentialBytes(int length)
    {
        var content = new byte[length];
        for (int i = 0; i < length; i++)
            content[i] = (byte)i;
        return new TempFile(content);
    }

    public void Dispose() => File.Delete(Path);
}
