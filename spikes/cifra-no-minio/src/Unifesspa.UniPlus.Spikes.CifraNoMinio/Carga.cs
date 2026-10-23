namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
/// Gerador de carga das medições, rodado no host. Cada arquivo tem conteúdo aleatório próprio (o
/// clamd guarda em cache o veredicto de conteúdo já visto). <c>--concorrencia</c> é o número de
/// candidatos enviando ao mesmo tempo. No fluxo atual o candidato espera a confirmação síncrona; no
/// novo ele é liberado ao fim do PUT e o veredicto chega depois, então o tempo até o veredicto vai do
/// fim do PUT ao registro do verificador, e a notificação, do fim do PUT à chegada do evento.
/// No modo <c>fila</c>, o candidato confirma o envio na API (que publica o pedido pelo outbox) e é
/// liberado; o veredicto é o registrado pela API quando o resultado volta do verificador.
/// </summary>
public static class Carga
{
    private sealed record Envio(string Chave, string Url);

    private sealed record Medida(bool Ok, string Veredicto, double EnvioMs, double AteVeredictoMs, double VerificacaoMs, double FilaMs, double NotificacaoMs = 0, string? Erro = null);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<int> ExecutarAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Dictionary<string, string> opcoes = Enumerable.Range(0, args.Length / 2)
            .ToDictionary(i => args[2 * i].TrimStart('-'), i => args[(2 * i) + 1], StringComparer.Ordinal);
        string modo = opcoes["modo"];
        int tamanhoMb = int.Parse(opcoes["tamanho-mb"], CultureInfo.InvariantCulture);
        int quantidade = int.Parse(opcoes["quantidade"], CultureInfo.InvariantCulture);
        int concorrencia = int.Parse(opcoes["concorrencia"], CultureInfo.InvariantCulture);
        string rotulo = opcoes.GetValueOrDefault("rotulo", $"{modo}-{tamanhoMb}mb-c{concorrencia}");
        // Nas provas de robustez, a carga só envia e confirma; quem acompanha o estado é o roteiro.
        SoConfirmar = string.Equals(opcoes.GetValueOrDefault("aguardar", "sim"), "nao", StringComparison.Ordinal);

        using HttpClient http = new() { Timeout = TimeSpan.FromMinutes(5) };
        using SemaphoreSlim vagas = new(concorrencia);
        Stopwatch parede = Stopwatch.StartNew();
        Medida[] medidas = await Task.WhenAll(Enumerable.Range(0, quantidade).Select(async _ =>
        {
            await vagas.WaitAsync();
            return await EnviarAsync(http, modo, tamanhoMb * 1024 * 1024, vagas);
        }));
        double paredeS = parede.Elapsed.TotalSeconds;

