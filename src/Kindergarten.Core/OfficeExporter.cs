using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Kindergarten.Core;

public static class OfficeExporter
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace C = "http://schemas.openxmlformats.org/package/2006/content-types";

    private static void Part(ZipArchive archive, string name, XDocument document)
    {
        using var stream = archive.CreateEntry(name).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        document.Save(writer);
    }

    private static XDocument Relationships(params (string Id, string Type, string Target)[] links) =>
        new(new XElement(P + "Relationships", links.Select(x => new XElement(P + "Relationship",
            new XAttribute("Id", x.Id), new XAttribute("Type", R.NamespaceName + "/" + x.Type), new XAttribute("Target", x.Target)))));

    private static XDocument Types(params (string Part, string Type)[] entries) => new(new XElement(C + "Types",
        new XElement(C + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
        new XElement(C + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
        entries.Select(x => new XElement(C + "Override",new XAttribute("PartName", x.Part),new XAttribute("ContentType", x.Type)))));

    private static XElement Paragraph(string text, bool bold = false, int size = 22) => new(W + "p",
        new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", 100))),
        new XElement(W + "r", new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", "Times New Roman"), new XAttribute(W + "hAnsi", "Times New Roman")),
            bold ? new XElement(W + "b") : null, new XElement(W + "sz", new XAttribute(W + "val", size))),
            new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));

    public static void Word(string path, ReportData report)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Part(archive, "[Content_Types].xml", Types(
            ("/word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"),
            ("/word/footer1.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml")));
        Part(archive, "_rels/.rels", Relationships(("rId1", "officeDocument", "word/document.xml")));
        Part(archive, "word/_rels/document.xml.rels", Relationships(("rId1", "footer", "footer1.xml")));
        Part(archive,"word/footer1.xml",new XDocument(new XElement(W+"ftr",new XElement(W+"p",
            new XElement(W+"pPr",new XElement(W+"jc",new XAttribute(W+"val","center"))),
            new XElement(W+"fldSimple",new XAttribute(W+"instr","PAGE"),new XElement(W+"r",new XElement(W+"t","1")))))));
        var width = 14800 / report.Headers.Length;
        var table = new XElement(W + "tbl", new XElement(W + "tblPr",
            new XElement(W+"tblW",new XAttribute(W+"w",14800),new XAttribute(W+"type","dxa")),
            new XElement(W+"tblBorders",new[]{"top","left","bottom","right","insideH","insideV"}.Select(side=>new XElement(W+side,new XAttribute(W+"val","single"),new XAttribute(W+"sz",4),new XAttribute(W+"color","D9D9D9")))),
            new XElement(W+"tblCellMar",new[]{"top","left","bottom","right"}.Select(side=>new XElement(W+side,new XAttribute(W+"w",80),new XAttribute(W+"type","dxa"))))),
            new XElement(W+"tblGrid",report.Headers.Select(_=>new XElement(W+"gridCol",new XAttribute(W+"w",width)))));
        var all = new[] { report.Headers }.Concat(report.Rows);
        var index = 0;
        foreach (var row in all)
        {
            var header = index++ == 0;
            table.Add(new XElement(W+"tr",new XElement(W+"trPr",new XElement(W+"cantSplit"),header?new XElement(W+"tblHeader"):null),
                row.Select(cell=>new XElement(W+"tc",new XElement(W+"tcPr",new XElement(W+"tcW",new XAttribute(W+"w",width),new XAttribute(W+"type","dxa")),
                    header?new XElement(W+"shd",new XAttribute(W+"val","clear"),new XAttribute(W+"fill","EAEBED")):null),Paragraph(cell,header,20)))));
        }
        var section = new XElement(W+"sectPr",new XElement(W+"footerReference",new XAttribute(W+"type","default"),new XAttribute(R+"id","rId1")),
            new XElement(W+"pgSz",new XAttribute(W+"w",16838),new XAttribute(W+"h",11906),new XAttribute(W+"orient","landscape")),
            new XElement(W+"pgMar",new XAttribute(W+"top",850),new XAttribute(W+"bottom",850),new XAttribute(W+"left",850),new XAttribute(W+"right",850),new XAttribute(W+"footer",425)));
        Part(archive,"word/document.xml",new XDocument(new XElement(W+"document",new XAttribute(XNamespace.Xmlns+"r",R),new XElement(W+"body",
            Paragraph(report.Title,true,30),Paragraph($"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}"),Paragraph(report.Filter),table,Paragraph($"Количество записей: {report.Rows.Count}"),section))));
    }

    private static string Column(int index)
    {
        var result = "";
        for (var number=index+1;number>0;number=(number-1)/26) result=(char)('A'+(number-1)%26)+result;
        return result;
    }

    private static XElement TextCell(string reference,string value,int style=0)=>new(S+"c",new XAttribute("r",reference),new XAttribute("t","inlineStr"),new XAttribute("s",style),
        new XElement(S+"is",new XElement(S+"t",new XAttribute(XNamespace.Xml+"space","preserve"),value)));

    private static XElement DataCell(string reference,string value,string header)
    {
        var numeric=header is "ID" or "Плата, руб." or "Количество" or "Вместимость" or "Зачислено" or "Свободно";
        if(numeric&&decimal.TryParse(value.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var number))
            return new XElement(S+"c",new XAttribute("r",reference),new XAttribute("s",header=="Плата, руб."?2:0),new XElement(S+"v",number.ToString(CultureInfo.InvariantCulture)));
        return TextCell(reference,value);
    }

    public static void Excel(string path, ReportData report)
    {
        using var archive=ZipFile.Open(path,ZipArchiveMode.Create);
        Part(archive,"[Content_Types].xml",Types(("/xl/workbook.xml","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
            ("/xl/worksheets/sheet1.xml","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"),
            ("/xl/styles.xml","application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")));
        Part(archive,"_rels/.rels",Relationships(("rId1","officeDocument","xl/workbook.xml")));
        Part(archive,"xl/_rels/workbook.xml.rels",Relationships(("rId1","worksheet","worksheets/sheet1.xml"),("rId2","styles","styles.xml")));
        Part(archive,"xl/workbook.xml",new XDocument(new XElement(S+"workbook",new XAttribute(XNamespace.Xmlns+"r",R),
            new XElement(S+"sheets",new XElement(S+"sheet",new XAttribute("name","Отчет"),new XAttribute("sheetId",1),new XAttribute(R+"id","rId1"))),
            new XElement(S+"calcPr",new XAttribute("calcId",191029),new XAttribute("fullCalcOnLoad",1)))));
        var border=new XElement(S+"border",new[]{"left","right","top","bottom"}.Select(x=>new XElement(S+x,new XAttribute("style","thin"),new XElement(S+"color",new XAttribute("rgb","FFD9D9D9")))),new XElement(S+"diagonal"));
        Part(archive,"xl/styles.xml",new XDocument(new XElement(S+"styleSheet",
            new XElement(S+"fonts",new XAttribute("count",2),new XElement(S+"font",new XElement(S+"sz",new XAttribute("val",11)),new XElement(S+"name",new XAttribute("val","Calibri"))),
                new XElement(S+"font",new XElement(S+"b"),new XElement(S+"sz",new XAttribute("val",11)),new XElement(S+"name",new XAttribute("val","Calibri")))),
            new XElement(S+"fills",new XAttribute("count",3),new XElement(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","none"))),new XElement(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","gray125"))),
                new XElement(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","solid"),new XElement(S+"fgColor",new XAttribute("rgb","FFEAEBED")),new XElement(S+"bgColor",new XAttribute("indexed",64))))),
            new XElement(S+"borders",new XAttribute("count",1),border),
            new XElement(S+"cellStyleXfs",new XAttribute("count",1),new XElement(S+"xf",new XAttribute("numFmtId",0),new XAttribute("fontId",0),new XAttribute("fillId",0),new XAttribute("borderId",0))),
            new XElement(S+"cellXfs",new XAttribute("count",3),Enumerable.Range(0,3).Select(i=>new XElement(S+"xf",new XAttribute("numFmtId",i==2?4:0),new XAttribute("fontId",i==1?1:0),new XAttribute("fillId",i==1?2:0),new XAttribute("borderId",0),new XAttribute("xfId",0),new XAttribute("applyAlignment",1),new XElement(S+"alignment",new XAttribute("wrapText",1),new XAttribute("vertical","center"))))),
            new XElement(S+"cellStyles",new XAttribute("count",1),new XElement(S+"cellStyle",new XAttribute("name","Normal"),new XAttribute("xfId",0),new XAttribute("builtinId",0))))));
        var data=new XElement(S+"sheetData",new XElement(S+"row",new XAttribute("r",1),TextCell("A1",report.Title,1)),
            new XElement(S+"row",new XAttribute("r",2),TextCell("A2",$"{DateTime.Now:dd.MM.yyyy HH:mm}. {report.Filter}")),
            new XElement(S+"row",new XAttribute("r",4),report.Headers.Select((x,i)=>TextCell(Column(i)+"4",x,1))));
        for(var r=0;r<report.Rows.Count;r++) data.Add(new XElement(S+"row",new XAttribute("r",r+5),report.Rows[r].Select((x,c)=>DataCell(Column(c)+(r+5),x,report.Headers[c]))));
        var totalRow=report.Rows.Count+6;
        data.Add(new XElement(S+"row",new XAttribute("r",totalRow),TextCell("A"+totalRow,"Количество записей",1),
            new XElement(S+"c",new XAttribute("r","B"+totalRow),new XElement(S+"f",report.Rows.Count==0?"0":$"COUNTA(A5:A{report.Rows.Count+4})"),new XElement(S+"v",report.Rows.Count))));
        var feeColumn=Array.IndexOf(report.Headers,"Плата, руб.");
        if(feeColumn>=0)
        {
            var sum=report.Rows.Sum(row=>decimal.TryParse(row[feeColumn].Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var value)?value:0);
            var letter=Column(feeColumn);
            data.Add(new XElement(S+"row",new XAttribute("r",totalRow+1),TextCell("A"+(totalRow+1),"Сумма платы по выбранным записям",1),
                new XElement(S+"c",new XAttribute("r","B"+(totalRow+1)),new XAttribute("s",2),new XElement(S+"f",report.Rows.Count==0?"0":$"SUM({letter}5:{letter}{report.Rows.Count+4})"),new XElement(S+"v",sum.ToString(CultureInfo.InvariantCulture)))));
        }
        Part(archive,"xl/worksheets/sheet1.xml",new XDocument(new XElement(S+"worksheet",
            new XElement(S+"sheetViews",new XElement(S+"sheetView",new XAttribute("workbookViewId",0),new XElement(S+"pane",new XAttribute("ySplit",4),new XAttribute("topLeftCell","A5"),new XAttribute("activePane","bottomLeft"),new XAttribute("state","frozen")))),
            new XElement(S+"cols",Enumerable.Range(0,report.Headers.Length).Select(i=>new XElement(S+"col",new XAttribute("min",i+1),new XAttribute("max",i+1),new XAttribute("width",i==0?12:28),new XAttribute("customWidth",1)))),
            data,new XElement(S+"autoFilter",new XAttribute("ref",$"A4:{Column(report.Headers.Length-1)}{report.Rows.Count+4}")),
            new XElement(S+"mergeCells",new XAttribute("count",2),new XElement(S+"mergeCell",new XAttribute("ref",$"A1:{Column(report.Headers.Length-1)}1")),new XElement(S+"mergeCell",new XAttribute("ref",$"A2:{Column(report.Headers.Length-1)}2"))),
            new XElement(S+"pageMargins",new XAttribute("left",0.3),new XAttribute("right",0.3),new XAttribute("top",0.5),new XAttribute("bottom",0.5),new XAttribute("header",0.2),new XAttribute("footer",0.2)),
            new XElement(S+"pageSetup",new XAttribute("paperSize",9),new XAttribute("orientation","landscape"),new XAttribute("fitToWidth",1),new XAttribute("fitToHeight",0)))));
    }
}
