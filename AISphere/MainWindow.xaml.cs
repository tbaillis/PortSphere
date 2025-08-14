using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace AISphere
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static string McpServerUrl =>
            GetEnvVar("MCP_SERVER_URL")?.TrimEnd('/') ?? "http://localhost:5056";

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var userInput = UserInput.Text;
                if (string.IsNullOrWhiteSpace(userInput)) return;

                // Display user input in chat history
                ChatHistory.Items.Add($"You: {userInput}");
                UserInput.Clear();

                // Call OpenAI API to process input
                var openAiResponse = await CallOpenAiApi(userInput);
                ChatHistory.Items.Add($"AI: {openAiResponse}");

                // Parse and forward commands to MCP server
                try
                {
                    await ForwardToMcpServer(openAiResponse);
                }
                catch (Exception ex)
                {
                    ChatHistory.Items.Add($"(MCP error: {ex.Message})");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ChatHistory.Items.Add($"(Error: {ex.Message})");
            }
        }

    private static string GetEnvVar(string name)
        {
            // Prefer current process for immediate availability, then User, then Machine
            return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)
                   ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
                   ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
        }

        private string BuildSystemInstruction()
        {
            return "You control a 3D sphere app via an MCP HTTP server. Respond ONLY with JSON (no prose). JSON schema: either a single object or array of objects with: {\n  'action': 'set_sphere', 'radius': <number>\n} OR {\n  'action': 'set_light', 'index': <1-4>, 'x': <number>, 'y': <number>, 'z': <number>\n}.";
        }

        private async Task<string> CallOpenAiApi(string input)
        {
            // Retrieve the OpenAI API key from environment variables
            var openAiApiKey = GetEnvVar("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(openAiApiKey))
            {
                MessageBox.Show("OpenAI API key is not set. Please configure the OPENAI_API_KEY environment variable.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return "(Error: API key not set)";
            }

            const string openAiEndpoint = "https://api.openai.com/v1/chat/completions";

            var requestBody = new
            {
                model = "gpt-4o",
                messages = new object[]
                {
                    new { role = "system", content = BuildSystemInstruction() },
                    new { role = "user", content = input }
                },
                max_tokens = 200
            };

            try
            {
                var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAiApiKey);

                var response = await _httpClient.PostAsync(openAiEndpoint, requestContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"(OpenAI error {((int)response.StatusCode)}: {responseContent})";
                }

                try
                {
                    using var jsonResponse = JsonDocument.Parse(responseContent);
                    var root = jsonResponse.RootElement;
                    var choices = root.GetProperty("choices");
                    if (choices.GetArrayLength() == 0)
                        return "(No response)";
                    var content = choices[0].GetProperty("message").GetProperty("content").GetString();
                    return string.IsNullOrWhiteSpace(content) ? "(No response)" : content.Trim();
                }
                catch (Exception jsonEx)
                {
                    return $"(Parse error: {jsonEx.Message})";
                }
            }
            catch (HttpRequestException httpEx)
            {
                return $"(Network error: {httpEx.Message})";
            }
            catch (TaskCanceledException)
            {
                return "(Request timed out)";
            }
        }

        private async Task ForwardToMcpServer(string aiResponse)
        {
            // Prefer JSON commands from the model. Support a single object or an array.
            var trimmed = aiResponse?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && (trimmed.StartsWith("{") || trimmed.StartsWith("[")))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            await ExecuteCommandAsync(item);
                        }
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        await ExecuteCommandAsync(doc.RootElement);
                    }
                    return;
                }
                catch
                {
                    // Fall through to heuristic parsing
                }
            }

            // Heuristic fallback for plain text
            if (aiResponse.Contains("sphere", StringComparison.OrdinalIgnoreCase))
            {
                var size = ExtractNumber(aiResponse);
                if (size.HasValue)
                {
                    var body = JsonSerializer.Serialize(new { radius = size.Value });
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    await _httpClient.PostAsync($"{McpServerUrl}/sphere", content);
                }
            }
            else if (aiResponse.Contains("light", StringComparison.OrdinalIgnoreCase))
            {
                var lightIndex = ExtractNumber(aiResponse);
                if (lightIndex.HasValue && lightIndex >= 1 && lightIndex <= 4)
                {
                    var body = JsonSerializer.Serialize(new { x = 1.0, y = 1.0, z = 1.0 });
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    await _httpClient.PostAsync($"{McpServerUrl}/light/{lightIndex}", content);
                }
            }
        }

        private async Task ExecuteCommandAsync(JsonElement cmd)
        {
            if (!cmd.TryGetProperty("action", out var actionProp) || actionProp.ValueKind != JsonValueKind.String)
                return;

            var action = actionProp.GetString();
            if (string.Equals(action, "set_sphere", StringComparison.OrdinalIgnoreCase))
            {
                if (cmd.TryGetProperty("radius", out var r) && r.TryGetDouble(out var radius))
                {
                    var body = JsonSerializer.Serialize(new { radius });
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    await _httpClient.PostAsync($"{McpServerUrl}/sphere", content);
                }
            }
            else if (string.Equals(action, "set_light", StringComparison.OrdinalIgnoreCase))
            {
                if (cmd.TryGetProperty("index", out var idx) && idx.TryGetInt32(out var index)
                    && index >= 1 && index <= 4)
                {
                    double x = 0, y = 0, z = 0;
                    if (cmd.TryGetProperty("x", out var xEl)) xEl.TryGetDouble(out x);
                    if (cmd.TryGetProperty("y", out var yEl)) yEl.TryGetDouble(out y);
                    if (cmd.TryGetProperty("z", out var zEl)) zEl.TryGetDouble(out z);

                    var body = JsonSerializer.Serialize(new { x, y, z });
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    await _httpClient.PostAsync($"{McpServerUrl}/light/{index}", content);
                }
            }
        }

        private static double? ExtractNumber(string input)
        {
            var match = System.Text.RegularExpressions.Regex.Match(input, "\\d+(\\.\\d+)?");
            return match.Success ? double.Parse(match.Value) : null;
        }

        private void ManageVariablesButton_Click(object sender, RoutedEventArgs e)
        {
            var manager = new EnvironmentVariableManager();
            manager.ShowDialog();
        }
    }
}
