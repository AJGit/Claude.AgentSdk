using Claude.AgentSdk.SignalR.Hubs;
using Claude.AgentSdk.SignalR.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add SignalR services
builder.Services.AddSignalR(options =>
{
    // Enable detailed errors for debugging
    options.EnableDetailedErrors = true;

    // Increase timeouts for long-running operations
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// Add session manager
builder.Services.AddSingleton<ISessionManager, SessionManager>();

// Add CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

WebApplication app = builder.Build();

// Enable static files for the demo client
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();

// Map SignalR hub
app.MapHub<ClaudeAgentHub>("/claude");

// Health check endpoint
app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });

Console.WriteLine(@"
==============================================
   Claude Agent SDK - SignalR Example
==============================================

This example demonstrates wrapping the Claude Agent SDK
with SignalR for real-time web communication.

Endpoints:
  - Hub:    http://localhost:5000/claude
  - Client: http://localhost:5000/index.html
  - Health: http://localhost:5000/health

Hub Methods:
  - Query(prompt, options?)     - One-shot streaming query
  - StartSession(options?)      - Start bidirectional session
  - SendMessage(content)        - Send message in session
  - Interrupt()                 - Interrupt current operation
  - EndSession()                - End session
  - SetModel(model)             - Change model
  - SetPermissionMode(mode)     - Change permission mode

Client Events:
  - ReceiveMessage(message)     - Receive messages from Claude

Press Ctrl+C to stop the server.
");

app.Run("http://localhost:5000");
