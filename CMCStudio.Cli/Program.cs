using System.IO;
using System.Text.Json;
using CMCStudio;
using CMCStudio.Engine;

internal record RosterEntry(int Id,string Name,string Portrait,string SheetName=null,string Attribute=null);
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length==0||args[0] is "--help" or "-h")
            {
                Console.WriteLine("CMCStudio.Cli import-roster --source og.iso --project input.cmcproject --manifest roster.json --output output.cmcproject");
                Console.WriteLine("Manifest: array of {id, name, portrait}. IDs define the enabled roster; other slots are disabled. Relative portrait paths resolve beside the manifest. Uses the GUI's full-colour, linked, keep-resolution importer. Combat stats and attributes are preserved.");
                Console.WriteLine("CMCStudio.Cli export-iso --source og.iso --project input.cmcproject --output new.iso");
                Console.WriteLine("CMCStudio.Cli export-report --source og.iso --project mod.cmcproject --output report");
                return 0;
            }
            if(args[0] is not ("recover-iso" or "import-roster" or "export-iso" or "export-report"))throw new ArgumentException("Unknown command. Use --help.");
            var options=new Dictionary<string,string>(StringComparer.Ordinal);
            for(int i=1;i<args.Length;i+=2)
            {
                if(!args[i].StartsWith("--")||i+1>=args.Length)throw new ArgumentException("Options require a value: "+args[i]);
                options.Add(args[i],args[i+1]);
            }
            string Required(string key)=>options.TryGetValue(key,out var value)?Path.GetFullPath(value):throw new ArgumentException("Missing "+key);
            string source=Required("--source"),output=Required("--output");
            if(args[0]=="recover-iso"){Workspace.RecoverIso(source,Required("--iso"),output,Console.WriteLine);return 0;}
            string project=Required("--project");
            Console.WriteLine("Opening project with the app's Workspace loader...");
            var work=Workspace.OpenProjectFile(source,project);
            if(args[0]=="export-report")
            {
                work.SaveChangeReport(output);Console.WriteLine("Exported "+output+".changes.md");return 0;
            }
            if(args[0]=="export-iso")
            {
                work.Export(output,(message,percent)=>Console.WriteLine(message));Console.WriteLine("Exported "+output);return 0;
            }
            string manifest=Required("--manifest");
            var entries=JsonSerializer.Deserialize<List<RosterEntry>>(File.ReadAllText(manifest),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Empty manifest.");
            if(entries.Count!=200||entries.Select(e=>e.Id).Distinct().Count()!=entries.Count||entries.Any(e=>e.Id<0||e.Id>=277))throw new InvalidDataException("Roster requires exactly 200 distinct monster IDs between 0 and 276.");
            var imports=new List<(Asset Asset,byte[] Image)>();
            foreach(var entry in entries)
            {
                MonsterNames.Validate(entry.Name);
                string portrait=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifest)!,entry.Portrait));
                imports.Add((Catalog.Data.Assets.Single(a=>a.Type==7&&a.Id==entry.Id),File.ReadAllBytes(portrait)));
            }
            Console.WriteLine($"Importing {entries.Count} portraits using the shared GUI importer...");
            work.Session.ImportPortraitBatch(imports,linked:true,originalSize:true);
            var enabled=entries.Select(e=>e.Id).ToHashSet();
            for(int i=0;i<work.Monsters.Count;i++)work.Monsters[i].IsUsed=enabled.Contains(i);
            foreach(var entry in entries)work.Session.MonsterNames[entry.Id].Name=entry.Name;
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            if(File.Exists(output))
            {
                string backup=output+".before-roster-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".bak";
                File.Copy(output,backup,false);Console.WriteLine("Backup: "+backup);
            }
            work.SaveProjectFile(output);
            Console.WriteLine($"Saved {entries.Count} names; {work.Session.FullColorPortraitEdits.Count} linked portrait replacements; {work.Monsters.Count(m=>m.IsUsed)} enabled monsters.");
            Console.WriteLine(output);return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
    }
}
