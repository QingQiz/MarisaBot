using NAudio.Wave;

namespace Marisa.Plugin.Shared.Util
{
    public class WavFileExt
    {
        private readonly WaveFileReader _reader;
        private readonly int _bytesPerMillisecond;

        public TimeSpan TotalSecond => _reader.TotalTime;

        public WavFileExt(string filePth)
        {
            _reader              = new WaveFileReader(filePth);
            _bytesPerMillisecond = _reader.WaveFormat.AverageBytesPerSecond / 1000;
        }

        public void TrimWav(Stream outStream, TimeSpan cutFromStart, TimeSpan cutLength)
        {
            using var writer = new WaveFileWriter(outStream, _reader.WaveFormat);

            var startPos = (int)cutFromStart.TotalMilliseconds * _bytesPerMillisecond;
            startPos -= startPos % _reader.WaveFormat.BlockAlign;

            var endPos = startPos + (int)cutLength.TotalMilliseconds * _bytesPerMillisecond;
            endPos =  Math.Min(endPos, (int)_reader.Length);
            endPos -= endPos & _reader.WaveFormat.BlockAlign;

            TrimWav(writer, startPos, endPos);
        }

        private void TrimWav(WaveFileWriter writer, int startPos, int endPos)
        {
            _reader.Position = startPos;

            var buffer = new byte[1024];
            while (_reader.Position < endPos)
            {
                var bytesRequired = (int)(endPos - _reader.Position);
                if (bytesRequired <= 0) continue;

                var bytesToRead = buffer.Length - buffer.Length % writer.WaveFormat.BlockAlign;
                var bytesRead   = _reader.Read(buffer, 0, bytesToRead);

                if (bytesRead > 0) writer.Write(buffer, 0, bytesRead);
            }
        }
    }
}