using api.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Local secrets: load repo-root .env in Development (Heroku uses config vars instead).
if (builder.Environment.IsDevelopment())
{
    var envPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".env"));
    if (File.Exists(envPath))
    {
        DotNetEnv.Env.Load(envPath);
    }
}

// Allow multipart uploads up to 10 MB (assignment files stay in memory only).
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = FileExtractionService.MaxFileBytes;
});

builder.Services.AddControllers();
builder.Services.AddSingleton<CourseService>();
builder.Services.AddSingleton<FileExtractionService>();
builder.Services.AddSingleton<AiService>();
builder.Services.AddSingleton<AnalyzeService>();

// Swagger UI in Development only — handy for uploading a test file from the browser.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
