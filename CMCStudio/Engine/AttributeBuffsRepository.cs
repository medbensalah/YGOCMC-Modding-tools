using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CMCStudio.Models;
using static CMCStudio.Models.AttributeBuffsViewModel;
using static CMCStudio.Models.StarterKitstListViewModel;

namespace CMCStudio.Data
{
    public class AttributeBuffsRepository
    {
        public static long BaseAddress = 0x004569C2;



        public List<ushort> ReadAll(string path)
        {
            var buffs = new List<ushort>();

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                fs.Seek(BaseAddress, SeekOrigin.Begin);
                // read 2 bytes, skip 8 bytes, repeat 64 times
                for (int i = 0; i < 64; i++)
                {
                    var buff = br.ReadUInt16();
                    buffs.Add(buff);
                    fs.Seek(6, SeekOrigin.Current);
                }
            }
            //for (int i = 0; i < buffs.Count; i++)
            //{
            //    //suppose it is a row major 8x8 matrix get row and column
            //    var buff = buffs[i];
            //    var row = i / 8;
            //    var col = i % 8;
            //    BuffInfo b = ParseBuffs(buff);
            //    Debug.WriteLine($"{attr[row]} -> {attr[col]} : {b}");
            //}

            return buffs;
        }

        public void WriteAll(string path, List<BuffInfo> buffs)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                fs.Seek(BaseAddress, SeekOrigin.Begin);
                for (int i = 0; i < buffs.Count; i++)
                {
                    bw.Write(buffs[i].RawValue);
                    fs.Seek(6, SeekOrigin.Current);
                }
            }
        }
    }
}