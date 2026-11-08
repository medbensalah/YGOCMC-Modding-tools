using System;
namespace CMCStudio.Engine
{
    // Recovered integer subsets; context, abilities/statuses and RNG are separate.
    public static class CombatMath
    {
        public static int GrowthStep(int value,int rate,int bits=16,int divisor=100,int positiveBias=99,int negativeBias=-99)
        {
            if(rate < -128 || rate > 127 || divisor<=0 || positiveBias<short.MinValue || positiveBias>short.MaxValue || negativeBias<short.MinValue || negativeBias>short.MaxValue || (bits!=8 && bits!=16)) throw new ArgumentOutOfRangeException();
            int mask=bits==16?65535:255; value &= mask;
            return unchecked(value+unchecked(value*rate+(rate<0?negativeBias:positiveBias))/divisor)&mask;
        }
        public static int CoreDamage(int baseAttack,int contextualAttack,int baseDefense,int contextualDefense,ulong flags)
        {
            int a0=unchecked(baseAttack*100),a=unchecked(contextualAttack*100),d0=unchecked(baseDefense*100),d=unchecked(contextualDefense*100);
            if((flags&2)!=0)d=d0;
            if((flags&0x18)!=0)a=a0;
            foreach(var p in new[]{(0x20UL,1,4),(0x40UL,1,2),(0x200UL,1,1),(0x80UL,-1,4),(0x100UL,-1,2)})
                if((flags&p.Item1)!=0)a=unchecked(a+p.Item2*(a0/p.Item3));
            foreach(var p in new[]{(0x400UL,1,4),(0x800UL,1,2),(0x2000000UL,30,100),(0x4000000UL,35,100),(0x8000000UL,40,100),(0x1000UL,-1,4),(0x2000UL,-1,2)})
                if((flags&p.Item1)!=0){int delta=unchecked(a*Math.Abs(p.Item2))/p.Item3; a=unchecked(a+(p.Item2>0?delta:-delta));}
            if((flags&0x40000)!=0)d=unchecked(d-d/2);
            if((flags&0x800000)!=0)d=unchecked(d+d/2);
            if((flags&1)!=0)d=0;
            int n=unchecked(a-d);
            if((flags&4)!=0)n=unchecked(n+n/4);
            if((flags&0x4000)!=0)n/=2;
            if((flags&0x1000000)!=0)n=unchecked(n+a0/4);
            return Math.Max(0,unchecked(n+99)/100);
        }
        public static string FlagHelp => "Known damage flags: 0x1 ignore DF; 0x2 baseline DF; 0x18 baseline AT; 0x20/40/200 add 25/50/100% baseline AT; 0x80/100 subtract 25/50% baseline AT; 0x400/800 add 25/50% contextual AT; 0x2000000/4000000/8000000 add 30/35/40% contextual AT; 0x1000/2000 subtract 25/50% contextual AT; 0x40000 halve DF; 0x800000 add 50% DF; 0x4 add 25% damage; 0x4000 halve damage; 0x1000000 add 25% baseline AT after subtraction. Other bits include hit parameters. Ordinary postprocessing has a minimum 10 and current-PP cap; abilities, immunity, event gates and RNG are additional stages.";
    }
}
