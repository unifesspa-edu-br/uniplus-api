namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ChavesDasExigenciasNoEnvelopeTests
{
    private static JsonObject Exigencia() => JsonNode.Parse("""
        {"chave":"exigencia_identidade_2","exigenciaId":"11111111-1111-4111-8111-111111111111",
         "tipoDocumentoOrigemId":"22222222-2222-4222-8222-222222222222","tipoDocumentoCodigo":"IDENTIDADE",
         "tipoDocumentoNome":"Identidade","tipoDocumentoCategoria":"PESSOAL","classificacaoProtecao":"PESSOAL",
         "finalidadeTratamento":"Comprovação","hipoteseLegal":"Obrigação legal","dadosComprovados":[],
         "exigidoNaFaseId":"33333333-3333-4333-8333-333333333333","exigidoNaEtapaId":null,
         "finalidade":null,"aplicabilidade":"GERAL","obrigatorio":true,"consequenciaIndeferimento":null,
         "condicoes":null,"basesLegais":[],"prazoDeEmissao":null,"validade":null,
         "formatosPermitidos":{"qualquer":true,"lista":null},"tamanhoMaximoBytes":null,"modelo":null}
        """)!.AsObject();

    private static IReadOnlyList<DocumentoExigido> Ler(LeitorEnvelope leitor, params JsonObject[] exigencias) =>
        LeituraDasExigencias.LerExigencias(leitor, new JsonObject { ["exigencias"] = new JsonArray(exigencias) });

    [Fact]
    public void Ler_ChaveInformada_PreservaNaExigencia()
    {
        LeitorEnvelope leitor = new();
        IReadOnlyList<DocumentoExigido> exigencias = Ler(leitor, Exigencia());

        leitor.Falhou.Should().BeFalse();
        exigencias.Should().ContainSingle().Which.Chave.Valor.Should().Be("exigencia_identidade_2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Exigencia")]
    [InlineData("exigencia__identidade")]
    public void Ler_ChaveAusenteOuMalformada_Recusa(string? chave)
    {
        JsonObject exigencia = Exigencia();
        if (chave is null)
        {
            exigencia.Remove("chave");
        }
        else
        {
            exigencia["chave"] = chave;
        }

        LeitorEnvelope leitor = new();
        Ler(leitor, exigencia);
        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("chave");
    }

    [Fact]
    public void Ler_ChaveRepetida_Recusa()
    {
        JsonObject exigencia = Exigencia();
        LeitorEnvelope leitor = new();
        Ler(leitor, exigencia, exigencia.DeepClone().AsObject());

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("exigencia_identidade_2");
    }
}
