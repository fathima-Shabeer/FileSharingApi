var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen((c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "File Sharing API", Version = "v1" });
}));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "File Sharing API v1"));
    app.UseDeveloperExceptionPage(); // More detailed errors in dev
}
else
{
    // Add more robust error handling for production
    // app.UseExceptionHandler("/error"); // Example: Redirect to an error page/endpoint
    app.UseHsts(); // Use HTTP Strict Transport Security
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
