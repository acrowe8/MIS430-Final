using api.Services;

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

builder.Services.AddControllers();
builder.Services.AddSingleton<CourseService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
