using ExpenseMgmt.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Razor Pages + Controllers (for API endpoints)
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Register services
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<ChatService>();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Expense Management API",
        Version = "v1",
        Description = "REST API for the modernised Expense Management System. All operations use stored procedures. Supports natural language chat via Azure OpenAI."
    });
    // Include XML comments if present
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Error handling middleware
app.UseExceptionHandler("/Error");
app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Swagger UI (available in all environments for easy API testing)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Management API v1");
    c.RoutePrefix = "swagger";
});

app.MapRazorPages();
app.MapControllers();

// Redirect root to /Index
app.MapGet("/", () => Results.Redirect("/Index"));

app.Run();
