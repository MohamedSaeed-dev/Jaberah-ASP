using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

public class DropboxService
{
    private readonly HttpClient _httpClient;
    private readonly string _dropboxClientId;
    private readonly string _dropboxClientSecret;
    private readonly string _dropboxRefreshToken;

    public DropboxService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _dropboxClientId = configuration["Dropbox:clientId"];
        _dropboxClientSecret = configuration["Dropbox:clientSecret"];
        _dropboxRefreshToken = configuration["Dropbox:refreshToken"];
    }

    public async Task<string> RefreshAccessTokenAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.dropbox.com/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", _dropboxRefreshToken)
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_dropboxClientId}:{_dropboxClientSecret}")));

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonResponse);
        return doc.RootElement.GetProperty("access_token").GetString();
    }

    public async Task UploadFileAsync(string accessToken, string filePath, byte[] fileContent)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://content.dropboxapi.com/2/files/upload")
        {
            Content = new ByteArrayContent(fileContent)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Dropbox-API-Arg", JsonSerializer.Serialize(new { path = filePath, mode = "overwrite" }));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> GetSharableLinkAsync(string accessToken, string filePath)
    {
        var listRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/2/sharing/list_shared_links")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { path = filePath }), Encoding.UTF8, "application/json")
        };
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var listResponse = await _httpClient.SendAsync(listRequest);
        var jsonResponse = await listResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonResponse);

        if (doc.RootElement.TryGetProperty("links", out var links) && links.GetArrayLength() > 0)
        {
            return links[0].GetProperty("url").GetString();
        }

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/2/sharing/create_shared_link_with_settings")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { path = filePath, settings = new { requested_visibility = "public" } }), Encoding.UTF8, "application/json")
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await _httpClient.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        jsonResponse = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(jsonResponse);
        return createDoc.RootElement.GetProperty("url").GetString();
    }
}
