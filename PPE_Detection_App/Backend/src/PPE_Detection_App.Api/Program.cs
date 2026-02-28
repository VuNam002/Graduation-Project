using Microsoft.AspNetCore.Mvc;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "PPE Detection API", 
        Version = "v1",
        Description = "Personal Protective Equipment Detection using YOLOv8"
    });
    c.OperationFilter<FileUploadOperationFilter>();
});
builder.Services.AddAntiforgery();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var modelPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "AITooling", "yolo_model", "best.onnx"));

if (!File.Exists(modelPath))
{
    throw new FileNotFoundException($"Model file not found at: {modelPath}");
}

var session = new InferenceSession(modelPath);
builder.Services.AddSingleton(session);

var processor = new YoloV8Processor(session);
builder.Services.AddSingleton(processor);


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    model = "YOLOv8n - PPE Detection",
    classes = new[] 
    {
        "Fall-Detected", "Gloves", "Goggles", "Hardhat", "Ladder", "Mask",
        "NO-Gloves", "NO-Goggles", "NO-Hardhat", "NO-Mask", "NO-Safety Vest",
        "Person", "Safety Cone", "Safety Vest"
    }
}))
.WithName("HealthCheck")
.WithOpenApi();

app.MapGet("/classes", ([FromServices] YoloV8Processor processor) => 
{
    return Results.Ok(new 
    {
        totalClasses = processor.GetClassLabels().Length,
        classes = processor.GetClassLabels()
    });
})
.WithName("GetClasses")
.WithOpenApi();

app.MapPost("/detect", async (IFormFile file, [FromServices] YoloV8Processor processor) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new 
        { 
            success = false,
            error = "No image file found in request",
            hint = "Please upload an image file using the 'file' field name in Swagger UI",
            acceptedFormats = new[] { "jpg", "jpeg", "png", "bmp", "gif", "webp" }
        });
    }

    try
    {
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
        {
            return Results.BadRequest(new 
            { 
                success = false,
                error = $"Invalid file format: {fileExtension}",
                acceptedFormats = allowedExtensions
            });
        }

        const long maxFileSize = 10 * 1024 * 1024;
        if (file.Length > maxFileSize)
        {
            return Results.BadRequest(new 
            { 
                success = false,
                error = "File size too large",
                maxSize = "10MB",
                uploadedSize = $"{file.Length / (1024.0 * 1024.0):F2}MB"
            });
        }

        using var stream = file.OpenReadStream();
        using var image = await Image.LoadAsync(stream);

        var detections = processor.ProcessImage(image);
        
        // Phân loại detections
        var safetyIssues = detections.Where(d => d.Label.StartsWith("NO-")).ToList();
        var equipment = detections.Where(d => !d.Label.StartsWith("NO-") && d.Label != "Person" && d.Label != "Fall-Detected").ToList();
        var persons = detections.Where(d => d.Label == "Person").ToList();
        var falls = detections.Where(d => d.Label == "Fall-Detected").ToList();
        
        return Results.Ok(new 
        {
            success = true,
            fileName = file.FileName,
            fileSize = $"{file.Length / 1024.0:F2} KB",
            imageWidth = image.Width,
            imageHeight = image.Height,
            summary = new 
            {
                totalDetections = detections.Count(),
                personsDetected = persons.Count,
                safetyIssuesDetected = safetyIssues.Count,
                equipmentDetected = equipment.Count,
                fallsDetected = falls.Count
            },
            detections = detections.Select(d => new 
            {
                label = d.Label,
                confidence = Math.Round(d.Confidence * 100, 2),
                isSafetyIssue = d.Label.StartsWith("NO-"),
                isFallDetected = d.Label == "Fall-Detected",
                box = new 
                {
                    x = Math.Round(d.Box.X, 2),
                    y = Math.Round(d.Box.Y, 2),
                    width = Math.Round(d.Box.Width, 2),
                    height = Math.Round(d.Box.Height, 2)
                }
            }).OrderByDescending(d => d.confidence),
            processedAt = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Image processing failed"
        );
    }
})
.WithName("DetectObjects")
.WithOpenApi()
.Accepts<IFormFile>("multipart/form-data")
.DisableAntiforgery();


app.MapPost("/detect-custom", async (
    IFormFile file,
    [FromQuery] float? confidence,
    [FromQuery] float? nms,
    [FromServices] YoloV8Processor processor) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("No file uploaded");
    }

    try
    {
        using var stream = file.OpenReadStream();
        using var image = await Image.LoadAsync(stream);

        var detections = processor.ProcessImageWithThresholds(
            image, 
            confidence ?? 0.25f, 
            nms ?? 0.5f
        );
        
        return Results.Ok(new 
        {
            success = true,
            fileName = file.FileName,
            thresholds = new 
            {
                confidence = confidence ?? 0.25f,
                nms = nms ?? 0.5f
            },
            detectionCount = detections.Count(),
            detections = detections
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message);
    }
})
.WithName("DetectWithCustomThresholds")
.DisableAntiforgery();

app.Run();




public class FileUploadOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        var fileParams = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile));

        if (fileParams.Any())
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["file"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary"
                                }
                            },
                            Required = new HashSet<string> { "file" }
                        }
                    }
                }
            };
        }
    }
}



public record DetectionResult(string Label, float Confidence, BoundingBox Box);

public record BoundingBox(float X, float Y, float Width, float Height)
{
    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;
}

public class YoloV8Processor
{
    private readonly InferenceSession _session;
    
    private readonly string[] _classLabels = new[]
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
        "Safety Cone",     
        "Safety Vest"       
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

    public IEnumerable<DetectionResult> ProcessImageWithThresholds(
        Image image, 
        float confidenceThreshold, 
        float nmsThreshold)
    {
        if (image == null)
            throw new ArgumentNullException(nameof(image));

        var originalWidth = image.Width;
        var originalHeight = image.Height;

        Console.WriteLine($"Processing image: {originalWidth}x{originalHeight}");

        var inputTensor = PreprocessImage(image);
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };
        
        using var results = _session.Run(inputs);
        var outputTensor = results.FirstOrDefault()?.AsTensor<float>();
        
        if (outputTensor == null) 
        {
            Console.WriteLine("No output from model");
            return Enumerable.Empty<DetectionResult>();
        }

        Console.WriteLine($"Output shape: [{outputTensor.Dimensions[0]}, {outputTensor.Dimensions[1]}, {outputTensor.Dimensions[2]}]");

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
    
    private IEnumerable<DetectionResult> Postprocess(
        Tensor<float> output, 
        int originalWidth, 
        int originalHeight,
        float confidenceThreshold,
        float nmsThreshold)
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
        
        int candidatesCount = 0;
        
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

            if (maxScore > 0.1f)
            {
                candidatesCount++;
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
            
            results.Add(new DetectionResult(
                _classLabels[labelIndex], 
                maxScore, 
                new BoundingBox(x, y, boxWidth, boxHeight)
            ));
        }

        Console.WriteLine($" Candidates (conf > 0.1): {candidatesCount}");
        Console.WriteLine($" After threshold ({confidenceThreshold}): {results.Count}");

        var nmsResults = ApplyNms(results, nmsThreshold);
        Console.WriteLine($" After NMS: {nmsResults.Count()}");

        return nmsResults;
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

            results = results.Where(r => 
                r.Label != current.Label || 
                CalculateIoU(current.Box, r.Box) < nmsThreshold
            ).ToList();
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