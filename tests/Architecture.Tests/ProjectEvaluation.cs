using System.Diagnostics;
using System.Text.Json;

namespace Architecture.Tests;

internal static class ProjectEvaluation
{
    public static string[] References(string path)
    {
        ProcessStartInfo start = new("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(path);
        start.ArgumentList.Add("-getItem:ProjectReference");
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet msbuild.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"MSBuild could not evaluate {path}: {error.GetAwaiter().GetResult()} {output.GetAwaiter().GetResult()}");
        using JsonDocument json = JsonDocument.Parse(output.GetAwaiter().GetResult());
        return json.RootElement.GetProperty("Items").GetProperty("ProjectReference").EnumerateArray()
            .Select(item => item.GetProperty("FullPath").GetString() ?? throw new InvalidOperationException("Missing project reference path."))
            .ToArray();
    }
}