        Medida[] ok = [.. medidas.Where(m => m.Ok)];
        var resumo = new
        {
            rotulo,
            modo,
            tamanhoMb,
            quantidade,
            concorrencia,
            falhas = quantidade - ok.Length,
            erros = medidas.Where(m => !m.Ok).GroupBy(m => m.Erro ?? m.Veredicto, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
            veredictos = ok.GroupBy(m => m.Veredicto, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
            paredeS = Math.Round(paredeS, 2),
            arquivosPorS = Math.Round(ok.Length / paredeS, 2),
            mbPorS = Math.Round(ok.Length * tamanhoMb / paredeS, 1),
            envioMs = Percentis(ok.Select(m => m.EnvioMs)),
            ateVeredictoMs = Percentis(ok.Select(m => m.AteVeredictoMs)),
            verificacaoMs = Percentis(ok.Select(m => m.VerificacaoMs)),
            filaMs = Percentis(ok.Select(m => m.FilaMs)),
            notificacaoMs = Percentis(ok.Select(m => m.NotificacaoMs)),
        };
        Console.WriteLine(JsonSerializer.Serialize(resumo, Json));
        return ok.Length == quantidade ? 0 : 1;
    }

    private static async Task<Medida> EnviarAsync(HttpClient http, string modo, int tamanho, SemaphoreSlim vaga)
    {
        byte[] conteudo = new byte[tamanho];
        RandomNumberGenerator.Fill(conteudo);
        Stopwatch cronometro = Stopwatch.StartNew();
        bool vagaLiberada = false;
        try
        {
            string api = modo == "fila" ? ApiComFila : "http://localhost:18080";
            using HttpResponseMessage criado = await http.PostAsync(
                new Uri($"{api}/envios?fluxo={modo}"), content: null);
            Envio envio = (await criado.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<Envio>(Json))!;
            using ByteArrayContent corpo = new(conteudo);
            corpo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            using HttpResponseMessage put = await http.PutAsync(new Uri(envio.Url), corpo);
            put.EnsureSuccessStatusCode();
            double envioMs = cronometro.Elapsed.TotalMilliseconds;
            DateTimeOffset fimDoEnvio = DateTimeOffset.UtcNow;

            if (modo == "atual")
            {
                using HttpResponseMessage confirmado = await http.PostAsync(
                    new Uri($"http://localhost:18081/confirmar/{envio.Chave}"), content: null);
                JsonElement corpoResposta = await confirmado.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<JsonElement>(Json);
                double total = cronometro.Elapsed.TotalMilliseconds;
                return new Medida(true, corpoResposta.GetProperty("veredicto").GetString()!, envioMs, total - envioMs, corpoResposta.GetProperty("ms").GetDouble(), 0);
            }

            if (modo == "fila")
            {
                return await ConfirmarEAguardarAsync(http, envio.Chave, envioMs, fimDoEnvio, cronometro, vaga, () => vagaLiberada = true);
            }

            // No fluxo novo o candidato termina no PUT: a vaga dele volta para o próximo envio.
            vaga.Release();
            vagaLiberada = true;
            while (cronometro.Elapsed < TimeSpan.FromMinutes(5))
            {
                using HttpResponseMessage consulta = await http.GetAsync(new Uri($"http://localhost:18082/resultados/{envio.Chave}"));
                if (consulta.IsSuccessStatusCode)
                {
                    RegistroDaVerificacao registro = (await consulta.Content.ReadFromJsonAsync<RegistroDaVerificacao>(Json))!;
                    return new Medida(
                        true,
                        registro.Veredicto,
                        envioMs,
                        (registro.ConcluidoEm - fimDoEnvio).TotalMilliseconds,
                        registro.VerificacaoMs,
                        registro.EsperaNaFilaMs,
                        (registro.RecebidoEm - fimDoEnvio).TotalMilliseconds);
                }

                await Task.Delay(50);
            }

            return new Medida(false, "SEM-RESULTADO", envioMs, 0, 0, 0, Erro: "sem veredicto em 5 min");
        }
        catch (HttpRequestException ex)
        {
            return new Medida(false, "FALHA", 0, 0, 0, 0, Erro: ex.StatusCode is { } status ? $"HTTP {(int)status}" : ex.HttpRequestError.ToString());
        }
        catch (TaskCanceledException)
        {
            return new Medida(false, "FALHA", 0, 0, 0, 0, Erro: "tempo esgotado");
        }
        finally
        {
            if (!vagaLiberada)
            {
                vaga.Release();
            }
        }
    }

    private static bool SoConfirmar { get; set; }

    private static readonly string ApiComFila = Environment.GetEnvironmentVariable("API_FILA") ?? "http://localhost:18083";

    private sealed record EstadoDoArquivo(string Estado, string? Veredicto, DateTimeOffset ConfirmadoEm, DateTimeOffset? InicioDaVerificacaoEm, DateTimeOffset? RegistradoEm, double? VerificacaoMs);

    /// <summary>
    /// A confirmação é a última espera do candidato. Depois dela, a vaga volta para o próximo envio e
    /// a carga só acompanha o estado na API. "Notificação" passa a ser o tempo da confirmação ao
    /// início da verificação (a fila), e "fila" o da confirmação na API (a última espera do candidato).
    /// </summary>
    private static async Task<Medida> ConfirmarEAguardarAsync(HttpClient http, string chave, double envioMs, DateTimeOffset fimDoEnvio, Stopwatch cronometro, SemaphoreSlim vaga, Action liberada)
    {
        using (HttpResponseMessage confirmado = await http.PostAsync(new Uri($"{ApiComFila}/confirmar/{chave}"), content: null))
        {
            confirmado.EnsureSuccessStatusCode();
        }

        double confirmacaoMs = cronometro.Elapsed.TotalMilliseconds - envioMs;
        vaga.Release();
        liberada();
        if (SoConfirmar)
        {
            return new Medida(true, "CONFIRMADO", envioMs, 0, 0, confirmacaoMs);
        }

        while (cronometro.Elapsed < TimeSpan.FromMinutes(5))
        {
            EstadoDoArquivo estado = (await http.GetFromJsonAsync<EstadoDoArquivo>(new Uri($"{ApiComFila}/arquivos/{chave}/estado"), Json))!;
            if (estado is { Estado: not "EM_VERIFICACAO", InicioDaVerificacaoEm: { } inicio, RegistradoEm: { } registrado })
            {
                return new Medida(
                    estado.Estado == "VERIFICADO",
                    estado.Veredicto ?? estado.Estado,
                    envioMs,
                    (registrado - fimDoEnvio).TotalMilliseconds,
                    estado.VerificacaoMs ?? 0,
                    confirmacaoMs,
                    (inicio - estado.ConfirmadoEm).TotalMilliseconds);
            }

            await Task.Delay(50);
        }

        return new Medida(false, "SEM-RESULTADO", envioMs, 0, 0, 0, Erro: "sem veredicto em 5 min");
    }

    private static object Percentis(IEnumerable<double> valores)
    {
        double[] ordenados = [.. valores.Order()];
        if (ordenados.Length == 0)
        {
            return new { };
        }

        double P(double p) => Math.Round(ordenados[Math.Min(ordenados.Length - 1, (int)Math.Ceiling(p * ordenados.Length) - 1)], 1);
        return new { p50 = P(0.50), p95 = P(0.95), max = Math.Round(ordenados[^1], 1), media = Math.Round(ordenados.Average(), 1) };
    }
}
