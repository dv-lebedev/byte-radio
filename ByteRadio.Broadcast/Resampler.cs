
using NAudio.Wave;
using NAudio.Wave.Compression;
using Serilog;

namespace ByteRadio.Broadcast;

public class Resampler
{
    private readonly Dictionary<int, AcmStream> _captureDevicesResamplers = [];
    private readonly Dictionary<int, byte[]> _notResampledHeadChunks = [];

    public WaveFormat SourceWaveFormat { get; private set; }
    public WaveFormat DestinationWaveFormat { get; private set; }

    public Resampler(WaveFormat sourceWaveFormat, WaveFormat destinationWaveFormat)
    {
        SourceWaveFormat = sourceWaveFormat;
        DestinationWaveFormat = destinationWaveFormat;        
    }

    public byte[] ResampleRawPcmData(byte[] input, int channel)
    {
        if (SourceWaveFormat.Equals(DestinationWaveFormat))
        {
            return input;
        }

        // https://markheath.net/post/input-driven-resampling-with-naudio-using-acm
        int resampledBytes = 0;
        int sourceBytesConverted = 0;

        byte[]? sourceBa = null;
        if (!_notResampledHeadChunks.TryGetValue(channel, out byte[] headBa))
            sourceBa = input;
        else
        {
            sourceBa = new byte[headBa.Length + input.Length];
            Buffer.BlockCopy(headBa, 0, sourceBa, 0, headBa.Length);
            Buffer.BlockCopy(input, 0, sourceBa, headBa.Length, input.Length);
            _notResampledHeadChunks.Remove(channel);
        }

        var sourceLength = sourceBa.Length;

        AcmStream _acmStream = null;
        try
        {
            if (!_captureDevicesResamplers.TryGetValue(channel, out _acmStream))
            {
                _acmStream = new AcmStream(SourceWaveFormat, DestinationWaveFormat);
                Log.Debug("Created AcmStream with src: {src} and dst: {dst} for channel: {channel}",
                    SourceWaveFormat, DestinationWaveFormat, channel);
                _captureDevicesResamplers.Add(channel, _acmStream);
            }


            var startIndex = 0;
            int bytesToProcess = _acmStream.SourceBuffer.Length;


            double srcDstRatio = (double)Math.Max(DestinationWaveFormat.SampleRate, SourceWaveFormat.SampleRate) /
                Math.Min(DestinationWaveFormat.SampleRate, SourceWaveFormat.SampleRate);
            var destBufferLength = sourceBa.Length * srcDstRatio + 100; //extra 100bytes will be enough
            byte[] destBuffer = new byte[(int)destBufferLength];
            var totalBytesResampled = 0;
            while (sourceBa.Length - startIndex >= bytesToProcess)
            {
                ResampleChunk(channel, sourceBa, startIndex, bytesToProcess, destBuffer, out resampledBytes, out sourceBytesConverted);
                startIndex += sourceBytesConverted;

                Buffer.BlockCopy(_acmStream.DestBuffer, 0, destBuffer, totalBytesResampled, resampledBytes);
                totalBytesResampled += resampledBytes;

            }
            if (startIndex < sourceBa.Length)
            {
                bytesToProcess = sourceBa.Length - startIndex;
                ResampleChunk(channel, sourceBa, startIndex, bytesToProcess, destBuffer, out resampledBytes, out sourceBytesConverted);
                startIndex += sourceBytesConverted;

                Buffer.BlockCopy(_acmStream.DestBuffer, 0, destBuffer, totalBytesResampled, resampledBytes);
                totalBytesResampled += resampledBytes;
            }

            return destBuffer.Length > totalBytesResampled
                    ? destBuffer[..totalBytesResampled]
                    : destBuffer;
        }
        catch (Exception e)
        {
            // Source length: 132960, SourceBufferLength: 96000 convertedBytes:0
            Log.Error(e, "Source length: {sourceLength}, SourceBufferLength: {sourceBufferLength} sourceBytesConverted: {sourceBytesConverted} convertedBytes:{convertedBytes}",
                sourceBa.Length, _acmStream?.SourceBuffer.Length, sourceBytesConverted, resampledBytes);
        }
        return null;
    }

