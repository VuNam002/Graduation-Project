using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using PPE_Detection_App.Api.Models;
using System.Text.RegularExpressions;

namespace PPE_Detection_App.Api.Services
{
    public class YoloModelProvider : IDisposable
    {
        public InferenceSession SessionV8 { get; }
        public InferenceSession SessionV11 { get; }

        public YoloModelProvider(IWebHostEnvironment env, ILogger<YoloModelProvider> logger)
        {
            var possibleV8Paths = new[]
            {
                Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "..", "AITooling", "yolo_model", "best.onnx")),
                Path.GetFullPath(Path.Combine(env.ContentRootPath, "AITooling", "yolo_model", "best.onnx")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "AITooling", "yolo_model", "best.onnx"))
            };

            var possibleV11Paths = new[]
            {
                Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "..", "AITooling", "Yolo11_Training", "weights", "best.onnx")),
                Path.GetFullPath(Path.Combine(env.ContentRootPath, "AITooling", "Yolo11_Training", "weights", "best.onnx")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "AITooling", "Yolo11_Training", "weights", "best.onnx"))
            };

            var v8Path = possibleV8Paths.FirstOrDefault(File.Exists) ?? possibleV8Paths[0];
            var v11Path = possibleV11Paths.FirstOrDefault(File.Exists) ?? possibleV11Paths[0];

            logger.LogInformation($"Loading YOLOv8 from: {v8Path}");
            SessionV8 = new InferenceSession(v8Path);

            if (File.Exists(v11Path))
            {
                try
                {
                    logger.LogInformation($"Loading YOLOv11 from: {v11Path}");
                    SessionV11 = new InferenceSession(v11Path);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Lỗi khi tải mô hình YOLOv11 (File có thể bị hỏng, lỗi Git LFS, hoặc sai định dạng). Hệ thống sẽ tự động dùng YOLOv8 dự phòng.");
                    SessionV11 = SessionV8;
                }
            }
            else
            {
                logger.LogWarning($"YOLOv11 model not found. Fallback to YOLOv8.");
                logger.LogWarning("Hệ thống đã tìm YOLOv11 tại các đường dẫn sau nhưng không thấy:");
                foreach (var p in possibleV11Paths) logger.LogWarning($"- {p}");
                SessionV11 = SessionV8;
            }
        }

        public void Dispose()
        {
            SessionV8?.Dispose();
            if (SessionV11 != null && SessionV11 != SessionV8) SessionV11.Dispose();
        }
    }

    public class YoloV8Processor
    {
        private readonly YoloModelProvider _modelProvider;
        private readonly Dictionary<string, string[]> _classLabelsByModel = new(StringComparer.OrdinalIgnoreCase)
        {
            {
                "YOLOv8", new[]
                {             
                    "NO-Hardhat",       
                    "NO-Mask",          
                    "NO-Safety Vest",   
                    "Machinery",
                    "Person"
                }
            },
            {
                "YOLOv11", new[]
                {
                    "Fall-Detected",    
                    "Gloves",           
                    "Goggles",          
                    "Hardhat",         
                    "Ladder",           
                    "Mask",             
                    "NO-Gloves",        
                    "NO-Goggles",       
                    "NO-Hardhat",       
                    "NO-Mask",          
                    "NO-Safety Vest",   
                    "Person",               
                    "Safety Vest"       
                }
            }
        };

        private readonly Dictionary<string, string[]> _ppeClassMapping = new(StringComparer.OrdinalIgnoreCase)
        {
            { "helmet", new[] { "Hardhat" } },
            { "vest",   new[] { "Safety Vest" } },
            { "mask",   new[] { "Mask" } }
        };

        public const float DefaultConfidenceThreshold = 0.3f;
        public const float DefaultNmsThreshold = 0.5f;
        private const int ModelWidth = 640;
        private const int ModelHeight = 640;
        private readonly Dictionary<string, string[]> _dynamicClassLabels = new(StringComparer.OrdinalIgnoreCase);

        public YoloV8Processor(YoloModelProvider modelProvider)
        {
            _modelProvider = modelProvider ?? throw new ArgumentNullException(nameof(modelProvider));
            
            _dynamicClassLabels["YOLOv8"] = ExtractLabels(_modelProvider.SessionV8) ?? _classLabelsByModel["YOLOv8"];
            if (_modelProvider.SessionV11 != null)
            {
                _dynamicClassLabels["YOLOv11"] = ExtractLabels(_modelProvider.SessionV11) ?? _classLabelsByModel["YOLOv11"];
            }
        }

        private string[]? ExtractLabels(InferenceSession session)
        {
            if (session == null) return null;
            try
            {
                if (session.ModelMetadata.CustomMetadataMap.TryGetValue("names", out string namesStr))
                {
                    var dict = new Dictionary<int, string>();
                    int maxIndex = -1;
                    var matches = Regex.Matches(namesStr, @"(\d+)\s*:\s*['""]([^'""]+)['""]");
                    foreach (Match match in matches)
                    {
                        if (int.TryParse(match.Groups[1].Value, out int idx))
                        {
                            dict[idx] = match.Groups[2].Value;
                            if (idx > maxIndex) maxIndex = idx;
                        }
                    }
                    if (dict.Count > 0)
                    {
                        var labels = new string[maxIndex + 1];
                        for (int i = 0; i <= maxIndex; i++)
                        {
                            labels[i] = dict.ContainsKey(i) ? dict[i] : "unknown";
                        }
                        return labels;
                    }
                }
            }
            catch { }
            return null;
        }

        public string[] GetClassLabels(string activeModel = "YOLOv8")
        {
            if (_dynamicClassLabels.TryGetValue(activeModel, out var dynLabels) && dynLabels != null)
            {
                return dynLabels;
            }
            return _classLabelsByModel.TryGetValue(activeModel, out var fallback) ? fallback : _classLabelsByModel["YOLOv8"];
        }

        public IEnumerable<DetectionResult> ProcessImage(Image image, string activeModel = "YOLOv8")
        {
            return ProcessImageWithThresholds(image, DefaultConfidenceThreshold, DefaultNmsThreshold, activeModel);
        }

        public IEnumerable<DetectionResult> ProcessImageWithThresholds(Image image, float confidenceThreshold, float nmsThreshold, string activeModel = "YOLOv8")
        {
            if (image == null) throw new ArgumentNullException(nameof(image));

            var session = activeModel == "YOLOv11" ? _modelProvider.SessionV11 : _modelProvider.SessionV8;
            var classLabels = GetClassLabels(activeModel); 
            var originalWidth = image.Width;
            var originalHeight = image.Height;

            var inputTensor = PreprocessImage(image);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };

            using var results = session.Run(inputs);
            var outputTensor = results.FirstOrDefault()?.AsTensor<float>();

            if (outputTensor == null) return Enumerable.Empty<DetectionResult>();

            return Postprocess(outputTensor, originalWidth, originalHeight, confidenceThreshold, nmsThreshold, classLabels);
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

        private IEnumerable<DetectionResult> Postprocess(
            Tensor<float> output, int originalWidth, int originalHeight,
            float confidenceThreshold, float nmsThreshold,
            string[] classLabels) 
        {
            if (output.Dimensions.Length < 3) return Enumerable.Empty<DetectionResult>();

            int dim1 = output.Dimensions[1];
            int dim2 = output.Dimensions[2];

            int numPredictions = dim1 > dim2 ? dim1 : dim2;
            int numClassesPlusBox = dim1 > dim2 ? dim2 : dim1;
            bool isTransposed = dim1 > dim2;

            var predictions = new List<float[]>();
            for (int i = 0; i < numPredictions; i++)
            {
                var prediction = new float[numClassesPlusBox];
                for (int j = 0; j < numClassesPlusBox; j++)
                {
                    prediction[j] = isTransposed ? output[0, i, j] : output[0, j, i];
                }
                predictions.Add(prediction);
            }

            var results = new List<DetectionResult>();

            float gain = Math.Min((float)ModelWidth / originalWidth, (float)ModelHeight / originalHeight);
            float padX = (ModelWidth - originalWidth * gain) / 2.0f;
            float padY = (ModelHeight - originalHeight * gain) / 2.0f;

            foreach (var prediction in predictions)
            {
                var centerX = prediction[0];
                var centerY = prediction[1];
                var width = prediction[2];
                var height = prediction[3];

                var boxWidth = width / gain;
                var boxHeight = height / gain;
                var x = (centerX - padX) / gain - boxWidth / 2;
                var y = (centerY - padY) / gain - boxHeight / 2;

                x = Math.Max(0, Math.Min(x, originalWidth));
                y = Math.Max(0, Math.Min(y, originalHeight));
                boxWidth = Math.Min(boxWidth, originalWidth - x);
                boxHeight = Math.Min(boxHeight, originalHeight - y);

                int maxClasses = Math.Min(prediction.Length, classLabels.Length + 4);

                for (int i = 4; i < maxClasses; i++)
                {
                    var score = prediction[i];
                    if (score >= confidenceThreshold)
                    {
                        var labelIndex = i - 4;
                        results.Add(new DetectionResult(classLabels[labelIndex], score, new BoundingBox(x, y, boxWidth, boxHeight)));
                    }
                }
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