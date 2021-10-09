namespace YOLO.DataStructures
{
    public class YoloResult
    {
        public float[] BBox { get; }
        public string Label { get; }
        public float Confidence { get; }
        public YoloResult(float[] bbox, string label, float confidence)
        {
            BBox = bbox;
            Label = label;
            Confidence = confidence;
        }
    }
}
