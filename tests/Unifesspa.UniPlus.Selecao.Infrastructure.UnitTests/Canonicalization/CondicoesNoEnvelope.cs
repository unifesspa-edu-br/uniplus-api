namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

internal static class CondicoesNoEnvelope
{
    internal static JsonObject Condicao(string operador, JsonNode? valor) => new()
    {
        ["codigoDoDado"] = "DADO",
        ["operador"] = operador,
        ["valor"] = valor,
    };

    internal static JsonArray Invalidas(bool clausulasSeparadas, bool unica = false) => unica
        ? new JsonArray(new JsonArray(Condicao("IGUAL", new JsonArray("VALOR_FICTICIO_X"))))
        : clausulasSeparadas
        ? new JsonArray(new JsonArray(Condicao("ALGUM_DE", JsonValue.Create("A"))),
            new JsonArray(Condicao("IGUAL", new JsonArray("VALOR_FICTICIO_X"))))
        : new JsonArray(new JsonArray(Condicao("ALGUM_DE", JsonValue.Create("A")),
            Condicao("IGUAL", new JsonArray("VALOR_FICTICIO_X"))));

    internal static void Conferir(JsonObject documento, LeitorEnvelope leitor, string caminho, bool separadas, bool unica = false)
    {
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(unica ? 1 : 2);
        ConferirCausa(documento, causas[0], $"{caminho}[0][0].valor", !unica);
        if (!unica)
        {
            ConferirCausa(documento, causas[1], $"{caminho}[{(separadas ? 1 : 0)}][{(separadas ? 0 : 1)}].valor", false);
        }
    }

    internal static void ConferirCausa(JsonObject documento, FieldError causa, string campo, bool lista)
    {
        string mensagem = lista
            ? "Os operadores ALGUM_DE e NENHUM_DE exigem um array JSON, sem itens de texto em branco (array vazio é aceito)."
            : "Os operadores IGUAL, DIFERENTE, MAIOR_IGUAL e MENOR_IGUAL exigem um valor JSON escalar não branco.";
        causa.Should().Be(new FieldError(campo, new DomainError("CondicaoDnf.FormaIncoerenteComOperador", mensagem)));
        CaminhoNoDocumento.Existe(documento, campo).Should().BeTrue();
        causa.Error.Message.Should().NotContain("VALOR_FICTICIO_X");
        causa.Field.Should().NotContain("VALOR_FICTICIO_X");
    }
}
