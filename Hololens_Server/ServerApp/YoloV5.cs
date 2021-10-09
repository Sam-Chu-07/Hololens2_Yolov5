using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Microsoft.ML;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;

namespace YOLO.DataStructures
{
    class YoloV5
    {
        const string modelPath = @"Assets\best.onnx";
        const string classPath = @"Assets\myObj_80.txt";
        const string outputFolder = @"Assets\Output";
        const float OriginW = 1504.0f;
        const float OriginH = 846.0f;

        const float scale = 640.0f / OriginW;
        const float halfB = (640 - 846.0f * scale) / 2;

        static List<string> classesNames = new List<string>();
        private PredictionEngine<YoloV5BitmapData, YoloV5Prediction> predictionEngine;

        public void InitModel()
        {
            try
            {
                MLContext mlContext = new MLContext();

                // Define scoring pipeline
                var pipeline = mlContext.Transforms.ResizeImages(inputColumnName: "bitmap", outputColumnName: "images", imageWidth: 640, imageHeight: 640, resizing: ResizingKind.Fill)
                    .Append(mlContext.Transforms.ExtractPixels(outputColumnName: "images", scaleImage: 1f / 255f, interleavePixelColors: false))
                    .Append(mlContext.Transforms.ApplyOnnxModel(
                        shapeDictionary: new Dictionary<string, int[]>()
                        {
                        { "images", new[] { 1, 3, 640, 640 } },
                        { "output", new[] { 1, 25200, 85 } },
                        },
                        inputColumnNames: new[]
                        {
                        "images"
                        },
                        outputColumnNames: new[]
                        {
                        "output"
                        },
                        modelFile: modelPath,
                        gpuDeviceId: 0,
                        fallbackToCpu: false));
                Console.WriteLine("Load Model Successfully");
                Console.WriteLine("< Success Create PipeLine >");

                string text = System.IO.File.ReadAllText(classPath);
                using (var stringReader = new StringReader(text))
                {
                    string line = string.Empty;
                    string content = string.Empty;
                    char[] charToTrim = { '\"', ' ', ',', '\'' };
                    while (stringReader.Peek() >= 0)
                    {
                        line = stringReader.ReadLine();
                        content = line.Trim(charToTrim);
                        classesNames.Add(content);
                    }
                }
                Console.WriteLine($"Total class: {classesNames.Count}");

                // Fit on empty list to obtain input data schema
                var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<YoloV5BitmapData>()));

                // Create prediction engine
                predictionEngine = mlContext.Model.CreatePredictionEngine<YoloV5BitmapData, YoloV5Prediction>(model);
                
                Console.WriteLine("< Initialize predictionEngine >");
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return;
            }   
        }

        public IReadOnlyList<YoloResult> predictBitmap(Bitmap bitmap)
        {
            try
            {
                var predict = predictionEngine.Predict(new YoloV5BitmapData() { Image = bitmap });
                var results = predict.GetResults(classesNames.ToArray(), 0.3f, 0.5f);
                return results;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Predict Bitmap Error: {ex.Message}");
                return null;
            }
        }


    }
}
