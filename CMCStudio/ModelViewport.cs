using System.Windows.Controls;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CMCStudio.Engine;
namespace CMCStudio;

public sealed class ModelViewport : Viewport3D
{
    readonly PerspectiveCamera camera = new(new Point3D(0, 0, 4), new Vector3D(0, 0, -4), new Vector3D(0, 1, 0), 40); readonly ModelVisual3D visual = new(); readonly AxisAngleRotation3D yaw = new(new Vector3D(0, 1, 0), 0), pitch = new(new Vector3D(1, 0, 0), 0); AlchemyModel loaded; int selected = -2; Func<ModelMesh,bool> meshFilter;
    public ModelViewport() { Camera = camera; ClipToBounds = true; var light = new Model3DGroup(); light.Children.Add(new AmbientLight(Color.FromRgb(75, 85, 100))); light.Children.Add(new DirectionalLight(Color.FromRgb(235, 255, 249), new Vector3D(-.5, -.7, -1))); light.Children.Add(new DirectionalLight(Color.FromRgb(130, 160, 190), new Vector3D(1, .3, 1))); Children.Add(new ModelVisual3D { Content = light }); Children.Add(visual); var transform = new Transform3DGroup(); transform.Children.Add(new RotateTransform3D(yaw)); transform.Children.Add(new RotateTransform3D(pitch)); visual.Transform = transform; }
    public void ShowTop(AlchemyModel model,int columns,int rows,bool hideSky=true)
    {
        model=hideSky?model.WithoutSky(columns,rows):model;
        meshFilter=null;
        loaded=null;Show(model,-1,0,0,1);
        var points=model.Meshes.Where(m=>meshFilter==null||meshFilter(m)).SelectMany(m=>m.Positions).ToArray();
        var min=points.Aggregate(System.Numerics.Vector3.Min);var max=points.Aggregate(System.Numerics.Vector3.Max);
        var center=(min+max)/2;double radius=Math.Max((max-min).Length()/2,.001);
        Camera=new OrthographicCamera(new Point3D((columns*5-center.X)/radius,(max.Y-center.Y)/radius+3,(rows*5-center.Z)/radius),new Vector3D(0,-1,0),new Vector3D(0,0,-1),columns*10/radius){NearPlaneDistance=.01,FarPlaneDistance=100};
    }
    public void Show(AlchemyModel model, int part, double y, double p, double zoom)
    {
        yaw.Angle = y; pitch.Angle = p; camera.Position = new Point3D(0, 0, 4 / zoom); camera.LookDirection = new Vector3D(0, 0, -4 / zoom); if (ReferenceEquals(loaded, model) && selected == part) return; loaded = model; selected = part;
        var meshes = model.Meshes.Where((m, i) => (part < 0 || i == part)&&(meshFilter==null||meshFilter(m))).ToList(); var points = meshes.SelectMany(x => x.Positions).ToList(); var min = points.Aggregate(System.Numerics.Vector3.Min); var max = points.Aggregate(System.Numerics.Vector3.Max); var center = (min + max) / 2; double radius = Math.Max((max - min).Length() / 2, .001); var group = new Model3DGroup(); int index = 0; var palette = new[] { Color.FromRgb(40, 167, 143), Color.FromRgb(90, 148, 211), Color.FromRgb(225, 175, 96), Color.FromRgb(177, 128, 209) };
        Brush diffuseBrush = null; var png = model.DiffusePreviewPng(); if (png != null) { using var stream = new MemoryStream(png); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); diffuseBrush = new ImageBrush(image); diffuseBrush.Freeze(); }
        foreach (var m in meshes) { Brush meshBrush=diffuseBrush; if(m.PreviewTexturePng!=null){using var stream=new MemoryStream(m.PreviewTexturePng);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();meshBrush=new ImageBrush(bitmap){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new System.Windows.Rect(0,0,1,1)};meshBrush.Freeze();} var geo = new MeshGeometry3D(); foreach (var v in m.Positions) { var a = (v - center) / (float)radius; geo.Positions.Add(new Point3D(a.X, a.Y, a.Z)); } int step = 3 * Math.Max(1, (model.TriangleCount + 199999) / 200000); for (int i = 0; i < m.Triangles.Length; i += step) { geo.TriangleIndices.Add(m.Triangles[i]); geo.TriangleIndices.Add(m.Triangles[i + 1]); geo.TriangleIndices.Add(m.Triangles[i + 2]); } if (m.Normals?.Length == m.Positions.Length) foreach (var n in m.Normals) geo.Normals.Add(new Vector3D(n.X, n.Y, n.Z)); if (meshBrush != null && m.Uvs?.Length == m.Positions.Length) foreach (var uv in m.Uvs) geo.TextureCoordinates.Add(new System.Windows.Point(uv.X, uv.Y)); geo.Freeze(); var material = new MaterialGroup(); material.Children.Add(new DiffuseMaterial(meshBrush ?? new SolidColorBrush(palette[index++ % palette.Length]))); material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(110, 145, 140)), 24)); material.Freeze(); group.Children.Add(new GeometryModel3D(geo, material) { BackMaterial = material }); }
        group.Freeze(); visual.Content = group;
    }
}

