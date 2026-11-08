using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ClosedXML.Excel;
using UglyToad.PdfPig;
namespace AIWork.Infrastructure;
public record ExtractedContent(string? Text,string Status,int? Pages=null);
public static class DocumentExtraction
{
    public static ExtractedContent Read(CheckedAttachment file)
    {
        try
        {
            if(file.Extension==".txt")return new(file.Text,"Text UTF-8 extracted locally");
            if(file.Extension==".docx")
            {
                using var zip=new ZipArchive(new MemoryStream(file.Bytes));var entry=zip.GetEntry("word/document.xml")??throw new InvalidDataException();using var stream=entry.Open();using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});var xml=XDocument.Load(reader);XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                var text=string.Join('\n',xml.Descendants(w+"p").Select(p=>string.Concat(p.Descendants(w+"t").Select(t=>t.Value))));return new(Limit(text),"Word paragraphs/tables extracted locally; images require visual analysis");
            }
            if(file.Extension==".xlsx")
            {
                using var book=new XLWorkbook(new MemoryStream(file.Bytes));var text=new StringBuilder();var count=0;
                foreach(var sheet in book.Worksheets.Take(8)){text.AppendLine("SHEET: "+sheet.Name);foreach(var row in sheet.RowsUsed().Take(500)){var cells=new List<string>();foreach(var cell in row.CellsUsed().Take(30)){if(++count>3000)break;cells.Add(cell.Address+": "+(cell.HasFormula?"[formula not executed]":cell.GetFormattedString()));}text.AppendLine(string.Join(" | ",cells));if(count>3000||text.Length>60000)break;}if(count>3000||text.Length>60000)break;}
                return new(Limit(text.ToString()),"Excel values/tables extracted locally; formulas never executed (bounded to 8 sheets/3000 cells)");
            }
            if(file.Extension==".pdf")
            {
                if(file.Bytes.Length<40||!Encoding.ASCII.GetString(file.Bytes.AsSpan(Math.Max(0,file.Bytes.Length-2048))).Contains("%%EOF"))return new(null,"Unsupported PDF structure; original file preserved");using var pdf=PdfDocument.Open(file.Bytes);var text=string.Join("\n",pdf.GetPages().Take(20).Select(p=>$"PAGE {p.Number}\n{p.Text}"));return new(Limit(text),pdf.NumberOfPages>20?"PDF exceeds 20-page analysis limit":"PDF text extracted locally; page images can be sent for visual analysis",pdf.NumberOfPages);
            }
            return new(null,"Vision required — no text invented");
        }
        catch(Exception ex)when(ex is not OutOfMemoryException){return new(null,"Local extraction unavailable; visual analysis may be required ("+ex.GetType().Name+")");}
    }
    public static IEnumerable<byte[]> WordImages(byte[] bytes)
    {
        using var zip=new ZipArchive(new MemoryStream(bytes));foreach(var entry in zip.Entries.Where(e=>e.FullName.StartsWith("word/media/",StringComparison.Ordinal)&&e.Length<2*1024*1024).Take(3)){using var input=entry.Open();using var buffer=new MemoryStream();input.CopyTo(buffer);yield return buffer.ToArray();}
    }
    private static string? Limit(string? text)=>string.IsNullOrWhiteSpace(text)?null:text[..Math.Min(text.Length,60000)];
}
