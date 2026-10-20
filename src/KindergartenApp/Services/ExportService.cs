using System.IO;
using Kindergarten.Core;

namespace KindergartenApp.Services;

public static class ExportService
{
    public static void Save(string path,string format,ReportData report)
    {
        var temp=Path.Combine(Path.GetDirectoryName(path)!,Guid.NewGuid()+".tmp");
        try
        {
            if(format=="DOCX")OfficeExporter.Word(temp,report);
            else if(format=="XLSX")OfficeExporter.Excel(temp,report);
            else PdfExporter.Save(temp,report);
            File.Move(temp,path,true);
        }
        finally { if(File.Exists(temp))File.Delete(temp); }
    }

}
