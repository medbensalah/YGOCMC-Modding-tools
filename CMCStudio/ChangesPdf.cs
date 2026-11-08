using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SkiaSharp;
namespace CMCStudio;

public sealed partial class MainWindow
{
    async Task ExportChangesPdf()
    {
        var input=new OpenFileDialog{Filter="Changes reports|*.changes.md|Markdown|*.md",Title="Select an ISO changes report"};
        if(input.ShowDialog()!=true)return;
        var output=new SaveFileDialog{Filter="PDF document|*.pdf",FileName=Path.GetFileNameWithoutExtension(input.FileName)+".pdf"};
        if(output.ShowDialog()!=true)return;
        await WithModelProgress(p=>{ChangesPdf.Export(input.FileName,output.FileName,p);return true;},"Formatting PDF report...");
        status.Text="PDF exported: "+output.FileName;
    }
}

// Native PDF output: no printer, browser or Python installation required.
internal static class ChangesPdf
{
    public static void Export(string input,string output,IProgress<string> progress=null)
    {
        string[] lines=File.ReadAllLines(input);
        string temporary=output+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=File.Create(temporary))
            using(var document=SKDocument.CreatePdf(stream))
            using(var regular=SKTypeface.FromFamilyName("Segoe UI"))
            using(var bold=SKTypeface.FromFamilyName("Segoe UI",SKFontStyle.Bold))
            using(var font=new SKFont(regular,9))
            using(var titleFont=new SKFont(bold,17))
            using(var headerFont=new SKFont(bold,9))
            using(var ink=new SKPaint{Color=SKColor.Parse("#233346"),IsAntialias=true})
            using(var fill=new SKPaint())
            {
                const float height=595,margin=32,bottom=560;float width=842;
                SKCanvas canvas=null;float y=0;int page=0;string section="Game data";
                void Text(string text,float x,float top,SKFont f)=>canvas.DrawText(text,x,top+f.Size,SKTextAlign.Left,f,ink);
                void Page()
                {
                    if(canvas!=null)document.EndPage();
                    canvas=document.BeginPage(width,height);canvas.Clear(SKColors.White);page++;
                    Text("CMC STUDIO / GAME DATA",margin,13,headerFont);
                    Text($"Page {page}",width-80,height-24,font);
                    Text("Contents",margin,height-24,font);canvas.DrawLinkDestinationAnnotation(new SKRect(margin,height-25,margin+60,height-10),"contents");
                    y=40;
                }
                List<string> Wrap(string text,float available,SKFont f)
                {
                    var result=new List<string>();string line="";
                    foreach(char c in Clean(text))
                    {
                        if(c=='\n'){result.Add(line);line="";continue;}
                        if(line.Length>0&&f.MeasureText(line+c)>available)
                        {int space=line.LastIndexOf(' ');if(space>0){result.Add(line[..space]);line=line[(space+1)..];}else{result.Add(line);line="";}}
                        line+=c;
                    }
                    result.Add(line);return result;
                }
                void Paragraph(string text,SKFont f)
                {
                    foreach(var line in Wrap(text,width-2*margin,f))
                    {if(y+f.Size+5>bottom)Page();Text(line,margin,y,f);y+=f.Size+5;}
                    y+=6;
                }
                void Link(string label,string destination)
                {
                    foreach(var line in Wrap(label,width-2*margin,font))
                    {
                        if(y+18>bottom)Page();
                        Text(line,margin,y,font);canvas.DrawLinkDestinationAnnotation(new SKRect(margin,y,width-margin,y+14),destination);y+=17;
                    }
                }
                var headings=lines.Select((line,i)=>(line,i)).Where(x=>x.line.StartsWith("## ")).ToArray();
                Page();canvas.DrawNamedDestinationAnnotation(new SKPoint(margin,y),"contents");Paragraph("Report contents",titleFont);
                foreach(var heading in headings)Link(Clean(heading.line.TrimStart('#',' ')),"section-"+heading.i);
                Page();
                for(int at=0;at<lines.Length;)
                {
                    progress?.Report($"Formatting report {at+1}/{lines.Length}");
                    string line=lines[at].Trim();
                    if(line.StartsWith('|')&&at+1<lines.Length&&Regex.IsMatch(lines[at+1],@"^\s*\|[\s:|\-]+\|\s*$"))
                    {
                        string[] headers=Cells(line);at+=2;var rows=new List<string[]>();
                        while(at<lines.Length&&lines[at].TrimStart().StartsWith('|'))rows.Add(Cells(lines[at++]));
                        // Wide grids are split into readable bands; repeat identity columns.
                        var bands=new List<int[]>();
                        bool monsterTable=headers.Length>1&&headers[0]=="ID"&&headers[1]=="Monster";
                        if(monsterTable||headers.Length<=6)bands.Add(Enumerable.Range(0,headers.Length).ToArray());
                        else for(int col=2;col<headers.Length;col+=4)bands.Add(new[]{0,1}.Concat(Enumerable.Range(col,Math.Min(4,headers.Length-col))).ToArray());
                        float[] monsterWidths=monsterTable?Enumerable.Range(0,headers.Length).Select(c=>Math.Max(42,Math.Max(headerFont.MeasureText(Clean(headers[c])),rows.Select(r=>font.MeasureText(Clean(c<r.Length?r[c]:"").Replace("\n"," "))).DefaultIfEmpty(0).Max())+16)).ToArray():null;
                        if(monsterTable){width=monsterWidths.Sum()+2*margin;Page();Paragraph(section,titleFont);}
                        foreach(var band in bands)
                        {
                            if(bands.Count>1){Page();Paragraph(section+" - "+string.Join(", ",band.Skip(2).Select(i=>headers[i])),titleFont);}
                            float cellWidth=(width-2*margin)/band.Length;
                            float ColumnX(int c)=>margin+(monsterTable?monsterWidths.Take(c).Sum():c*cellWidth);
                            void Header()
                            {
                                var wrapped=band.Select(i=>monsterTable?new List<string>{Clean(headers[i])}:Wrap(headers[i],cellWidth-12,headerFont)).ToArray();float h=wrapped.Max(a=>a.Count)*13+12;
                                fill.Color=SKColor.Parse("#DCE7F0");canvas.DrawRect(margin,y,width-2*margin,h,fill);
                                for(int c=0;c<band.Length;c++)for(int n=0;n<wrapped[c].Count;n++)Text(wrapped[c][n],ColumnX(c)+6,y+6+n*13,headerFont);
                                y+=h;
                            }
                            if(y>bottom-90)Page();Header();int index=0;
                            foreach(var row in rows)
                            {
                                var cells=band.Select(i=>monsterTable?new List<string>{Clean(i<row.Length?row[i]:"").Replace("\n"," ")}:Wrap(i<row.Length?row[i]:"",cellWidth-12,font)).ToArray();
                                int count=cells.Max(c=>c.Count),offset=0;
                                if(count*13+12<400 && y+count*13+12>bottom){Page();Paragraph(section+" (continued)",headerFont);Header();}
                                while(offset<count)
                                {
                                    int fit=(int)((bottom-y-12)/13);
                                    if(fit<1){Page();Paragraph(section+" (continued)",headerFont);Header();continue;}
                                    int take=Math.Min(fit,count-offset);float h=take*13+12;
                                    fill.Color=index%2==0?SKColor.Parse("#F1F5F8"):SKColors.White;canvas.DrawRect(margin,y,width-2*margin,h,fill);
                                    for(int c=0;c<cells.Length;c++)for(int n=0;n<take&&offset+n<cells[c].Count;n++)Text(cells[c][offset+n],ColumnX(c)+6,y+6+n*13,font);
                                    y+=h;offset+=take;
                                }
                                index++;
                            }
                            y+=12;
                        }
                        if(monsterTable){width=842;Page();}
                        continue;
                    }
                    at++;if(line.Length==0)continue;
                    if(line.StartsWith('#')){section=line.TrimStart('#',' ');if(y>bottom-80)Page();canvas.DrawNamedDestinationAnnotation(new SKPoint(margin,y),"section-"+(at-1));Paragraph(section,titleFont);}
                    else Paragraph(line,font);
                }
                if(canvas!=null)document.EndPage();document.Close();
            }
            File.Move(temporary,output,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    static string[] Cells(string line)=>line.Trim().Trim('|').Split('|').Select(s=>s.Trim()).ToArray();
    static string Clean(string text)=>WebUtility.HtmlDecode(Regex.Replace(text,@"\[([^\]]+)\]\([^)]*\)","$1").Replace("<br>","\n").Replace("**","").Replace("`",""));
}
