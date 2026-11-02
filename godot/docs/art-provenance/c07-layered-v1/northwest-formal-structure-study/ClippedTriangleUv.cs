using Godot;

// Offline-only: vertices supplied by Geometry2D's intersection of this triangle.
// The intersection is quantized to Vector2 floats; permit only that coordinate's
// one-ULP uncertainty, never arbitrary extrapolation into a material.
public static class ClippedTriangleUv
{
 public static Vector2 Map(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 ua, Vector2 ub, Vector2 uc)
 {
  if(p==a)return ua;if(p==b)return ub;if(p==c)return uc;
  double vx=(double)b.X-a.X,vy=(double)b.Y-a.Y,wx=(double)c.X-a.X,wy=(double)c.Y-a.Y;
  double qx=(double)p.X-a.X,qy=(double)p.Y-a.Y,den=vx*wy-vy*wx;
  if(den==0||!double.IsFinite(den))throw new ArgumentException("Clipped UV requires a finite nondegenerate source triangle.");
  double wb=(qx*wy-qy*wx)/den,wc=(vx*qy-vy*qx)/den,wa=1-wb-wc;
  double ULP(float x)=>Math.Max(Math.Abs((double)MathF.BitIncrement(x)-x),Math.Abs((double)x-MathF.BitDecrement(x)));
  double ex=ULP(p.X),ey=ULP(p.Y);
  double ba=(Math.Abs(vy-wy)*ex+Math.Abs(wx-vx)*ey)/Math.Abs(den);
  double bb=(Math.Abs(wy)*ex+Math.Abs(wx)*ey)/Math.Abs(den);
  double bc=(Math.Abs(vy)*ex+Math.Abs(vx)*ey)/Math.Abs(den);
  if(!double.IsFinite(wa)||!double.IsFinite(wb)||!double.IsFinite(wc)||wa < -ba||wb < -bb||wc < -bc||wa>1+ba||wb>1+bb||wc>1+bc)
   throw new ArgumentException("Clipped UV point lies outside the source triangle's float-quantization envelope.");
  wa=Math.Clamp(wa,0,1);wb=Math.Clamp(wb,0,1);wc=Math.Clamp(wc,0,1);
  double sum=wa+wb+wc;wa/=sum;wb/=sum;wc/=sum;
  return new((float)(wa*ua.X+wb*ub.X+wc*uc.X),(float)(wa*ua.Y+wb*ub.Y+wc*uc.Y));
 }
}
