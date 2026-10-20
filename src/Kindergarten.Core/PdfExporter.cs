using System.Reflection;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Kindergarten.Core;

public static class PdfExporter
{
    private sealed class FontResolver : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName,bool bold,bool italic)=>new("OpenSans");
        public byte[] GetFont(string faceName)
        {
            using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Kindergarten.Core.OpenSans.ttf")!;
            using var memory=new MemoryStream();stream.CopyTo(memory);return memory.ToArray();
        }
    }

    private static readonly object Sync=new();

    public static void Save(string path,ReportData report)
    {
        lock(Sync)
        {
            GlobalFontSettings.FontResolver??=new FontResolver();
            using var document=new PdfDocument();
            document.Info.Title=report.Title;
            document.Info.Author="Сеть детских садов";
            var font=new XFont("OpenSans",9);
            var heading=new XFont("OpenSans",16);
            var rowIndex=0;
            do
            {
                var page=document.AddPage();page.Size=PageSize.A4;page.Orientation=PageOrientation.Landscape;
                using var graphics=XGraphics.FromPdfPage(page);
                var pageWidth=page.Width.Point;var pageHeight=page.Height.Point;
                var cellWidth=(pageWidth-60)/report.Headers.Length;
                List<string> Wrap(string text,double width)
                {
                    var lines=new List<string>();var line="";
                    foreach(var character in text)
                    {
                        if(character=='\n'||graphics.MeasureString(line+character,font).Width>width)
                        {
                            lines.Add(line);line=character=='\n'?"":character.ToString();
                        }
                        else line+=character;
                    }
                    lines.Add(line);return lines;
                }
                void DrawLines(List<string> lines,double x,double y)
                {
                    foreach(var line in lines){graphics.DrawString(line,font,XBrushes.Black,new XPoint(x,y+10));y+=13;}
                }
                graphics.DrawString(report.Title,heading,XBrushes.Black,new XPoint(30,32));
                var subtitle=Wrap($"{DateTime.Now:dd.MM.yyyy HH:mm}. {report.Filter}",pageWidth-60);
                DrawLines(subtitle,30,45);
                var y=55+subtitle.Count*13d;
                var headerLines=report.Headers.Select(x=>Wrap(x,cellWidth-12)).ToList();
                var headerHeight=headerLines.Max(x=>x.Count)*13+12;
                for(var c=0;c<headerLines.Count;c++)
                {
                    graphics.DrawRectangle(XPens.LightGray,XBrushes.LightGray,30+c*cellWidth,y,cellWidth,headerHeight);
                    DrawLines(headerLines[c],36+c*cellWidth,y+5);
                }
                y+=headerHeight;
                while(rowIndex<report.Rows.Count)
                {
                    var lines=report.Rows[rowIndex].Select(x=>Wrap(x,cellWidth-12)).ToList();
                    var rowHeight=Math.Max(26,lines.Max(x=>x.Count)*13+12);
                    if(y+rowHeight>pageHeight-45)break;
                    for(var c=0;c<lines.Count;c++)
                    {
                        graphics.DrawRectangle(XPens.LightGray,rowIndex%2==0?XBrushes.White:XBrushes.WhiteSmoke,30+c*cellWidth,y,cellWidth,rowHeight);
                        DrawLines(lines[c],36+c*cellWidth,y+5);
                    }
                    y+=rowHeight;rowIndex++;
                }
                graphics.DrawString($"Количество записей: {report.Rows.Count} | Страница {document.PageCount}",font,XBrushes.Black,new XPoint(30,pageHeight-22));
            }while(rowIndex<report.Rows.Count);
            document.Save(path);
        }
    }
}
