namespace PPE_Detection_App.Api.Models
{
    public record DetectionResult(string Label, float Confidence, BoundingBox Box);

    public record BoundingBox(float X, float Y, float Width, float Height)
    {
        public float Left => X;
        public float Top => Y;
        public float Right => X + Width;
        public float Bottom => Y + Height;
    }
}