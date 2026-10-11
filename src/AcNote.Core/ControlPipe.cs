using System.IO.Pipes;
using System.Text;

namespace AcNote.Core;

public static class ControlPipe
{
    public const string Name = "AcNote.Control";

    public static string? Transact(string requestJson, int attempts = 20, int delayMs = 250)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", Name, PipeDirection.InOut);
                pipe.Connect(800);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                writer.WriteLine(requestJson);
                return reader.ReadLine();
            }
            catch (TimeoutException)
            {
                Thread.Sleep(delayMs);
            }
            catch (IOException)
            {
                Thread.Sleep(delayMs);
            }
        }

        return null;
    }
}
