using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using AwesomeAssertions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Escala horizontal — caminho ponta a ponta na topologia CO-HOSPEDADA, com
/// processos REAIS separados (duas instâncias da API em portas distintas). É o
/// único jeito de exercitar o handler ancillary <c>[MartenStore]</c> sob
/// concorrência multi-réplica sem o artefato de code-gen multi-app in-process
/// (cada processo tem seu próprio cache do JasperFx). Sobre um único Postgres,
/// as duas réplicas retificam o mesmo stream concorrentemente e tudo converge —
/// sem lost update.
/// </summary>
public sealed class EscalaCoHospedadaProcessosTests : IAsyncLifetime
{
    private const int Retificacoes = 8;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_spike_cohost")
        .WithUsername("uniplus_spike")
        .WithPassword("uniplus_spike")
        .Build();

    private readonly List<Process> _processos = [];
    private readonly ConcurrentQueue<string> _logs = new();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync()
    {
        foreach (Process p in _processos)
        {
            try { if (!p.HasExited) { p.Kill(entireProcessTree: true); } }
            catch (InvalidOperationException) { /* já saiu */ }
            p.Dispose();
        }

        await _postgres.DisposeAsync();
    }

    [Fact(DisplayName = "Escala co-hospedada: 2 processos da API em portas distintas retificam o mesmo stream sem lost update")]
    public async Task Dois_processos_co_hospedados_retificam_mesmo_stream()
    {
        string hostDll = LocalizarHostDll();
        File.Exists(hostDll).Should().BeTrue($"o host deve estar compilado em {hostDll}");

        int portaA = PortaLivre();
        int portaB = PortaLivre();

        using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };

        // Sobe a réplica A e espera ficar pronta (provisiona o schema).
        IniciarReplica(hostDll, portaA);
        (await EsperarSaudavelAsync(http, portaA, TimeSpan.FromSeconds(90)))
            .Should().BeTrue($"a réplica A (porta {portaA}) deve ficar pronta. Logs:\n{Logs()}");

        // Sobe a réplica B (schema já existe).
        IniciarReplica(hostDll, portaB);
        (await EsperarSaudavelAsync(http, portaB, TimeSpan.FromSeconds(90)))
            .Should().BeTrue($"a réplica B (porta {portaB}) deve ficar pronta. Logs:\n{Logs()}");

        // Abre o stream (via réplica A).
        Guid editalId = Guid.CreateVersion7();
        HttpResponseMessage abertura = await http.PostAsync(Url(portaA, $"/es/{editalId}/abrir"), content: null);
        string corpoAbertura = await abertura.Content.ReadAsStringAsync();
        abertura.IsSuccessStatusCode.Should().BeTrue(
            $"abrir o edital deve responder com sucesso. Corpo: {corpoAbertura}\nLogs:\n{Logs()}");

        // N retificações CONCORRENTES no mesmo stream, divididas entre as duas réplicas.
        List<Task<HttpResponseMessage>> chamadas = [];
        for (int i = 0; i < Retificacoes; i++)
        {
            int porta = i % 2 == 0 ? portaA : portaB;
            chamadas.Add(http.PostAsync(Url(porta, $"/es/{editalId}/retificar"), content: null));
        }

        HttpResponseMessage[] respostas = await Task.WhenAll(chamadas);
        respostas.Should().OnlyContain(r => r.IsSuccessStatusCode,
            "o Wolverine retenta os conflitos de concorrência; toda retificação converge");

        // Estado final: todas as retificações aplicadas, nenhuma perdida.
        bool convergiu = await EsperarAsync(
            async () => await ContarRetificacoesAsync(http, portaA, editalId) == Retificacoes,
            TimeSpan.FromSeconds(30));

        int total = await ContarRetificacoesAsync(http, portaA, editalId);
        convergiu.Should().BeTrue($"as {Retificacoes} retificações devem convergir sem lost update (aplicadas: {total})");
    }

    private void IniciarReplica(string hostDll, int porta)
    {
        ProcessStartInfo psi = new("dotnet", $"exec \"{hostDll}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["SPIKE_DB"] = _postgres.GetConnectionString();
        psi.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{porta}";
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["DOTNET_ENVIRONMENT"] = "Development";

        Process processo = new() { StartInfo = psi };
        processo.OutputDataReceived += (_, e) => { if (e.Data is not null) { _logs.Enqueue($"[{porta}] {e.Data}"); } };
        processo.ErrorDataReceived += (_, e) => { if (e.Data is not null) { _logs.Enqueue($"[{porta}!] {e.Data}"); } };
        processo.Start();
        processo.BeginOutputReadLine();
        processo.BeginErrorReadLine();
        _processos.Add(processo);
    }

    private static string LocalizarHostDll()
    {
        // Resolve a configuração (Debug/Release) e o TFM a partir do diretório de
        // saída do próprio teste — .../Tests/bin/<Config>/<Tfm>/ — para lançar o host
        // da MESMA configuração da execução (não hard-coda Debug).
        DirectoryInfo saidaTeste = new(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        string tfm = saidaTeste.Name;            // ex.: net10.0
        string config = saidaTeste.Parent!.Name; // Debug | Release

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            $"../../../../Unifesspa.UniPlus.Spikes.EventSourcing.Host/bin/{config}/{tfm}/Unifesspa.UniPlus.Spikes.EventSourcing.Host.dll"));
    }

    private static int PortaLivre()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int porta = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return porta;
    }

    private static Uri Url(int porta, string caminho) => new($"http://127.0.0.1:{porta}{caminho}");

    private static async Task<bool> EsperarSaudavelAsync(HttpClient http, int porta, TimeSpan timeout)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < limite)
        {
            try
            {
                HttpResponseMessage r = await http.GetAsync(Url(porta, "/health"));
                if (r.StatusCode == HttpStatusCode.OK) { return true; }
            }
            catch (HttpRequestException) { /* ainda subindo */ }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return false;
    }

    private static async Task<int> ContarRetificacoesAsync(HttpClient http, int porta, Guid editalId) =>
        await http.GetFromJsonAsync<int>(Url(porta, $"/es/{editalId}/retificacoes"));

    private static async Task<bool> EsperarAsync(Func<Task<bool>> condicao, TimeSpan timeout)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < limite)
        {
            if (await condicao()) { return true; }
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return await condicao();
    }

    private string Logs() => string.Join('\n', _logs.TakeLast(40));
}
