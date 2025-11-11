using System;
using System.Collections.Generic;

namespace AntManager.Models;

public class ApiRequestTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string Body { get; set; } = string.Empty;
    public List<ApiHeaderEntry> Headers { get; set; } = new();
}

public class ApiHeaderEntry
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
