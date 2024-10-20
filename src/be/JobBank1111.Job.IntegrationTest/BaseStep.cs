﻿using System.Net.Mime;
using System.Text;
using System.Text.Json.JsonDiffPatch;
using System.Text.Json.JsonDiffPatch.Xunit;
using System.Text.Json.Nodes;
using FluentAssertions;
using Flurl;
using JobBank1111.Job.DB;
using JobBank1111.Testing.Common;
using JobBank1111.Testing.Common.MockServer;
using Json.Path;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reqnroll;
using Xunit.Abstractions;

namespace JobBank1111.Job.WebAPI.IntegrationTest;

[Binding]
public class BaseStep : Steps
{
    private readonly ITestOutputHelper _testOutputHelper;
    private static HttpClient ExternalClient;
    private const string StringEquals = "字串等於";
    private const string NumberEquals = "數值等於";
    private const string BoolEquals = "布林值等於";
    private const string JsonEquals = "Json等於";
    private const string DateTimeEquals = "時間等於";

    private const string OperationTypes = StringEquals
                                          + "|" + NumberEquals
                                          + "|" + BoolEquals
                                          + "|" + JsonEquals
                                          + "|" + DateTimeEquals;

    public BaseStep(ITestOutputHelper testOutputHelper)
    {
        this._testOutputHelper = testOutputHelper;
    }

    [BeforeTestRun]
    public static async Task BeforeTestRun()
    {
        //建立容器
        await CreateContainersAsync();
        TestAssistant.SetEnvironmentVariables();

        //建立當前測試步驟所需要的 DI Containers
        var serviceProvider = CreateServiceProvider();

        // //初始化測試專案需要的資源
        await InitialDatabase(serviceProvider);

        //
        async Task InitialDatabase(ServiceProvider serviceProvider)
        {
            var dbContextFactory = serviceProvider.GetService<IDbContextFactory<MemberDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            await dbContext.Initial();
        }

        async Task CreateContainersAsync()
        {
            var msSqlContainer = await TestContainerFactory.CreateMsSqlContainerAsync();
            var dbConnectionString = msSqlContainer.GetConnectionString();
            TestAssistant.SetDbConnectionEnvironmentVariable(dbConnectionString);
            var redisContainer = await TestContainerFactory.CreateRedisContainerAsync();
            var redisDomainUrl = redisContainer.GetConnectionString();
            TestAssistant.SetRedisConnectionEnvironmentVariable(redisDomainUrl);

            var mockServerContainer = await TestContainerFactory.CreateMockServerContainerAsync();
            var externalUrl = TestContainerFactory.GetMockServerConnection(mockServerContainer);
            TestAssistant.SetExternalConnectionEnvironmentVariable(externalUrl);
            ExternalClient = new HttpClient() { BaseAddress = new Uri(externalUrl) };
        }
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSysEnvironments();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDatabase();

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider;
    }

    [Given(@"資料庫已存在 Member 資料")]
    public void Given資料庫已存在Member資料(Table table)
    {
    }

    [Given(@"建立假端點，HttpMethod = ""(.*)""，URL = ""(.*)""，StatusCode = ""(.*)""，ResponseContent =")]
    public async Task Given建立假端點HttpMethodUrlStatusCodeResponseContent(
        string httpMethod, string url, int statusCode, string body)
    {
        var client = ExternalClient;
        await MockedServerAssistant.PutNewEndPointAsync(client, httpMethod, url, statusCode, body);
    }

    [When(@"調用端發送 ""(.*)"" 請求至 ""(.*)""")]
    public async Task When調用端發送請求至(string methodName, string url)
    {
        var client = this.ScenarioContext.GetHttpClient();

        var httpMethod = new HttpMethod(methodName);
        var urlSegments = Url.ParsePathSegments(url);
        var urlEncoded = Url.Combine(urlSegments.ToArray());
        urlEncoded = this.AppendQuery(urlEncoded);
        using var httpRequest = new HttpRequestMessage(httpMethod, urlEncoded);

        var contentType = MediaTypeNames.Application.Json;
        var headers = this.ScenarioContext.GetOrNewHeaders();
        foreach (var header in headers)
        {
            if (header.Key == "content-type")
            {
                contentType = header.Value.First();
            }
            else
            {
                httpRequest.Headers.Add(header.Key, header.Value.ToArray());
            }
        }

        var body = this.ScenarioContext.GetHttpRequestBody();
        if (string.IsNullOrWhiteSpace(body) is false)
        {
            httpRequest.Content = new StringContent(body, Encoding.UTF8, contentType);
        }

        var httpResponse = await client.SendAsync(httpRequest);
        var responseBody = await httpResponse.Content.ReadAsStringAsync();
        this.ScenarioContext.SetHttpResponse(httpResponse);
        this.ScenarioContext.SetHttpResponseBody(responseBody);
        this.ScenarioContext.SetHttpStatusCode(httpResponse.StatusCode);
        if (string.IsNullOrWhiteSpace(responseBody) == false)
        {
            Console.WriteLine(responseBody);
            this.ScenarioContext.SetJsonNode(JsonNode.Parse(responseBody));
        }
    }

