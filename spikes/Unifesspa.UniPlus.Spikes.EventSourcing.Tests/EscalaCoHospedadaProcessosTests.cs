using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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

        using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };

        // Réplica A: porta efêmera (:0). A porta REAL é lida do log "Now listening on"
        // do processo — sem pré-alocação, sem corrida de porta. Provisiona o schema.
        int portaA = await IniciarReplicaAsync(hostDll, TimeSpan.FromSeconds(90));
        (await EsperarSaudavelAsync(http, portaA, TimeSpan.FromSeconds(30)))
            .Should().BeTrue($"a réplica A (porta {portaA}) deve ficar pronta. Logs:\n{Logs()}");

        // Réplica B (schema já existe).
        int portaB = await IniciarReplicaAsync(hostDll, TimeSpan.FromSeconds(90));
        (await EsperarSaudavelAsync(http, portaB, TimeSpan.FromSeconds(30)))
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

    private async Task<int> IniciarReplicaAsync(string hostDll, TimeSpan timeout)
    {
        ProcessStartInfo psi = new("dotnet", $"exec \"{hostDll}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["SPIKE_DB"] = _postgres.GetConnectionString();
        // Porta efêmera: o SO escolhe uma porta livre; lemos a real do log de startup.
        psi.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["DOTNET_ENVIRONMENT"] = "Development";

        TaskCompletionSource<int> portaLida = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void Observar(string? linha)
        {
            if (linha is null)
            {
                return;
            }

            _logs.Enqueue(linha);
            Match m = Regex.Match(linha, @"Now listening on:\s*http://[^:]+:(\d+)");
            if (m.Success)
            {
                portaLida.TrySetResult(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        Process processo = new() { StartInfo = psi };
        processo.OutputDataReceived += (_, e) => Observar(e.Data);
        processo.ErrorDataReceived += (_, e) => Observar(e.Data);
        processo.Start();
        processo.BeginOutputReadLine();
        processo.BeginErrorReadLine();
        _processos.Add(processo);

        return await portaLida.Task.WaitAsync(timeout);
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
