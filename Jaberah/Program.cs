using Jaberah.Models.MyDbContext;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<JaberahDBContext>(x => x.UseSqlServer(builder.Configuration.GetConnectionString("DB")));
builder.Services.AddAutoMapper(typeof(Program));
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseRouting();
app.UseCors();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseSwagger().UseSwaggerUI(sw =>
{
    sw.SwaggerEndpoint("/swagger/v1/swagger.json", " Jaberah API");
    sw.RoutePrefix = string.Empty;
    sw.DefaultModelsExpandDepth(-1);
    sw.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    sw.DisplayRequestDuration();
    //sw.EnableFilter("");
});
app.Run();
