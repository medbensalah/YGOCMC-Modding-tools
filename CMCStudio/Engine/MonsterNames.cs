using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace CMCStudio.Engine
{
    public sealed class MonsterNameRow : INotifyPropertyChanged
    {
        public int Id { get; }
        public string LoadedName { get; }
        private string _name;
        public string Name
        {
            get => _name;
            set { MonsterNames.Validate(value); _name=value; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Name))); }
        }
        public bool Changed => Name!=LoadedName;
        public event PropertyChangedEventHandler PropertyChanged;
        public MonsterNameRow(int id,string name){Id=id;LoadedName=name;_name=name;}
    }

    public static class MonsterNames
    {
        private const int Entries=512, Header=2*(Entries+1);
        private static Asset Asset(string source)=>ModelArchives.Resolve(source,Catalog.Data.Assets.Single(a=>a.Type==14&&a.Id==3));
        private static byte[][] ReadEntries(string source)
        {
            var asset=Asset(source);using var f=File.OpenRead(source);var bytes=new byte[asset.Size];f.Position=asset.Iso;f.ReadExactly(bytes);
            if(bytes.Length<Header||BitConverter.ToUInt16(bytes)!=Header)throw new InvalidDataException("Unsupported monster-name table.");
            int end=Entries*2+BitConverter.ToUInt16(bytes,Entries*2);
            if(end<Header||end>bytes.Length||bytes.AsSpan(end).IndexOfAnyExcept((byte)0)>=0)throw new InvalidDataException("Invalid monster-name table extent.");
            var names=new byte[Entries][];
            for(int i=0;i<Entries;i++)
            {
                int start=2*i+BitConverter.ToUInt16(bytes,2*i);
                if(start<Header||start>=end)throw new InvalidDataException("Monster-name pointer outside text data.");
                int length=bytes.AsSpan(start,end-start).IndexOf((byte)0x1f);
                if(length<0)throw new InvalidDataException("Unterminated monster name.");
                names[i]=bytes.AsSpan(start,length+1).ToArray();
            }
            return names;
        }
        public static List<MonsterNameRow> Read(string source)=>ReadEntries(source).Take(277).Select((b,i)=>new MonsterNameRow(i,Encoding.ASCII.GetString(b,0,b.Length-1))).ToList();
        public static void Validate(string name)
        {
            if(string.IsNullOrWhiteSpace(name)||name.Length>63||name.Any(c=>c<' '||c>'~'))throw new InvalidDataException("Names require 1–63 printable English characters.");
        }
        public static Dictionary<string,byte[]> Build(string source,IEnumerable<MonsterNameRow> rows)
        {
            var edits=rows.Where(r=>r.Changed).ToArray();var result=new Dictionary<string,byte[]>();if(edits.Length==0)return result;
            var entries=ReadEntries(source);
            foreach(var row in edits){if(row.Id<0||row.Id>=277)throw new InvalidDataException("Unknown monster name ID.");Validate(row.Name);entries[row.Id]=Encoding.ASCII.GetBytes(row.Name+"\x1f");}
            using var output=new MemoryStream();output.Write(new byte[Header]);var positions=new Dictionary<string,int>();var pointers=new ushort[Entries+1];
            for(int i=0;i<Entries;i++)
            {
                string key=Convert.ToBase64String(entries[i]);
                if(!positions.TryGetValue(key,out int at)){at=checked((int)output.Position);positions.Add(key,at);output.Write(entries[i]);}
                int relative=at-2*i;if(relative<0||relative>ushort.MaxValue)throw new InvalidDataException("Monster-name table exceeds its pointer limit.");pointers[i]=(ushort)relative;
            }
            pointers[Entries]=checked((ushort)(output.Length-2*Entries));
            while(output.Length%2048!=0)output.WriteByte(0);
            var rebuilt=output.ToArray();for(int i=0;i<pointers.Length;i++)BitConverter.GetBytes(pointers[i]).CopyTo(rebuilt,2*i);
            result["14/3"]=rebuilt;return result;
        }
    }
}
