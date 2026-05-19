namespace DocumentManagement.Intelligence.Models;

public sealed record BoundingBox(double X1, double Y1, double X2, double Y2)
{
    public double Width => Math.Max(0, X2 - X1);

    public double Height => Math.Max(0, Y2 - Y1);

    public double CenterX => X1 + (Width / 2);

    public double CenterY => Y1 + (Height / 2);
}