    public void DisposeResamplers()
    {
        foreach (var pair in _captureDevicesResamplers)
        {
            var acmStream = pair.Value;
            acmStream.Dispose();
        }
    }

    private void ResampleChunk(int channel, byte[] source, int startIndex, int bytesToProcess, byte[] destBuffer, out int resampledBytes, out int sourceBytesConverted)
    {
        AcmStream _acmStream = _captureDevicesResamplers[channel];
        Buffer.BlockCopy(source, startIndex, _acmStream.SourceBuffer, 0, bytesToProcess);

        resampledBytes = _acmStream.Convert(bytesToProcess, out sourceBytesConverted);
        if (sourceBytesConverted != bytesToProcess)
        {
            var notResampledBytes = bytesToProcess - sourceBytesConverted;

            var headBa = new byte[notResampledBytes];

            Buffer.BlockCopy(source, sourceBytesConverted, headBa, 0, notResampledBytes);

            _notResampledHeadChunks.Add(channel, headBa);
            //Log.Error("We didn't convert everything from {sourceLength} bytes. Only {sourceBytesConverted} bytes converted", source.Length, sourceBytesConverted);
        }
    }

    public byte[] ResampleMultiChannelDataSplittingChannels(byte[] sourceAudioData, int channelCount)
    {
        byte[][] extractedChannels = ExtractIndividualChannels(sourceAudioData, channelCount, sizeof(short));
        byte[][] resampledDataPerChannel = new byte[channelCount][];
        for (int i = 0; i < channelCount; i++)
        {
            byte[] res = ResampleRawPcmData(extractedChannels[i], channel: i);
            if (res == null)
            {
                return null;
            }
            //var resampledRawData = new byte[resampledBufferSize];
            //Buffer.BlockCopy(res, 0, resampledRawData, 0, resampledBufferSize);
            resampledDataPerChannel[i] = res;
        }

        return CombineIndividualChannels(resampledDataPerChannel, sizeof(short));
    }

    private byte[][] ExtractIndividualChannels(byte[] source, int channelCount, int sampleSize)
    {
        int eachChannelSize = source.Length / channelCount;
        int FrameCount = eachChannelSize / sizeof(short);

        var ExtractedChannels = new byte[channelCount][];
        for (int i = 0; i < channelCount; i++)
            ExtractedChannels[i] = new byte[eachChannelSize];

        int BytesInEachFrame = channelCount * sampleSize;

        for (int ChannelCounter = 0; ChannelCounter < channelCount; ChannelCounter++)
        {
            //Finish One Channel
            int frameCounter = 0;
            for (int byteCounter = sizeof(short) * ChannelCounter; frameCounter < FrameCount; byteCounter += BytesInEachFrame)
            {
                ExtractedChannels[ChannelCounter][2 * frameCounter] = source[byteCounter];
                ExtractedChannels[ChannelCounter][2 * frameCounter + 1] = source[byteCounter + 1];
                frameCounter++;
            }
        }

        return ExtractedChannels;
    }

    private byte[] CombineIndividualChannels(byte[][] channels, int sampleSize)
    {
        if (channels == null || channels.Length == 0)
        {
            throw new ArgumentException("Channels cannot be null or empty.", nameof(channels));
        }

        int channelCount = channels.Length;
        int channelLength = channels[0].Length;

        for (int i = 1; i < channelCount; i++)
        {
            if (channels[i].Length != channelLength)
            {
                throw new ArgumentException("All channel arrays must have the same length.");
            }
        }

        int frameCount = channelLength / sampleSize;
        int bytesInFrame = channelCount * sampleSize;
        byte[] combined = new byte[frameCount * bytesInFrame];

        for (int frame = 0; frame < frameCount; frame++)
        {
            for (int ch = 0; ch < channelCount; ch++)
            {
                int srcIndex = frame * sampleSize;
                int destIndex = frame * bytesInFrame + ch * sampleSize;

                for (int b = 0; b < sampleSize; b++)
                {
                    combined[destIndex + b] = channels[ch][srcIndex + b];
                }
            }
        }

        return combined;
    }
}
