using System.Globalization;
using System.Text;

namespace PaycheckCalculator.App.Services.Pdf;

/// <summary>
/// Minimal, dependency-free PDF writer covering the subset of features the
/// paycheck export needs: the built-in Helvetica fonts, text, filled
/// rectangles, and embedded baseline-JPEG images (via the <c>DCTDecode</c>
/// filter). It is deliberately not a general-purpose PDF library.
/// </summary>
/// <remarks>
/// Objects can be reserved up-front and filled in later, which lets callers
/// build the catalog/pages tree (whose members reference each other) without
/// worrying about ordering. Byte offsets for the cross-reference table are
/// computed during <see cref="Build"/>, so the output is always self-consistent.
/// </remarks>
internal sealed class PdfDocument
{
    private readonly List<byte[]?> _objects = new();

    /// <summary>Reserves a (1-based) object number whose body is supplied later via <see cref="Set"/>.</summary>
    public int Reserve()
    {
        _objects.Add(null);
        return _objects.Count;
    }

    /// <summary>Appends an object with its body, returning its (1-based) object number.</summary>
    public int Add(byte[] body)
    {
        _objects.Add(body);
        return _objects.Count;
    }

    /// <summary>Appends an object whose body is ASCII text.</summary>
    public int Add(string body) => Add(Encoding.ASCII.GetBytes(body));

    /// <summary>Fills in the body of a previously <see cref="Reserve"/>d object.</summary>
    public void Set(int id, string body) => _objects[id - 1] = Encoding.ASCII.GetBytes(body);

    /// <summary>
    /// Builds a stream object body from a dictionary fragment (the contents of
    /// the <c>&lt;&lt; ... &gt;&gt;</c> without <c>/Length</c>) and raw stream data.
    /// </summary>
    public static byte[] Stream(string dictionaryFragment, byte[] data)
    {
        var header = Encoding.ASCII.GetBytes($"<< {dictionaryFragment} /Length {data.Length} >>\nstream\n");
        var footer = Encoding.ASCII.GetBytes("\nendstream");
        var buffer = new byte[header.Length + data.Length + footer.Length];
        Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
        Buffer.BlockCopy(data, 0, buffer, header.Length, data.Length);
        Buffer.BlockCopy(footer, 0, buffer, header.Length + data.Length, footer.Length);
        return buffer;
    }

    /// <summary>Serializes the document. <paramref name="rootId"/> is the catalog object number.</summary>
    public byte[] Build(int rootId)
    {
        using var ms = new MemoryStream();

        void WriteAscii(string s)
        {
            var b = Encoding.ASCII.GetBytes(s);
            ms.Write(b, 0, b.Length);
        }

        WriteAscii("%PDF-1.5\n");
        // Binary marker comment so tools treat the file as binary.
        ms.WriteByte((byte)'%');
        ms.Write(new byte[] { 0xE2, 0xE3, 0xCF, 0xD3 }, 0, 4);
        ms.WriteByte((byte)'\n');

        var offsets = new long[_objects.Count + 1];
        for (int i = 0; i < _objects.Count; i++)
        {
            var body = _objects[i]
                ?? throw new InvalidOperationException($"PDF object {i + 1} was reserved but never set.");
            offsets[i + 1] = ms.Position;
            WriteAscii($"{i + 1} 0 obj\n");
            ms.Write(body, 0, body.Length);
            WriteAscii("\nendobj\n");
        }

        long xrefOffset = ms.Position;
        int count = _objects.Count + 1;
        WriteAscii("xref\n");
        WriteAscii($"0 {count}\n");
        WriteAscii("0000000000 65535 f \n");
        for (int i = 1; i < count; i++)
            WriteAscii($"{offsets[i].ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");

        WriteAscii("trailer\n");
        WriteAscii($"<< /Size {count} /Root {rootId} 0 R >>\n");
        WriteAscii("startxref\n");
        WriteAscii($"{xrefOffset.ToString(CultureInfo.InvariantCulture)}\n");
        WriteAscii("%%EOF");

        return ms.ToArray();
    }
}
