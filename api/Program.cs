using ClausesExtractor.Api.Swagger;
using ClausesExtractor.Api.Models;

var builder = WebApplication.CreateBuilder(args);

var isHeroku = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DYNO"));

// Add services to the container.
builder.Services.AddOpenApi();
// Swagger (Swashbuckle)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<ExampleResponseOperationFilter>();
    // Include XML comments
    var xmlFilename = System.IO.Path.ChangeExtension(System.Reflection.Assembly.GetExecutingAssembly().Location, ".xml");
    if (System.IO.File.Exists(xmlFilename)) options.IncludeXmlComments(xmlFilename);
});

// Add HttpClient factory for outbound requests
builder.Services.AddHttpClient();
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{

}

app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapControllers();

if (isHeroku)
{
    var port = Environment.GetEnvironmentVariable("APP_PORT") ?? "3000";
    app.Run($"http://*:{port}");
}
else
{
    app.Run();
}
