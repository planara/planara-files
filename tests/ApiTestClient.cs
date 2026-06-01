using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Planara.Files.Tests;

public static class ApiTestClient
{
    public static async Task<JsonDocument> ReadJsonAsync(
        this HttpResponseMessage response,
        CancellationToken ct = default)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);

        return json ?? throw new InvalidOperationException("Empty JSON response");
    }

    public static void AsUser(this HttpClient client, Guid userId)
    {
        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
    }

    public static MultipartFormDataContent MultipartFile(
        string fieldName,
        string fileName,
        string contentType,
        string body)
    {
        return MultipartFile(
            fieldName,
            fileName,
            contentType,
            Encoding.UTF8.GetBytes(body));
    }

    public static MultipartFormDataContent MultipartFile(
        string fieldName,
        string fileName,
        string contentType,
        byte[] bytes)
    {
        var form = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        form.Add(fileContent, fieldName, fileName);

        return form;
    }
    
    public static MultipartFormDataContent MultipartFileWithoutContentType(
        string fieldName,
        string fileName,
        string body)
    {
        var form = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(body));

        form.Add(fileContent, fieldName, fileName);

        return form;
    }
}