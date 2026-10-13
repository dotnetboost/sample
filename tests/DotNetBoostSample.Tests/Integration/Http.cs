using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DotNetBoostSample.Tests.Integration;

/// <summary>
/// The small amount of HTTP plumbing the integration tests need, so a test reads as the exchange
/// it is making rather than as request construction.
///
/// <para>Responses are read as <see cref="JsonElement"/> rather than into DTOs on purpose. Several
/// of these tests are about the SHAPE the API sends — that a listing carries no <c>role</c>, that
/// a settings group grew a field — and a typed model would quietly paper over exactly the
/// difference being asserted.</para>
/// </summary>
internal static class Http
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>A GET, read in <paramref name="language"/> — the header the whole sample turns on.</summary>
    public static Task<HttpResponseMessage> GetAsync(
        this HttpClient client, string url, string? language = null) =>
        client.SendAsync(Request(HttpMethod.Get, url, language));

    /// <summary>A GET whose body is expected to be JSON, with a non-success status failing loudly.</summary>
    public static async Task<JsonElement> GetJsonAsync(
        this HttpClient client, string url, string? language = null)
    {
        var response = await client.GetAsync(url, language);
        return await response.ReadJsonAsync(url);
    }

    /// <summary>A write carrying a JSON body, returned unchecked so a test can assert the status.</summary>
    public static Task<HttpResponseMessage> SendJsonAsync(
        this HttpClient client,
        HttpMethod method,
        string url,
        object? body = null,
        string? language = null,
        string? ifMatch = null)
    {
        var request = Request(method, url, language);

        if (body is not null) request.Content = JsonContent.Create(body, options: Options);
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        return client.SendAsync(request);
    }

    /// <summary>A write that is expected to succeed, with its JSON body.</summary>
    public static async Task<JsonElement> SendJsonCheckedAsync(
        this HttpClient client,
        HttpMethod method,
        string url,
        object? body = null,
        string? language = null)
    {
        var response = await client.SendJsonAsync(method, url, body, language);
        return await response.ReadJsonAsync($"{method} {url}");
    }

    /// <summary>A POST that is expected to succeed and whose body is not needed.</summary>
    public static async Task PostAsJsonChecked(this HttpClient client, string url, object body)
    {
        var response = await client.SendJsonAsync(HttpMethod.Post, url, body);
        await response.EnsureOkAsync($"POST {url}");
    }

    /// <summary>
    /// Sends a write that answers with <b>no body</b>, failing with the API's own sentence if it
    /// refused. DotNetBoost.MultiLingual's translation <c>PUT</c> answers <c>204</c>, so
    /// <see cref="SendJsonCheckedAsync"/> cannot be used for it — there is no JSON to read.
    /// </summary>
    public static async Task SendJsonOkAsync(
        this HttpClient client,
        HttpMethod method,
        string url,
        object? body = null,
        string? language = null)
    {
        var response = await client.SendJsonAsync(method, url, body, language);
        await response.EnsureOkAsync($"{method} {url}");
    }

    /// <summary>
    /// Uploads a small real PNG. Real rather than random bytes because the media library sniffs
    /// the content type from the leading bytes and refuses what it cannot recognise.
    /// </summary>
    public static async Task<JsonElement> UploadAsync(
        this HttpClient client, string url, string fileName = "test.png")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png.OnePixel);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", fileName);

        var response = await client.PostAsync(url, form);
        return await response.ReadJsonAsync($"POST {url}");
    }

    /// <summary>The JSON body, after asserting the call succeeded.</summary>
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response, string what)
    {
        await response.EnsureOkAsync(what);

        var json = await response.Content.ReadAsStringAsync();

        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>
    /// Fails with the API's own body rather than with a bare status code — these endpoints answer
    /// refusals in problem details with a sentence in them, and that sentence is the useful part
    /// of a failing test.
    /// </summary>
    public static async Task EnsureOkAsync(this HttpResponseMessage response, string what)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();

        throw new HttpRequestException(
            $"{what} answered {(int)response.StatusCode} {response.StatusCode}: {body}");
    }

    /// <summary>
    /// The whole RFC 7807 body of a refusal, <b>without</b> asserting success — which is what
    /// separates this from <see cref="ReadJsonAsync"/>, and the thing to reach for when the
    /// non-success status is the point of the test.
    /// </summary>
    public static async Task<JsonElement> ProblemJsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    /// <summary>The <c>detail</c> of an RFC 7807 body — where every refusal here puts its sentence.</summary>
    public static async Task<string> ProblemDetailAsync(this HttpResponseMessage response)
    {
        var json = await response.ProblemJsonAsync();

        return json.TryGetProperty("detail", out var detail) ? detail.GetString() ?? "" : "";
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string? language)
    {
        var request = new HttpRequestMessage(method, url);

        if (language is not null)
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));

        return request;
    }

    /// <summary>
    /// Creates a product of this test's own, and answers its id.
    ///
    /// <para>Tests in this collection share one database, so reaching for a seeded row is how two
    /// of them end up fighting: <c>primary</c> holds ONE file, so a second test attaching to the
    /// same product silently replaces the first test's image and the assertions drift apart
    /// depending on the order they ran in. A row per test costs a round trip and removes the
    /// whole class of problem.</para>
    ///
    /// <para><paramref name="categoryId"/> is passed in rather than defaulted to <c>1</c>, which
    /// is what it was. An extension on <see cref="HttpClient"/> cannot reach the fixture, so a
    /// default here would be this helper quietly assuming the seed's insert order on behalf of
    /// every test that calls it — the assumption <c>SampleApp.SeededCategoryId</c> exists to stop
    /// anyone making. Required, so adding a caller is a question rather than an inheritance.</para>
    /// </summary>
    /// <param name="client">The application's client.</param>
    /// <param name="categoryId">The category to file it under — <c>SampleApp.SeededCategoryId</c>.</param>
    /// <param name="name">Its English name, which several assertions read back.</param>
    public static async Task<int> CreateProductAsync(
        this HttpClient client, int categoryId, string name = "Test product")
    {
        var created = await client.SendJsonCheckedAsync(HttpMethod.Post, "/api/products", new
        {
            sku = $"T-{Guid.NewGuid():N}"[..12],
            price = 1.0m,
            categoryId,
            translations = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Name"] = name, ["Description"] = "Created by a test." }
            }
        });

        return created.Int("id");
    }

    /// <summary>Convenience for the many assertions that read one string out of a response.</summary>
    public static string Str(this JsonElement element, string property) =>
        element.GetProperty(property).GetString() ?? "";

    /// <summary>Convenience for the many assertions that read one number out of a response.</summary>
    public static int Int(this JsonElement element, string property) =>
        element.GetProperty(property).GetInt32();

    /// <summary>The property names an object actually carries — for the shape assertions.</summary>
    public static HashSet<string> Keys(this JsonElement element) =>
        [.. element.EnumerateObject().Select(p => p.Name)];

    public static bool IsNotFound(this HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.NotFound;
}

/// <summary>A 1×1 PNG, as bytes, so the tests need no fixture file on disk.</summary>
internal static class Png
{
    public static readonly byte[] OnePixel =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];
}
