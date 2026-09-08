using Microsoft.Extensions.Logging;
using NAudio.Lame;
using NAudio.Wave;
using System.IO;

namespace ByteRadio.Broadcast;

public class PcmToMp3Converter : IDisposable
{
    private readonly LameMP3FileWriter _writer;
    private readonly MemoryStream _ms;

    public PcmToMp3Converter(WaveFormat waveFormat)
    {
        _ms = new MemoryStream();
        _writer = new LameMP3FileWriter(_ms, waveFormat, LAMEPreset.ABR_320);
    }

    public byte[]? Convert(byte[] buffer)
    {
        _writer.Write(buffer, 0, buffer.Length);
        
        if (_ms.Length > 0)
        {
            var data = _ms.ToArray();
            _ms.SetLength(0); // Clear the memory stream for the next write
            return data;
        }

        return null;
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _ms?.Dispose();
    }
}