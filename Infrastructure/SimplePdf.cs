using System.Globalization;
using System.Text;
namespace MSMultilider.Finanzas.Infrastructure;
public static class SimplePdf {
    public static byte[] Create(IEnumerable<string> lines) {
        var text=new StringBuilder("BT /F1 11 Tf 50 790 Td 16 TL\n");
        foreach(var line in lines.Take(40)) {
            var safe=new string(line.Where(c=>c>=32 && c!=127).Take(100).ToArray())
                .Replace("\\","\\\\").Replace("(","\\(").Replace(")","\\)");
            text.Append('(').Append(safe).Append(") Tj T*\n");
        }
        text.Append("ET");
        var stream=text.ToString();
        var objects=new[] {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            $"<< /Length {Encoding.Latin1.GetByteCount(stream)} >>\nstream\n{stream}\nendstream"
        };
        using var output=new MemoryStream();
        void Write(string value)=>output.Write(Encoding.Latin1.GetBytes(value));
        Write("%PDF-1.4\n");
        var offsets=new List<long>{0};
        for(var i=0;i<objects.Length;i++) {offsets.Add(output.Position);Write($"{i+1} 0 obj\n{objects[i]}\nendobj\n");}
        var start=output.Position;
        Write($"xref\n0 {objects.Length+1}\n0000000000 65535 f \n");
        foreach(var offset in offsets.Skip(1))Write(offset.ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");
        Write($"trailer\n<< /Size {objects.Length+1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n");
        return output.ToArray();
    }
}

