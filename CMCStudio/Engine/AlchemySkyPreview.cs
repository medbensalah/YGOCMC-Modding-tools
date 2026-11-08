using System.Numerics;
namespace CMCStudio.Engine;

public sealed partial class AlchemyModel
{
    // Fit a sphere to each section, including sections that do not enclose the board.
    internal static bool SkySurface(ModelMesh mesh, float boardSize)
    {
        var points = mesh.Positions;
        if (points.Length < 8) return false;
        var min = points.Aggregate(Vector3.Min); var max = points.Aggregate(Vector3.Max);
        var span = max - min;
        if (Math.Min(span.X, Math.Min(span.Y, span.Z)) < 10) return false;
        // Normalize before solving to avoid precision loss on large sky geometry.
        var origin = (min + max) / 2; double scale = span.Length();
        var matrix = new double[4,5];
        foreach (var point in points)
        {
            var p = (point - origin) / (float)scale;
            double[] row = { 2*p.X, 2*p.Y, 2*p.Z, 1 };
            double value = p.LengthSquared();
            for (int i=0;i<4;i++) { for(int j=0;j<4;j++) matrix[i,j]+=row[i]*row[j]; matrix[i,4]+=row[i]*value; }
        }
        for(int col=0;col<4;col++)
        {
            int pivot=col;for(int i=col+1;i<4;i++)if(Math.Abs(matrix[i,col])>Math.Abs(matrix[pivot,col]))pivot=i;
            if(Math.Abs(matrix[pivot,col])<1e-9)return false;
            for(int j=col;j<5;j++)(matrix[col,j],matrix[pivot,j])=(matrix[pivot,j],matrix[col,j]);
            double divisor=matrix[col,col];for(int j=col;j<5;j++)matrix[col,j]/=divisor;
            for(int i=0;i<4;i++)if(i!=col){double factor=matrix[i,col];for(int j=col;j<5;j++)matrix[i,j]-=factor*matrix[col,j];}
        }
        var center = origin + new Vector3((float)matrix[0,4],(float)matrix[1,4],(float)matrix[2,4])*(float)scale;
        var radii=points.Select(p=>(double)Vector3.Distance(p,center)).ToArray();double radius=radii.Average();
        if(radius < Math.Max(90,boardSize*.65) || radius>10000)return false;
        double error=Math.Sqrt(radii.Average(r=>(r-radius)*(r-radius)))/radius;
        // Several stock skies are flattened or have clipped poles. Only relax
        // the fit for geometry spanning most of the board in all three axes.
        bool dome=span.X>boardSize*.9f && span.Z>boardSize*.9f && span.Y>boardSize*.65f;
        return error < .025 || (dome && error < .13);
    }
    // Preview copy only; imported/exported geometry is never modified.
    public AlchemyModel WithoutSky(int columns, int rows)
    {
        var kept=Meshes.Where(m=>!SkySurface(m,Math.Min(columns,rows)*10)).ToList();
        if(kept.Count==0 || kept.Count==Meshes.Count)return this;
        var preview=new AlchemyModel(Bytes);preview.Meshes.AddRange(kept);return preview;
    }
}


