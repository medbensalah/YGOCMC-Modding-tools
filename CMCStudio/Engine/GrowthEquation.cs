using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
namespace CMCStudio.Engine
{
    public sealed class GrowthParameters
    {
        public int Divisor { get; set; } = 100;
        public int PositiveBias { get; set; } = 99;
        public int NegativeBias { get; set; } = -99;
    }
    public sealed class GrowthEquation : INotifyPropertyChanged
    {
        // The existing signed reciprocal multiply supports these divisors by changing only its shift.
        public static readonly int[] Divisors = {25,50,100,200,400};
        private const int Start=0x38C938, End=0x38D118;
        private const int IsoDelta=0x35880;
        private static readonly int[] Negative={0x38C998,0x38CA7C,0x38CB48,0x38CC28,0x38CD08,0x38CDE8,0x38CEC8,0x38CFA8,0x38D088};
        private static readonly int[] Positive={0x38C9D8,0x38CAB0,0x38CB88,0x38CC68,0x38CD48,0x38CE28,0x38CF08,0x38CFE8,0x38D0C8};
        private static readonly int[][] Shifts={new[]{0x38C9B0,0x38C9F0},new[]{0x38CA90,0x38CAC4},new[]{0x38CB60,0x38CBA0},new[]{0x38CC40,0x38CC80},new[]{0x38CD20,0x38CD60},new[]{0x38CE00,0x38CE40},new[]{0x38CEE0,0x38CF20},new[]{0x38CFC0,0x38D000},new[]{0x38D0A0,0x38D0E0}};
        private readonly int _stat;
        private readonly byte[] _original;
        private readonly GrowthParameters _loaded;
        private readonly GrowthParameters _parameters;
        public string Stat => ModSession.GrowthStats[_stat];
        public int Divisor { get=>_parameters.Divisor; set { Validate(new GrowthParameters { Divisor=value,PositiveBias=PositiveBias,NegativeBias=NegativeBias }); _parameters.Divisor=value; Changed(); } }
        public int PositiveBias { get=>_parameters.PositiveBias; set { CheckBias(value); _parameters.PositiveBias=value; Changed(); } }
        public int NegativeBias { get=>_parameters.NegativeBias; set { CheckBias(value); _parameters.NegativeBias=value; Changed(); } }
        public string Equation => $"next = wrap{(_stat<6?16:8)}(previous + trunc((rate × previous + (rate < 0 ? {NegativeBias} : {PositiveBias})) / {Divisor}))";
        public string ReadableEquation => $"1. Multiply the current stat by the level's rate.\n2. Add {PositiveBias} for a positive or zero rate; add {NegativeBias} for a negative rate.\n3. Divide by {Divisor} and drop the decimal part.\n4. Add that change to the current stat.";
        public string WorkedExample => $"Starting at 101: rate +10 gives {Step(101,10)}; rate −10 gives {Step(101,-10)}.";
        public bool ChangedFromSource => Divisor!=_loaded.Divisor || PositiveBias!=_loaded.PositiveBias || NegativeBias!=_loaded.NegativeBias;
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(string.Empty));
        private static void CheckBias(int value) { if(value<short.MinValue || value>short.MaxValue) throw new ArgumentOutOfRangeException(nameof(value),"Rounding biases must be signed 16-bit integers."); }
        public static void Validate(GrowthParameters value)
        {
            if(value==null || !Divisors.Contains(value.Divisor)) throw new InvalidDataException("Growth divisor must be 25, 50, 100, 200 or 400.");
            CheckBias(value.PositiveBias); CheckBias(value.NegativeBias);
        }
        private GrowthEquation(int stat,byte[] routine)
        {
            _stat=stat; _original=routine;
            int a=(int)(BitConverter.ToUInt32(routine,Shifts[stat][0]-Start)>>6&31);
            int b=(int)(BitConverter.ToUInt32(routine,Shifts[stat][1]-Start)>>6&31);
            if(a!=b || a<3 || a>7) throw new InvalidDataException("Unsupported or inconsistent growth divisor instructions.");
            _loaded=new GrowthParameters { Divisor=25<<(a-3),PositiveBias=BitConverter.ToInt16(routine,Positive[stat]-Start),NegativeBias=BitConverter.ToInt16(routine,Negative[stat]-Start) };
            _parameters=new GrowthParameters { Divisor=_loaded.Divisor,PositiveBias=_loaded.PositiveBias,NegativeBias=_loaded.NegativeBias };
        }
        public GrowthParameters Snapshot()=>new GrowthParameters { Divisor=Divisor,PositiveBias=PositiveBias,NegativeBias=NegativeBias };
        public void Set(GrowthParameters value) { Validate(value); _parameters.Divisor=value.Divisor; _parameters.PositiveBias=value.PositiveBias; _parameters.NegativeBias=value.NegativeBias; Changed(); }
        public void Revert()=>Set(_loaded);
        public int Step(int value,int rate)=>CombatMath.GrowthStep(value,rate,_stat<6?16:8,Divisor,PositiveBias,NegativeBias);
        public void AddPatches(PatchSet plan)
        {
            Validate(_parameters);
            if(!ChangedFromSource)return;
            if(PositiveBias!=_loaded.PositiveBias) plan.Add(Positive[_stat]+IsoDelta,BitConverter.GetBytes((short)PositiveBias));
            if(NegativeBias!=_loaded.NegativeBias) plan.Add(Negative[_stat]+IsoDelta,BitConverter.GetBytes((short)NegativeBias));
            if(Divisor!=_loaded.Divisor)
            {
                int shift=3+Array.IndexOf(Divisors,Divisor);
                foreach(int address in Shifts[_stat])
                {
                    uint word=BitConverter.ToUInt32(_original,address-Start);
                    plan.Add(address+IsoDelta,BitConverter.GetBytes((word&~0x7C0u)|((uint)shift<<6)));
                }
            }
        }
        public static List<GrowthEquation> Load(string source)
        {
            using var stream=File.OpenRead(source); stream.Position=Start+IsoDelta;
            var routine=new byte[End-Start]; stream.ReadExactly(routine);
            var normalized=(byte[])routine.Clone();
            foreach(int address in Negative.Concat(Positive)) { normalized[address-Start]=0; normalized[address-Start+1]=0; }
            foreach(int address in Shifts.SelectMany(x=>x))
            {
                uint word=BitConverter.ToUInt32(normalized,address-Start)&~0x7C0u;
                BitConverter.GetBytes(word).CopyTo(normalized,address-Start);
            }
            if(Convert.ToHexString(SHA256.HashData(normalized))!="12043FCEBAAB0F45E787A90AFFAA5A5AA84D7F92F0DB9998086BB739E437EE40")
                throw new InvalidDataException("Growth code differs from the verified instruction layout; equation editing is unsafe.");
            return Enumerable.Range(0,9).Select(stat=>new GrowthEquation(stat,routine)).ToList();
        }
    }
    public sealed class GrowthRateRow
    {
        private readonly ModSession _session;
        private readonly int _element, _stat;
        public GrowthRateRow(ModSession session,int element,int stat) { _session=session; _element=element; _stat=stat; }
        public string Stat=>ModSession.GrowthStats[_stat];
        private string Get(int level)=>_session.Find($"growth/{_element}/{_stat}/{level}").Value;
        private void Set(int level,string value)=>_session.Find($"growth/{_element}/{_stat}/{level}").Value=value;
        public string L2 { get=>Get(0); set=>Set(0,value); }
        public string L3 { get=>Get(1); set=>Set(1,value); }
        public string L4 { get=>Get(2); set=>Set(2,value); }
        public string L5 { get=>Get(3); set=>Set(3,value); }
        public string L6 { get=>Get(4); set=>Set(4,value); }
        public string L7 { get=>Get(5); set=>Set(5,value); }
        public string L8 { get=>Get(6); set=>Set(6,value); }
        public string L9 { get=>Get(7); set=>Set(7,value); }
        public string L10 { get=>Get(8); set=>Set(8,value); }
    }
}
