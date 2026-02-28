using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using PPE_Detection_App.Api.Models;

namespace PPE_Detection_App.Api.Services
{
    public class YoloV8Processor
    {
        private readonly InferenceSession _session;

        private readonly string[] _classLabels = new[]
        {
            "Fall-Detected", "Gloves", "Goggles", "Hardhat", "Ladder", "Mask",
            "NO-Gloves", "NO-Goggles", "NO-Hardhat", "NO-Mask", "NO-Safety Vest",
            "Person", "Safety Cone", "Safety Vest"
        };

        private const float DefaultConfidenceThreshold = 0.25f;
        private const float DefaultNmsThreshold = 0.5f;
        private const int ModelWidth = 640;
        private const int ModelHeight = 640;

        public YoloV8Processor(InferenceSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Console.WriteLine($"YOLOv8 Processor initialized with {_classLabels.Length} classes");
        }

        public string[] GetClassLabels() => _classLabels;

        public IEnumerable<DetectionResult> ProcessImage(Image image)
        {
            return ProcessImageWithThresholds(image, DefaultConfidenceThreshold, DefaultNmsThreshold);
        }

        public IEnumerable<DetectionResult> ProcessImageWithThresholds(Image image, float confidenceThreshold, float nmsThreshold)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));

            var originalWidth = image.Width;
            var originalHeight = image.Height;

            var inputTensor = PreprocessImage(image);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };

            using var results = _session.Run(inputs);
            var outputTensor = results.FirstOrDefault()?.AsTensor<float>();

            if (outputTensor == null) return Enumerable.Empty<DetectionResult>();

            return Postprocess(outputTensor, originalWidth, originalHeight, confidenceThreshold, nmsThreshold);
        }

        private DenseTensor<float> PreprocessImage(Image image)
        {
            using var imageRgba32 = image.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
            imageRgba32.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(ModelWidth, ModelHeight),
                Mode = ResizeMode.Pad,
                PadColor = SixLabors.ImageSharp.Color.Black
            }));

            var tensor = new DenseTensor<float>(new[] { 1, 3, ModelHeight, ModelWidth });

            imageRgba32.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        var pixel = pixelRow[x];
                        tensor[0, 0, y, x] = pixel.R / 255.0f;
                        tensor[0, 1, y, x] = pixel.G / 255.0f;
                        tensor[0, 2, y, x] = pixel.B / 255.0f;
                    }
                }
            });
            return tensor;
        }

        private IEnumerable<DetectionResult> Postprocess(Tensor<float> output, int originalWidth, int originalHeight, float confidenceThreshold, float nmsThreshold)
        {
            var predictions = new List<float[]>();
            var numPredictions = output.Dimensions[2];
            var numClassesPlusBox = output.Dimensions[1];

            for (int i = 0; i < numPredictions; i++)
            {
                var prediction = new float[numClassesPlusBox];
                for (int j = 0; j < numClassesPlusBox; j++)
                {
                    prediction[j] = output[0, j, i];
                }
                predictions.Add(prediction);
            }

            var results = new List<DetectionResult>();
            var scaleX = (float)originalWidth / ModelWidth;
            var scaleY = (float)originalHeight / ModelHeight;

            foreach (var prediction in predictions)
            {
                var centerX = prediction[0];
                var centerY = prediction[1];
                var width = prediction[2];
                var height = prediction[3];

                var maxScore = 0.0f;
                var labelIndex = -1;

                for (int i = 4; i < prediction.Length; i++)
                {
                    if (prediction[i] > maxScore)
                    {
                        maxScore = prediction[i];
                        labelIndex = i - 4;
                    }
                }

                if (maxScore < confidenceThreshold || labelIndex < 0 || labelIndex >= _classLabels.Length)
                    continue;

                var x = (centerX - width / 2) * scaleX;
                var y = (centerY - height / 2) * scaleY;
                var boxWidth = width * scaleX;
                var boxHeight = height * scaleY;

                x = Math.Max(0, Math.Min(x, originalWidth));
                y = Math.Max(0, Math.Min(y, originalHeight));
                boxWidth = Math.Min(boxWidth, originalWidth - x);
                boxHeight = Math.Min(boxHeight, originalHeight - y);

                results.Add(new DetectionResult(_classLabels[labelIndex], maxScore, new BoundingBox(x, y, boxWidth, boxHeight)));
            }

            return ApplyNms(results, nmsThreshold);
        }

        private IEnumerable<DetectionResult> ApplyNms(List<DetectionResult> results, float nmsThreshold)
        {
            var finalResults = new List<DetectionResult>();
            results = results.OrderByDescending(r => r.Confidence).ToList();

            while (results.Count > 0)
            {
                var current = results[0];
                finalResults.Add(current);
                results.RemoveAt(0);

                results = results.Where(r => r.Label != current.Label || CalculateIoU(current.Box, r.Box) < nmsThreshold).ToList();
            }
            return finalResults;
        }

        private float CalculateIoU(BoundingBox boxA, BoundingBox boxB)
        {
            var xA = Math.Max(boxA.Left, boxB.Left);
            var yA = Math.Max(boxA.Top, boxB.Top);
            var xB = Math.Min(boxA.Right, boxB.Right);
            var yB = Math.Min(boxA.Bottom, boxB.Bottom);

            var interWidth = Math.Max(0, xB - xA);
            var interHeight = Math.Max(0, yB - yA);
            var interArea = interWidth * interHeight;

            var boxAArea = boxA.Width * boxA.Height;
            var boxBArea = boxB.Width * boxB.Height;
            var unionArea = boxAArea + boxBArea - interArea;

            return unionArea > 0 ? interArea / unionArea : 0;
        }
    }
}