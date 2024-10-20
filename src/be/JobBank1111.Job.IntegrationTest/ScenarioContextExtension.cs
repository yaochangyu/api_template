﻿using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using JobBank1111.Job.DB;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace JobBank1111.Job.WebAPI.IntegrationTest;

public static class ScenarioContextExtension
{
    public static T? GetOrDefault<T>(this ScenarioContext context, string key, T? defaultValue = default) =>
        context.ContainsKey(key) ? context.Get<T>(key) : defaultValue;

    public static void SetServiceProvider(this ScenarioContext context, IServiceProvider serviceProvider)
    {
        context.Set(serviceProvider);
    }

    public static IServiceProvider GetServiceProvider(this ScenarioContext context)
    {
        return context.Get<IServiceProvider>();
    }

    public static IDbContextFactory<MemberDbContext> GetMemberDbContextFactory(this ScenarioContext context)
    {
        return GetServiceProvider(context).GetService<IDbContextFactory<MemberDbContext>>();
    }

    public static string? GetUserId(this ScenarioContext context)
        => context.TryGetValue($"UserId", out string userId)
            ? userId
            : null;

    public static void SetUserId(this ScenarioContext context, string userId) => context.Set(userId, $"UserId");

    public static DateTimeOffset? GetUtcNow(this ScenarioContext context) =>
        context.TryGetValue($"UtcNow", out DateTimeOffset dateTime)
            ? dateTime
            : null;

    public static void SetUtcNow(this ScenarioContext context, DateTimeOffset? dateTime) =>
        context.Set(dateTime, $"UtcNow");

    public static long? GetFirmId(this ScenarioContext context) =>
        context.TryGetValue($"FirmId", out long firmId)
            ? firmId
            : null;

    public static void SetHttpClient(this ScenarioContext context, HttpClient httpClient) =>
        context.Set(httpClient);

    public static HttpClient GetHttpClient(this ScenarioContext context) =>
        context.Get<HttpClient>();

    public static void AddQueryString(this ScenarioContext context, string key, string value)
    {
        if (!context.TryGetValue<IList<(string Key, string Value)>>("QueryString", out var data))
        {
            data = new List<(string Key, string Value)>();
        }

        data.Add((key, value));
        context.Set(data, "QueryString");
    }

    public static IList<(string Key, string Value)> GetAllQueryString(this ScenarioContext context)
    {
        return context.TryGetValue(out IList<(string Key, string Value)> result)
            ? result
            : new List<(string Key, string Value)>();
    }

    public static void SetHttpResponse(this ScenarioContext context, HttpResponseMessage response) =>
        context.Set(response);

    public static HttpResponseMessage GetHttpResponse(this ScenarioContext context) =>
        context.TryGetValue(out HttpResponseMessage result) ? result : default;

    public static void SetHttpResponseBody(this ScenarioContext context, string body) =>
        context.Set(body, "HttpResponseBody");

    public static string GetHttpResponseBody(this ScenarioContext context) =>
        context.TryGetValue("HttpResponseBody", out string body) ? body : null;

    public static void SetHttpStatusCode(this ScenarioContext context, HttpStatusCode httpStatusCode) =>
        context.Set(httpStatusCode, "HttpStatusCode");

    public static HttpStatusCode GetHttpStatusCode(this ScenarioContext context) =>
        context.Get<HttpStatusCode>("HttpStatusCode");

    public static void SetXUnitLog(this ScenarioContext context, StringBuilder stringBuilder)
    {
        context.Set(stringBuilder, "XUnitLog");
    }

    public static StringBuilder GetXUnitLog(this ScenarioContext context)
    {
        context.TryGetValue("XUnitLog", out StringBuilder? stringBuilder);
        return stringBuilder ?? new StringBuilder();
    }

    public static void AddHttpHeader(this ScenarioContext context, string key, string value)
    {
        var headers = context.GetOrNewHeaders();
        headers[key] = value;
        context.Set(headers);
    }

    public static IHeaderDictionary GetOrNewHeaders(this ScenarioContext context)
    {
        if (context.TryGetValue(out IHeaderDictionary result) is false)
        {
            result = new HeaderDictionary();
        }

        return result;
    }

    public static void SetHttpRequestBody(this ScenarioContext context, string body) =>
        context.Set(body, "HttpRequestBody");

    public static string? GetHttpRequestBody(this ScenarioContext context) =>
        context.GetOrDefault<string>($"HttpRequestBody");

    public static void SetJsonNode(this ScenarioContext context, JsonNode input)
    {
        context.Set(input);
    }

    public static JsonNode GetJsonNode(this ScenarioContext context)
    {
        return context.Get<JsonNode>();
    }
}