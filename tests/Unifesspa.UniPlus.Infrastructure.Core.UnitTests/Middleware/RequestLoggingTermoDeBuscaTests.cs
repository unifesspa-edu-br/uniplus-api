namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Middleware;

using System.Globalization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

/// <summary>
/// O termo digitado em uma busca, e o identificador de pessoa ou de ocorrência que o cliente informa na consulta,
/// nunca chegam ao log da requisição: são texto livre e podem ser nome, e-mail ou número de documento.
/// O log segue registrando método, caminho, status, duração e os demais parâmetros.
/// </summary>
public sealed class RequestLoggingTermoDeBuscaTests
{
    private const string TermoFicticio = "termo-ficticio-alfa";

    public static readonly TheoryData<string, string, string> ConsultasComTextoDoCliente = new()
    {
        { "GET", "/api/configuracao/cursos", "q" },
        { "GET", "/api/configuracao/ofertas-curso", "q" },
        { "GET", "/api/organizacao/unidades", "q" },
        { "GET", "/api/configuracao/termos-consentimento", "q" },
        { "GET", "/api/selecao/certames", "q" },
        { "GET", "/api/configuracao/admin/protecao-do-catalogo/decisoes", "busca" },
        { "GET", "/api/autorizacao/concessoes", "contaId" },
        { "POST", "/api/selecao/processos-seletivos/{id}/inscricoes/{inscricaoId}/campos/{fatoCodigo}/revelacao", "ocorrencia" },
    };

    public static readonly TheoryData<string, string, string, int> ConsultasComTextoDoClientePorResposta = ComCadaResposta();

    [Theory]
    [MemberData(nameof(ConsultasComTextoDoClientePorResposta))]
    public async Task InvokeAsync_ConsultaComTextoDoCliente_NaoGravaOValorEmNenhumaResposta(string metodo, string caminho, string parametro, int status)
    {
        List<LogEvent> eventos = await ExecutarAsync(caminho, $"?{parametro}={TermoFicticio}", status, metodo: metodo);

        LogEvent log = eventos.Should().ContainSingle().Subject;
        TextoCompleto(log).Should().NotContain(TermoFicticio);
        log.Properties["Query"].ToString().Trim('"').Should().Be($"?{parametro}=***");
    }

    [Theory]
    [MemberData(nameof(ConsultasComTextoDoCliente))]
    public async Task InvokeAsync_ConsultaComTextoDoClienteQueLancaExcecao_NaoGravaOValor(string metodo, string caminho, string parametro)
    {
        List<LogEvent> eventos = await ExecutarAsync(
            caminho, $"?{parametro}={TermoFicticio}", status: 200, falha: new InvalidOperationException("falha"), metodo: metodo);

        LogEvent log = eventos.Should().ContainSingle().Subject;
        log.Level.Should().Be(LogEventLevel.Error);
        TextoCompleto(log).Should().NotContain(TermoFicticio);
    }

    [Theory]
    [InlineData("Q")]
    [InlineData("%71")]
    [InlineData("BUSCA")]
    [InlineData("%62usca")]
    public async Task InvokeAsync_NomeDoParametroComOutraGrafia_NaoGravaOTermo(string nome)
    {
        List<LogEvent> eventos = await ExecutarAsync("/api/configuracao/cursos", $"?{nome}=termo-ficticio-gama", status: 200);

        TextoCompleto(eventos.Should().ContainSingle().Subject).Should().NotContain("termo-ficticio-gama");
    }

    [Fact]
    public async Task InvokeAsync_BuscaRepetida_NaoGravaNenhumDosTermos()
    {
        List<LogEvent> eventos = await ExecutarAsync("/api/configuracao/cursos", "?q=termo-ficticio-alfa&q=termo-ficticio-beta", status: 200);

        LogEvent log = eventos.Should().ContainSingle().Subject;
        TextoCompleto(log).Should().NotContain("termo-ficticio");
        log.Properties["Query"].ToString().Trim('"').Should().Be("?q=***&q=***");
    }

    [Fact]
    public async Task InvokeAsync_BuscaComOutrosParametros_MantemLimiteDirecaoEFiltroPorCodigo()
    {
        List<LogEvent> eventos = await ExecutarAsync(
            "/api/selecao/certames", "?limit=20&direction=forward&situacao=PUBLICADO&q=termo-ficticio-delta", status: 200);

        string consulta = eventos.Should().ContainSingle().Subject.Properties["Query"].ToString().Trim('"');
        consulta.Should().Contain("limit=20").And.Contain("direction=forward").And.Contain("situacao=PUBLICADO");
        consulta.Should().NotContain("termo-ficticio-delta");
    }

    private static TheoryData<string, string, string, int> ComCadaResposta()
    {
        TheoryData<string, string, string, int> dados = [];
        foreach (object[] consulta in ConsultasComTextoDoCliente)
        {
            foreach (int status in new[] { StatusCodes.Status200OK, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError })
            {
                dados.Add((string)consulta[0], (string)consulta[1], (string)consulta[2], status);
            }
        }

        return dados;
    }

    private static string TextoCompleto(LogEvent log) =>
        log.RenderMessage(CultureInfo.InvariantCulture) + string.Concat(log.Properties.Values.Select(static valor => valor.ToString()))
        + (log.Exception?.ToString() ?? string.Empty);

    private static async Task<List<LogEvent>> ExecutarAsync(string caminho, string consulta, int status, Exception? falha = null, string metodo = "GET")
    {
        DefaultHttpContext contexto = CriarContexto(metodo, caminho, consulta);
        List<LogEvent> eventos = [];
        await using Logger logger = new LoggerConfiguration().WriteTo.Sink(new CapturaDeEventos(eventos)).CreateLogger();
        using SerilogLoggerFactory fabrica = new(logger);

        await InvocarIgnorandoFalhaAsync(CriarMiddleware(fabrica, contexto, status, falha), contexto);

        return eventos;
    }

    private static DefaultHttpContext CriarContexto(string metodo, string caminho, string consulta)
    {
        DefaultHttpContext contexto = new();
        contexto.Request.Method = metodo;
        contexto.Request.Path = caminho;
        contexto.Request.QueryString = new QueryString(consulta);
        return contexto;
    }

    private static RequestLoggingMiddleware CriarMiddleware(ILoggerFactory fabrica, HttpContext contexto, int status, Exception? falha) =>
        new(_ => ResponderAsync(contexto, status, falha), fabrica.CreateLogger<RequestLoggingMiddleware>(), CriarMasker(), CriarOpcoes());

    // A falha é repassada ao middleware de erros; aqui interessa só o registro que ficou no log.
    private static async Task InvocarIgnorandoFalhaAsync(RequestLoggingMiddleware middleware, HttpContext contexto)
    {
        try
        {
            await middleware.InvokeAsync(contexto);
        }
        catch (InvalidOperationException)
        {
            // Esperado nos cenários de falha do servidor.
        }
    }

    private static Task ResponderAsync(HttpContext contexto, int status, Exception? falha)
    {
        contexto.Response.StatusCode = status;
        return falha is null ? Task.CompletedTask : Task.FromException(falha);
    }

    private static QueryStringMasker CriarMasker() => new(CriarOpcoes());

    private static IOptions<RequestLoggingOptions> CriarOpcoes() => Options.Create(new RequestLoggingOptions());

    private sealed class CapturaDeEventos(List<LogEvent> eventos) : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            ArgumentNullException.ThrowIfNull(logEvent);
            eventos.Add(logEvent);
        }
    }
}
