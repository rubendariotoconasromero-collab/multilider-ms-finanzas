using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MSMultilider.Finanzas.Services;

public static class StartupHook
{
    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("MULTILIDER_TEST_ISOLATED") != "1" ||
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Testing" ||
            Environment.GetEnvironmentVariable("MULTILIDER_F5_BOUNDARY_DIR") is not { } directory)
            throw new InvalidOperationException("Isolated finance fixture required.");

        DiagnosticListener.AllListeners.Subscribe(new Subscription<DiagnosticListener>(listener =>
        {
            if (listener.Name == "Microsoft.EntityFrameworkCore")
                listener.Subscribe(new Subscription<KeyValuePair<string, object?>>(entry =>
                {
                    var request = new HttpContextAccessor().HttpContext?.Request;
                    if (entry.Key != RelationalEventId.TransactionCommitted.Name || request is null ||
                        !HttpMethods.IsPost(request.Method) || request.Path.Value is not { } path ||
                        !path.StartsWith("/api/pagos/") || !path.EndsWith("/procesar") ||
                        !Guid.TryParse(request.Headers["Idempotency-Key"], out var id))
                        return;
                    Wait(directory, id.ToString("N") + "-Cobro-after");
                }));
            if (listener.Name == "Microsoft.Extensions.Hosting")
                listener.Subscribe(new Subscription<KeyValuePair<string, object?>>(entry =>
                {
                    if (entry is { Key: "HostBuilding", Value: IHostBuilder host })
                        host.ConfigureServices((_, services) => services.AddSingleton<ILibelulaProvider>(new FixtureProvider(directory)));
                }));
        }));
    }

    private static void Wait(string directory, string name)
    {
        var marker = Path.Combine(directory, name);
        if (!File.Exists(marker + ".arm")) return;
        File.Move(marker + ".arm", marker + ".claimed");
        File.WriteAllText(marker + ".hit", Environment.ProcessId.ToString());
        if (!SpinWait.SpinUntil(() => { Thread.Sleep(25); return File.Exists(marker + ".release"); }, TimeSpan.FromSeconds(60)))
            throw new TimeoutException("Isolated finance boundary was not released.");
    }

    private sealed class FixtureProvider(string directory) : ILibelulaProvider
    {
        public Task<JsonElement> Register(object data, CancellationToken ct) =>
            Task.FromResult(JsonSerializer.SerializeToElement(new { id_transaccion = "fixture", url_pasarela_pagos = "https://payments.example.test/checkout" }));

        public Task<bool> Verify(string reference, string currency, decimal amount, DateTime created, CancellationToken ct)
        {
            if (!reference.StartsWith("TEMP-") || !Guid.TryParseExact(reference[5..], "D", out var id))
                return Task.FromResult(false);
            Wait(directory, id.ToString("N") + "-Callback-after");
            return Task.FromResult(currency == "BOB" && amount == .20m && File.Exists(Path.Combine(directory, $"{id:N}-provider-paid")));
        }
    }

    private sealed class Subscription<T>(Action<T> receive) : IObserver<T>
    {
        public void OnNext(T value) => receive(value);
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
