using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.ML.OnnxRuntime;
using Microsoft.OpenApi.Models;
using PPE_Detection_App.Api.Filters;
using PPE_Detection_App.Api.Hubs;
using PPE_Detection_App.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PPE Detection API", Version = "v1" });
    c.OperationFilter<FileUploadOperationFilter>();

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

// Thêm dịch vụ SignalR
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:3000") 
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); 
    });
});

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
builder.Services.AddScoped<NotificationService>();


var modelPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "AITooling", "yolo_model", "best.onnx"));
if (!File.Exists(modelPath)) throw new FileNotFoundException($"Model file not found at: {modelPath}");

builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();

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

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        try
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var socketManager = context.RequestServices.GetRequiredService<WebSocketManagerService>();
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

app.MapHub<NotificationHub>("/notificationHub");

app.Run();