namespace CMCStudio;
internal static class PreviewNavigation {
 internal static (double Yaw,double Pitch) Drag(double yaw,double pitch,double dx,double dy)=>((((yaw+dx*.6+180)%360+360)%360)-180,Math.Clamp(pitch+dy*.6,-80,80));
 internal static double Zoom(double zoom,int delta)=>Math.Clamp(zoom*Math.Pow(1.15,delta/120.0),.25,3);
}
