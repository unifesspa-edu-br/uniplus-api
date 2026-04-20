namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

using System.Collections.Immutable;
using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed partial class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly QueryStringMasker _masker;
    private readonly ImmutableArray<string> _prefixosSilenciados;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        QueryStringMasker masker,
        IOptions<RequestLoggingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(masker);
        ArgumentNullException.ThrowIfNull(options);
        _next = next;
        _logger = logger;
        _masker = masker;

        // Prefixos são normalizados removendo trailing slash (exceto o caso
        // especial "/", preservado como raiz). Armazenados como ImmutableArray
        // ordenado para iteração em hot path sem custo de enumerator —
        // FrozenSet.Contains não serve porque precisamos de prefix match com
        // boundary, não igualdade.
        _prefixosSilenciados = options.Value.PrefixosSilenciados
            .Select(NormalizarPrefixo)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        long startTimestamp = Stopwatch.GetTimestamp();
        Exception? falha = null;

        // try/catch/throw + finally: capturamos a exception apenas como
        // contexto para o log (mantendo stack trace via `throw;`) e a
        // deixamos propagar para o GlobalExceptionMiddleware decidir a
        // resposta. Sem essa captura, uma exception que escapasse daria
        // um log Information com status 200 (default) — mascarando falha
        // como sucesso.
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            falha = ex;
            throw;
        }
        finally
        {
            double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            string method = context.Request.Method;
            string path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
            string query = _masker.Mascarar(context.Request.QueryString);
            int statusCode = context.Response.StatusCode;

            // Presença de exception força nível Error independentemente do
            // status code observado — evita que uma falha silenciosa (status
            // não alterado) apareça como sucesso no log. Severidade
            // proporcional à categoria HTTP: 5xx é falha do servidor,
            // 4xx sinaliza problema no lado do cliente e demais respostas são
            // tráfego operacional normal. Paths de infraestrutura (health,
            // metrics) são silenciados quando bem-sucedidos para não saturar
            // observabilidade com probes de liveness/readiness; respostas de
            // erro continuam sendo reportadas porque sinalizam problema real.
            if (falha is not null || statusCode >= 500)
            {
                LogRequestServerError(_logger, method, path, query, statusCode, elapsedMs, falha);
            }
            else if (statusCode >= 400)
            {
                LogRequestClientError(_logger, method, path, query, statusCode, elapsedMs);
            }
            else if (!DeveSilenciar(path))
            {
                LogRequestSucesso(_logger, method, path, query, statusCode, elapsedMs);
            }
        }
    }

    // Match case-insensitive por prefixo com boundary em `/`. Aceita o path
    // exato ou subpaths (`/health`, `/health/`, `/health/db`) mas rejeita
    // falsos positivos que meramente começam com o prefixo (`/healthy`,
    // `/health-ui`). O boundary garante que apenas separadores hierárquicos
    // de URL contam como extensão do prefixo.
    //
    // Trabalha com `ReadOnlySpan<char>` em vez de `string` para não alocar
    // em requests com trailing slash — hot path executa por request. Os
    // prefixos em `_prefixosSilenciados` já estão normalizados (sem trailing
    // slash, exceto "/") desde o construtor.
    private bool DeveSilenciar(string path)
    {
        ReadOnlySpan<char> pathSpan = path.AsSpan();
        if (pathSpan.Length > 1 && pathSpan[^1] == '/')
        {
            pathSpan = pathSpan.TrimEnd('/');
        }

        foreach (string prefixo in _prefixosSilenciados)
        {
            if (pathSpan.Equals(prefixo, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (pathSpan.Length > prefixo.Length &&
                pathSpan.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase) &&
                pathSpan[prefixo.Length] == '/')
            {
                return true;
            }
        }

        return false;
    }

    // Normaliza removendo trailing slash exceto no caso raiz "/", que é
    // preservado como path canônico da raiz. Usado apenas no construtor
    // (caminho frio) — o hot path do DeveSilenciar faz a normalização
    // equivalente via span sem alocação.
    private static string NormalizarPrefixo(string valor) =>
        valor.Length > 1 && valor.EndsWith('/') ? valor.TrimEnd('/') : valor;

    [LoggerMessage(Level = LogLevel.Information, Message = "HTTP {Method} {Path}{Query} respondeu {StatusCode} em {ElapsedMs}ms")]
    private static partial void LogRequestSucesso(ILogger logger, string method, string path, string query, int statusCode, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HTTP {Method} {Path}{Query} respondeu {StatusCode} em {ElapsedMs}ms")]
    private static partial void LogRequestClientError(ILogger logger, string method, string path, string query, int statusCode, double elapsedMs);

    // Exception é o último parâmetro (convenção do source generator de
    // LoggerMessage): o gerador reconhece o tipo e emite no LogEvent.Exception
    // em vez de no template de mensagem — preserva stack trace estruturada
    // nos sinks sem polui-lo com a representação textual.
    [LoggerMessage(Level = LogLevel.Error, Message = "HTTP {Method} {Path}{Query} respondeu {StatusCode} em {ElapsedMs}ms")]
    private static partial void LogRequestServerError(ILogger logger, string method, string path, string query, int statusCode, double elapsedMs, Exception? ex);
}
