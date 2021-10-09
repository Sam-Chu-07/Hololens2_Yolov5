using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;
using System.Drawing;

namespace YOLO.DataStructures
{
    public class YoloV5BitmapData
    {
        [ColumnName("bitmap")]
        [ImageType(640, 640)]
        public Bitmap Image { get; set; }

        [ColumnName("width")]
        public float ImageWidth => Image.Width;

        [ColumnName("height")]
        public float ImageHeight => Image.Height;
    }
}
