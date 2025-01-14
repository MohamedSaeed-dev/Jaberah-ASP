using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Jaberah.Middlewares;
using Jaberah.Models.MyDbContext;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<JaberahDBContext>(x => x.UseSqlServer(builder.Configuration.GetConnectionString("DB")));
builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddScoped<VerifyTokenAttribute>();

builder.Services.AddMemoryCache();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var key = Encoding.UTF8.GetBytes(builder.Configuration["TokenKey"]!);

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});
var serviceAccountFilePath = builder.Configuration["FCM:ServiceAccountFilePath"];
if (string.IsNullOrEmpty(serviceAccountFilePath))
{
    throw new InvalidOperationException("Firebase service account file path is not configured.");
}
Console.WriteLine(serviceAccountFilePath);
FirebaseApp.Create(new AppOptions()
{
    Credential = GoogleCredential.FromFile(builder.Configuration["FCM:ServiceAccountFilePath"]),
});

builder.Services.AddAuthorization();

builder.Services.AddSwaggerGen(sw =>
{
    sw.SwaggerDoc("v1", new OpenApiInfo { Title = "Jaberah API", Version = "V1" });
    sw.EnableAnnotations();
    sw.OrderActionsBy(a => a.GroupName);
    sw.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'bearer' [space] and then your token in the text box below.\r\n\r\nExample: \"bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9......\"",

    });
    sw.AddSecurityRequirement(new OpenApiSecurityRequirement
      {
         {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
         }
      });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseRouting();
app.UseCors(
    x => x.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod()
    );
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
