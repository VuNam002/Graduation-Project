using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PPE_Detection_App.Api.Services
{
    public class FaceRecognitionService : IDisposable
    {
        private readonly InferenceSession? _session;
        private readonly ILogger<FaceRecognitionService> _logger;

        public FaceRecognitionService(IConfiguration config, ILogger<FaceRecognitionService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            var possiblePaths = new[]
            {
                Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "..", "AITooling", "face_model", "w600k_r50.onnx")),
                Path.GetFullPath(Path.Combine(env.ContentRootPath, "AITooling", "face_model", "w600k_r50.onnx")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "AITooling", "face_model", "w600k_r50.onnx"))
            };

            var modelPath = possiblePaths.FirstOrDefault(File.Exists);

            if (string.IsNullOrEmpty(modelPath))
            {
                _logger.LogWarning("Face model 'w600k_r50.onnx' not found. Face recognition will be disabled.");
                _logger.LogWarning("Hệ thống đã tìm kiếm tại các đường dẫn sau nhưng không thấy:");
                foreach (var p in possiblePaths) _logger.LogWarning($"- {p}");
                return;
            }

            try
            {
                _session = new InferenceSession(modelPath);
                _logger.LogInformation("Face Recognition Model loaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Đã tìm thấy file Model tại {modelPath} nhưng không thể Load được! " +
                                     $"Nguyên nhân có thể do file bị hỏng (0KB), sai định dạng, hoặc máy thiếu thư viện C++.");
                _session = null;
            }
        }

        public float[] GetFaceEmbedding(Image<Rgb24> faceImage)
        {
            if (_session == null) return Array.Empty<float>();
            var resizedImage = faceImage.Clone(x => x.Resize(112, 112));
            var tensor = new DenseTensor<float>(new[] { 1, 3, 112, 112 });

            for (int y = 0; y < 112; y++)
            {
                for (int x = 0; x < 112; x++)
                {
                    var pixel = resizedImage[x, y];
                    tensor[0, 0, y, x] = (pixel.R / 255f - 0.5f) / 0.5f;
                    tensor[0, 1, y, x] = (pixel.G / 255f - 0.5f) / 0.5f;
                    tensor[0, 2, y, x] = (pixel.B / 255f - 0.5f) / 0.5f;
                }
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_session.InputMetadata.Keys.First(), tensor)
            };

            using var results = _session.Run(inputs);
            var embedding = results.First().AsEnumerable<float>().ToArray();

            return embedding;
        }

        public double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length) return 0;

            double dotProduct = 0.0, normA = 0.0, normB = 0.0;
            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }

            if (normA == 0 || normB == 0) return 0;
            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
