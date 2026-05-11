namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Middleware;

using System.Net.Http;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

// Smoke tests do CorrelationIdMiddleware via WebApplicationFactory<Program>
// (issues #116 / #117). Cobrem o que os 25 testes unitários do middleware
// (DefaultHttpContext) NÃO conseguem cobrir: ordem de registro em
// Program.cs, propagação ao longo do pipeline real e comportamento
// post-OnStarting com a resposta sendo flushada.
//
// Cada cenário é uma proteção contra regressão de wiring específica:
//
//   CA-03 (gerar): se `AddCorrelationIdMiddleware` for removido do
//          ServiceCollection ou `UseMiddleware<CorrelationIdMiddleware>`
//          sair do Program.cs, o header some.
//   CA-04 (ecoar): se o middleware mover para depois do response start
//          handler, OnStarting não é chamado e o valor do client é
//          descartado.
//   CA-05 (sanitizar): se o regex de validação degradar para algo mais
//          permissivo (ex.: deixar passar `\r\n`), o teste falha — defesa
//          contra log injection cross-pipeline.
public sealed class CorrelationIdMiddlewareSmokeTests : IClassFixture<InfraCoreApiFactory>
{
    private static readonly Uri HealthUri = new("/health", UriKind.Relative);

    private readonly InfraCoreApiFactory _factory;

    public CorrelationIdMiddlewareSmokeTests(InfraCoreApiFactory factory) => _factory = factory;

    [Fact(DisplayName = "GET /health sem X-Correlation-Id — resposta contém UUID v4 gerado")]
    public async Task SemHeader_DeveGerarCorrelationIdUuid()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(HealthUri);

        string correlationId = ExtrairCorrelationId(response);
        ValidarUuidV4(correlationId);
    }

    [Fact(DisplayName = "GET /health com X-Correlation-Id válido — resposta ecoa o valor enviado")]
    public async Task ComHeaderValido_DeveEcoarMesmoValor()
    {
        const string idEnviado = "abc-123";
        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, HealthUri);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, idEnviado);

        HttpResponseMessage response = await client.SendAsync(request);

        string correlationId = ExtrairCorrelationId(response);
        correlationId.Should().Be(idEnviado);
    }

    [Fact(DisplayName = "GET /health com X-Correlation-Id contendo CRLF — resposta sanitiza com UUID v4 distinto (log injection defense)")]
    public async Task ComHeaderCrLf_DeveDescartarValorSanitizando()
    {
        // O middleware aceita apenas `^[A-Za-z0-9\-_.]{1,128}$`. CRLF cai fora
        // do alfabeto permitido e dispara o fallback para Guid.NewGuid().
        //
        // `TryAddWithoutValidation` é necessário porque o HttpClient valida o
        // valor do header antes de enviar e rejeita CRLF cliente-side com
        // FormatException. O TestServer em uso pelo WebApplicationFactory é
        // mais permissivo do que o Kestrel produtivo nesse aspecto — aceita
        // o byte e entrega ao middleware, que então valida via regex e cai
        // para UUID. Em produção, Kestrel rejeitaria com 400; o smoke aqui
        // garante a defesa de profundidade do middleware em qualquer
        // transporte que entregue o valor cru.
        const string idForjado = "abc\r\n[admin] forjado";
        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, HealthUri);

        // Provar que o header REALMENTE saiu com o valor CRLF antes de afirmar
        // que o servidor o sanitizou. Sem isso, se uma versão futura do
        // HttpClient passar a rejeitar CRLF silenciosamente (TryAddWithout-
        // Validation retorna false), o request sairia sem header e o teste
        // viraria falso positivo do cenário "sem header → UUID gerado".
        bool adicionou = request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, idForjado);
        adicionou.Should().BeTrue(
            "TryAddWithoutValidation deve aceitar CRLF; se rejeitar, o smoke não exercita CA-05");
        request.Headers.GetValues(CorrelationIdMiddleware.HeaderName)
            .Should().ContainSingle().Which.Should().Be(idForjado,
                "header precisa carregar o valor CRLF intacto para validar a defesa do middleware");

        HttpResponseMessage response = await client.SendAsync(request);

        string correlationId = ExtrairCorrelationId(response);
        correlationId.Should().NotBe(idForjado,
            "CRLF não passa pela regex de validação e o middleware deve gerar um UUID novo");
        ValidarUuidV4(correlationId);
    }

    private static string ExtrairCorrelationId(HttpResponseMessage response)
    {
        response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out IEnumerable<string>? valores)
            .Should().BeTrue($"resposta deveria conter o header `{CorrelationIdMiddleware.HeaderName}`");
        string? correlationId = valores?.FirstOrDefault();
        correlationId.Should().NotBeNullOrWhiteSpace();
        return correlationId!;
    }

    private static void ValidarUuidV4(string correlationId)
    {
        Guid.TryParse(correlationId, out Guid parsed).Should().BeTrue(
            $"correlation id `{correlationId}` deveria ser um UUID parseável");
        // RFC 9562 §5.4 — versão fica nos 4 bits altos do byte 7 do payload
        // big-endian. Em .NET 10 `Guid` é little-endian para os primeiros 3
        // grupos, então o byte físico que carrega a versão é o índice 7 do
        // ToByteArray() (não o índice 6).
        byte versionByte = parsed.ToByteArray()[7];
        int version = (versionByte & 0xF0) >> 4;
        version.Should().Be(4, "Guid.NewGuid() emite RFC 9562 versão 4 random-based");
    }
}