    private static void ContentShouldBe(JsonNode srcJsonNode, string selectPath, string operationType, string expected)
    {
        var destJsonNode = JsonPath.Parse(selectPath);
        switch (operationType)
        {
            case StringEquals:
            {
                var actual = destJsonNode.Evaluate(srcJsonNode).Matches.FirstOrDefault()?.Value?.GetValue<string>();
                var errorReason =
                    $"{nameof(operationType)}: [{operationType}], {nameof(selectPath)}: [{selectPath}], {nameof(expected)}: [{expected}], {nameof(actual)}: [{actual}]";
                (actual ?? string.Empty).Should().Be(expected, errorReason);
                break;
            }
            case NumberEquals:
            {
                var actual = destJsonNode.Evaluate(srcJsonNode).Matches.FirstOrDefault()?.Value?.GetValue<int>();
                var errorReason =
                    $"{nameof(operationType)}: [{operationType}], {nameof(selectPath)}: [{selectPath}], {nameof(expected)}: [{expected}], {nameof(actual)}: [{actual}]";
                actual.Should().Be(int.Parse(expected), errorReason);
                break;
            }
            case BoolEquals:
            {
                var actual = destJsonNode.Evaluate(srcJsonNode).Matches.FirstOrDefault()?.Value?.GetValue<bool>();
                var errorReason =
                    $"{nameof(operationType)}: [{operationType}], {nameof(selectPath)}: [{selectPath}], {nameof(expected)}: [{expected}], {nameof(actual)}: [{actual}]";
                actual.Should().Be(bool.Parse(expected), errorReason);
                break;
            }
            case DateTimeEquals:
            {
                var expect = DateTimeOffset.Parse(expected);
                var actual = destJsonNode.Evaluate(srcJsonNode).Matches.FirstOrDefault()
                        ?.Value
                        ?.GetValue<DateTimeOffset>()
                    ;
                var errorReason =
                    $"{nameof(operationType)}: [{operationType}], {nameof(selectPath)}: [{selectPath}], {nameof(expected)}: [{expect}], {nameof(actual)}: [{actual}]";
                actual.Should().Be(expect, errorReason);
                break;
            }
            case JsonEquals:
            {
                var actual = destJsonNode.Evaluate(srcJsonNode).Matches.FirstOrDefault()?.Value;
                var expect = string.IsNullOrWhiteSpace(expected) ? null : JsonNode.Parse(expected);
                var diff = actual.Diff(expect);
                var errorReason =
                    $"{nameof(operationType)}: [{operationType}], {nameof(selectPath)}: [{selectPath}], {nameof(expected)}: [{expected}], {nameof(actual)}: [{actual?.ToJsonString()}], diff: [{diff?.ToJsonString()}]";
                actual.DeepEquals(expect).Should().BeTrue(errorReason);
                break;
            }
        }
    }

    private string AppendQuery(string url)
    {
        var flUrl = new Url(url);
        foreach (var query in this.ScenarioContext.GetAllQueryString())
        {
            flUrl.QueryParams.Add(query.Key, query.Value.Trim());

            // 不能用  SetQueryParam，因為有多個相同的 querystring 如: filters，會後蓋前
            //url = url.SetQueryParam(query.Key, query.Value.Trim());
        }

        return flUrl.ToString();
    }

    [Then(@"預期回傳內容中路徑 ""(.*)"" 的""(.*)"" ""(.*)""")]
    public void Then預期回傳內容中路徑的(string selectPath, string operationType, string expected)
    {
        var srcJsonNode = this.ScenarioContext.GetJsonNode();
        ContentShouldBe(srcJsonNode, selectPath, operationType, expected);
    }

    [Then(@"預期回傳內容為")]
    public void Then預期回傳內容為(string expected)
    {
        var actual = this.ScenarioContext.GetHttpResponseBody();
        JsonAssert.Equal(expected, actual, true);
    }

    [Given(@"調用端已準備 Header 參數")]
    public void Given調用端已準備Header參數(Table table)
    {
        foreach (var row in table.Rows)
        {
            foreach (var header in table.Header)
            {
                var value = row[header];
                this.ScenarioContext.AddHttpHeader(header, value);
            }
        }
    }

    [Given(@"調用端已準備 Query 參數")]
    public void Given調用端已準備Query參數(Table table)
    {
        foreach (var row in table.Rows)
        {
            foreach (var header in table.Header)
            {
                var value = row[header];
                this.ScenarioContext.AddQueryString(header, value);
            }
        }
    }

    [Given(@"初始化測試伺服器")]
    public void Given初始化測試伺服器(Table table)
    {
        var row = table.Rows.FirstOrDefault();

        DateTimeOffset? now = null;
        if (row.TryGetValue("Now", out var nowText))
        {
            now = TestAssistant.ToUtc(nowText);
            this.ScenarioContext.SetUtcNow(now);
        }

        if (row.TryGetValue("UserId", out var userId))
        {
            this.ScenarioContext.SetUserId(userId);
        }

        var server = new TestServer(now.Value, userId);
        var httpClient = server.CreateClient();
        this.ScenarioContext.SetHttpClient(httpClient);
        this.ScenarioContext.SetServiceProvider(server.Services);
    }

    [Given(@"調用端已準備 Body 參數\(Json\)")]
    public void Given調用端已準備Body參數Json(string json)
    {
        this.ScenarioContext.SetHttpRequestBody(json);
    }

    [Then(@"預期資料庫已存在 Member 資料為")]
    public async Task Then預期資料庫已存在Member資料為(Table table)
    {
        await using var dbContext = await this.ScenarioContext.GetMemberDbContextFactory().CreateDbContextAsync();
        var actual = await dbContext.Members.AsNoTracking().ToListAsync();
        table.CompareToSet(actual);
    }

    [Then(@"預期得到 HttpStatusCode 為 ""(.*)""")]
    public void Then預期得到HttpStatusCode為(int expected)
    {
        var actual = (int)this.ScenarioContext.GetHttpStatusCode();
        actual.Should().Be(expected);
    }
}