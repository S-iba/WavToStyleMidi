using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WavToStyleMidi.Cli;
using WavToStyleMidi.Core;
using WavToStyleMidi.Core.Conversion;
using WavToStyleMidi.Core.DependencyInjection;

if (!CliArguments.TryParse(args, out var request, out var error))
{
    if (error is not null)
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine();
    }

    Console.WriteLine(CliArguments.Usage);
    return error is null ? 0 : 2;
}

// Arguments are parsed above; keep them out of configuration so "--bpm" is not read as a setting.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = [],
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddWavToStyleMidi();
builder.Services.Configure<DetectionOptions>(builder.Configuration.GetSection(DetectionOptions.SectionName));
builder.Services.Configure<DrumNoteMap>(builder.Configuration.GetSection(DrumNoteMap.SectionName));

using var host = builder.Build();

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var converter = host.Services.GetRequiredService<IBatchConverter>();

try
{
    var results = await converter.ConvertAsync(request!, cancellation.Token);
    foreach (var result in results)
    {
        Console.WriteLine(ResultFormatter.Format(result));
    }

    return results.All(r => r.Succeeded) ? 0 : 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return 130;
}
