using Microsoft.ML.OnnxRuntime;
using Microsoft.OpenApi.Models;
using PPE_Detection_App.Api.Filters;
using PPE_Detection_App.Api.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PPE Detection API", Version = "v1" });
    c.OperationFilter<FileUploadOperationFilter>();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => { policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader(); });
});


builder.Services.AddScoped<DatabaseService>(); 

var modelPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "AITooling", "yolo_model", "best.onnx"));
if (!File.Exists(modelPath)) throw new FileNotFoundException($"Model file not found at: {modelPath}");

builder.Services.AddSingleton(new InferenceSession(modelPath));
builder.Services.AddSingleton<YoloV8Processor>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); 
app.UseCors("AllowAll");

app.MapControllers(); 

app.Run();