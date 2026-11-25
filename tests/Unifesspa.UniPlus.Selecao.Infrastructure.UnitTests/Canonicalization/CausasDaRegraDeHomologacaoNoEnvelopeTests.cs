namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDaRegraDeHomologacaoNoEnvelopeTests
{
    private const string Condicao = "fase.regraDeHomologacao.alternativas[0].condicoes[0]";
    private const string Enquadramento = Condicao + ".enquadramento";

    [Theory]
    [InlineData(1997)]
    [InlineData(2101)]
    public void Ler_EdicaoInvalida_PropagaCausa(int edicao)
    {
        JsonObject documento = Documento();
        Item(documento)["edicaoDoExame"] = edicao;
        LeitorEnvelope leitor = Ler(documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().ContainSingle();
        Conferir(documento, causas[0], Condicao + ".edicaoDoExame", HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida);
        causas[0].Error.Message.Should().Be("A edição do exame deve ser um ano entre 1998 e 2100.");
    }

    [Fact]
    public void Ler_MultiplasCausasDaCondicao_PropagaNaOrdem()
    {
        JsonObject documento = Documento();
        JsonObject item = Item(documento);
        item["tipo"] = "VALOR_FICTICIO_X";
        item["edicaoDoExame"] = 1997;
        item["motivoDesfavoravelCodigo"] = "VALOR_FICTICIO_X mal";
        IReadOnlyList<FieldError> causas = Ler(documento).Falha<object>().Errors;
        causas.Should().HaveCount(3);
        Conferir(documento, causas[0], Condicao + ".tipo", HomologacaoDasInscricoesErrorCodes.CondicaoNaoDisponivel);
        Conferir(documento, causas[1], Condicao + ".edicaoDoExame", HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida);
        Conferir(documento, causas[2], Condicao + ".motivoDesfavoravelCodigo", MotivoDeDecisaoErrorCodes.CodigoFormatoInvalido);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void Ler_EnquadramentoInvalido_PropagaCausas(bool separadas, bool unica)
    {
        JsonObject documento = Documento();
        Item(documento)["enquadramento"] = CondicoesNoEnvelope.Invalidas(separadas, unica);
        LeitorEnvelope leitor = Ler(documento);
        CondicoesNoEnvelope.Conferir(documento, leitor, Enquadramento, separadas, unica);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void Ler_TipoEEnquadramentoInvalidos_PropagaCausasNaOrdem(bool separadas, bool unica)
    {
        JsonObject documento = Documento();
        Item(documento)["tipo"] = "VALOR_FICTICIO_X";
        Item(documento)["enquadramento"] = CondicoesNoEnvelope.Invalidas(separadas, unica);
        IReadOnlyList<FieldError> causas = Ler(documento).Falha<object>().Errors;
        causas.Should().HaveCount(unica ? 2 : 3);
        Conferir(documento, causas[0], Condicao + ".tipo", HomologacaoDasInscricoesErrorCodes.CondicaoNaoDisponivel);
        CondicoesNoEnvelope.ConferirCausa(documento, causas[1], Enquadramento + "[0][0].valor", !unica);
        if (!unica)
        {
            CondicoesNoEnvelope.ConferirCausa(documento, causas[2], $"{Enquadramento}[{(separadas ? 1 : 0)}][{(separadas ? 0 : 1)}].valor", false);
        }
    }

    [Fact]
    public void Ler_AlternativaVazia_PropagaCausa()
    {
        JsonObject documento = Documento();
        documento["fase"]!["regraDeHomologacao"]!["alternativas"]![0]!["condicoes"] = new JsonArray();
        IReadOnlyList<FieldError> causas = Ler(documento).Falha<object>().Errors;
        causas.Should().ContainSingle();
        Conferir(documento, causas[0], "fase.regraDeHomologacao.alternativas[0].condicoes", HomologacaoDasInscricoesErrorCodes.AlternativaSemCondicao);
    }

    private static JsonObject Documento() => JsonNode.Parse("""
        {"fase":{"regraDeHomologacao":{"resultadoAtoCodigo":null,"alternativas":[{"condicoes":[
        {"tipo":"RESULTADO_NO_RETORNO_DO_INEP","edicaoDoExame":2024,"motivoDesfavoravelCodigo":null,"enquadramento":null}
        ]}]}}}
        """)!.AsObject();

    private static JsonObject Item(JsonObject documento) =>
        documento["fase"]!["regraDeHomologacao"]!["alternativas"]![0]!["condicoes"]![0]!.AsObject();

    private static LeitorEnvelope Ler(JsonObject documento)
    {
        LeitorEnvelope leitor = new();
        RegraDeHomologacaoNoEnvelope.Ler(leitor, documento["fase"]!.AsObject(), "fase").Should().BeNull();
        leitor.Falhou.Should().BeTrue();
        return leitor;
    }

    private static void Conferir(JsonObject documento, FieldError causa, string campo, string codigo)
    {
        causa.Field.Should().Be(campo);
        causa.Error.Code.Should().Be(codigo);
        CaminhoNoDocumento.Existe(documento, campo).Should().BeTrue();
        causa.Field.Should().NotContain("VALOR_FICTICIO_X");
        causa.Error.Message.Should().NotContain("VALOR_FICTICIO_X");
    }
}
