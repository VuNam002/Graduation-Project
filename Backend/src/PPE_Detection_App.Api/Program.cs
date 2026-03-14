using Microsoft.AspNetCore.Authentication.JwtBearer;
﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.ML.OnnxRuntime;
using Microsoft.OpenApi.Models;
using PPE_Detection_App.Api.Filters;
using PPE_Detection_App.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PPE Detection API", Version = "v1" });
    c.OperationFilter<FileUploadOperationFilter>();

    // Cấu hình cho Swagger để sử dụng JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Cấu hình JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? throw new Exception("Thiếu Jwt:SecretKey trong cấu hình.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

//Đăng ký Services
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ViolationRepository>();
builder.Services.AddScoped<DashboardStatisticService>();
builder.Services.AddSingleton<WebSocketManagerService>();
builder.Services.AddSingleton<CameraStreamService>();
builder.Services.AddScoped<EmailService>();

var modelPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "AITooling", "yolo_model", "best.onnx"));
if (!File.Exists(modelPath)) throw new FileNotFoundException($"Model file not found at: {modelPath}");

builder.Services.AddSingleton(new InferenceSession(modelPath));
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var sessionOptions = new SessionOptions { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING };

    // Cấu hình ONNX Runtime để ưu tiên sử dụng GPU nếu có thể
    // Ưu tiên 1: CUDA (NVIDIA)
    try
    {
        sessionOptions.AppendExecutionProvider_CUDA(0); // 0 là device ID của GPU
        logger.LogInformation("ONNX Runtime: Using CUDA Execution Provider.");
        return new InferenceSession(modelPath, sessionOptions);
    }
    catch (Exception) { /* Bỏ qua nếu không có CUDA hoặc cài đặt lỗi */ }

    // Ưu tiên 2: DirectML (Windows - Hỗ trợ nhiều loại GPU)
    try
    {
        sessionOptions.AppendExecutionProvider_DML(0); // 0 là device ID của GPU
        logger.LogInformation("ONNX Runtime: Using DirectML Execution Provider.");
        return new InferenceSession(modelPath, sessionOptions);
    }
    catch (Exception) { /* Bỏ qua nếu không có DirectML hoặc cài đặt lỗi */ }

    // Mặc định: CPU
    logger.LogInformation("ONNX Runtime: Using CPU Execution Provider.");
    return new InferenceSession(modelPath);
});
builder.Services.AddScoped<YoloV8Processor>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Cấu hình WebSocket với options
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2),
    ReceiveBufferSize = 4 * 1024
};
app.UseWebSockets(webSocketOptions);

// WebSocket endpoint cho camera streaming
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        try
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var socketManager = context.RequestServices.GetRequiredService<WebSocketManagerService>();

            // Add socket và xử lý connection
            await socketManager.HandleWebSocketAsync(webSocket);
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "WebSocket error occurred");
            context.Response.StatusCode = 500;
        }
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
    }
});

app.MapControllers();

app.Run();