namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Assertions;

using System.Net;
using System.Net.Http;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

public sealed class HttpResponsePiiAssertionsTests
{
    // ─── AssertBodyNoPii — sem PII ─────────────────────────────────────────

    [Fact]
    public void AssertBodyNoPii_DadoResponseSemPii_NaoDeveGerarFalha()
    {
        const string body = """
            {
              "type": "https://uniplus.unifesspa.edu.br/errors/uniplus.selecao.edital.nao_encontrado",
              "title": "Edital não encontrado",
              "status": 404,
              "detail": "Edital não encontrado.",
              "instance": "urn:uuid:01960000-0000-7000-0000-000000000001",
              "code": "uniplus.selecao.edital.nao_encontrado",
              "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
            }
            """;

        Action acao = () => HttpResponsePiiAssertions.AssertBodyNoPii(body);

        acao.Should().NotThrow();
    }

    [Fact]
    public void AssertBodyNoPii_DadoBodyVazio_NaoDeveGerarFalha()
    {
        Action acao = () => HttpResponsePiiAssertions.AssertBodyNoPii(string.Empty);

        acao.Should().NotThrow();
    }

    // ─── CA-02: detecta CPF ─────────────────────────────────────────────────

    [Fact]
    public void AssertBodyNoPii_DadoCpfNaoMascaradoNoDetail_DeveFalhar()
    {
        const string body = """{"detail": "CPF 529.982.247-25 já cadastrado."}""";

        Exception? excecao = Record.Exception(
            () => HttpResponsePiiAssertions.AssertBodyNoPii(body));

        excecao.Should().NotBeNull();
        excecao!.Message.Should().Contain("CPF não mascarado");
    }

    // ─── Regressão P1: traceId/UUID com dígitos adjacentes a letra hex não disparam
    //     o padrão CPF cru (falso positivo reportado pelo Codex) ──────────────────

    [Fact]
    public void AssertBodyNoPii_DadoTraceIdComSequenciaDigitosAdjacenteALetraHex_NaoDeveGerarFalha()
    {
        // "a12345678901b" — 11 dígitos adjacentes a letras hex: padrão antigo
        // (?<!\d)\d{11}(?!\d) produzia falso positivo; padrão novo bloqueia.
        const string body = """
            {
              "type": "https://uniplus.unifesspa.edu.br/errors/uniplus.selecao.edital.nao_encontrado",
              "title": "Edital não encontrado",
              "status": 404,
              "detail": "Edital não encontrado.",
              "instance": "urn:uuid:01960000-0000-7000-0000-0000000000a1",
              "code": "uniplus.selecao.edital.nao_encontrado",
              "traceId": "a12345678901bcdef0123456789abcde"
            }
            """;

        Action acao = () => HttpResponsePiiAssertions.AssertBodyNoPii(body);

        acao.Should().NotThrow();
    }

    // ─── CA-02: detecta CPF sem formatação ─────────────────────────────────

    [Fact]
    public void AssertBodyNoPii_DadoCpfCruNoDetail_DeveFalhar()
    {
        const string body = """{"detail": "CPF 52998224725 já cadastrado."}""";

        Exception? excecao = Record.Exception(
            () => HttpResponsePiiAssertions.AssertBodyNoPii(body));

        excecao.Should().NotBeNull();
        excecao!.Message.Should().Contain("CPF não mascarado (sem formatação)");
    }

    // ─── CA-02: detecta e-mail ──────────────────────────────────────────────

    [Fact]
    public void AssertBodyNoPii_DadoEmailNoCampoMessage_DeveFalhar()
    {
        const string body = """
            {
              "errors": [{"field": "email", "code": "Email.Invalido", "message": "usuario@unifesspa.edu.br inválido"}]
            }
            """;

        Exception? excecao = Record.Exception(
            () => HttpResponsePiiAssertions.AssertBodyNoPii(body));

        excecao.Should().NotBeNull();
        excecao!.Message.Should().Contain("e-mail completo");
    }

    // ─── CA-02: detecta nome + CPF combinados ───────────────────────────────

    [Fact]
    public void AssertBodyNoPii_DadoNomeSeguidoDeCpf_DeveFalhar()
    {
        const string body = """{"detail": "Candidato João Silva 123.456.789-00 não encontrado"}""";

        Exception? excecao = Record.Exception(
            () => HttpResponsePiiAssertions.AssertBodyNoPii(body));

        excecao.Should().NotBeNull();
        excecao!.Message.Should().Contain("nome + CPF combinados");
    }

    // ─── CA-03: mensagem indica campo JSON ──────────────────────────────────

    [Fact]
    public void AssertBodyNoPii_DadoCpfEmCampoAninhado_MensagemDeveIndicarCampo()
    {
        const string body = """
            {
              "errors": [{"field": "cpf", "code": "Cpf.Invalido", "message": "529.982.247-25 inválido"}]
            }
            """;

        Exception? excecao = Record.Exception(
            () => HttpResponsePiiAssertions.AssertBodyNoPii(body));

        excecao.Should().NotBeNull();
        excecao!.Message.Should().Contain("errors[0].message");
    }

    // ─── AssertNoPiiAsync — via HttpResponseMessage ─────────────────────────

    [Fact]
    public async Task AssertNoPiiAsync_DadoResponseComCpfNoBody_DeveFalhar()
    {
        using HttpResponseMessage response = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"detail": "CPF 000.000.000-00 inválido"}""",
                System.Text.Encoding.UTF8,
                "application/json"),
        };

        Exception? excecao = await Record.ExceptionAsync(
            () => response.AssertNoPiiAsync());

        excecao.Should().NotBeNull();
        excecao!.Message.Should().Contain("CPF não mascarado");
    }
}
