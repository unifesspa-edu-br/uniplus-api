namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ClassificacaoDeAtoRecusadaNoEnvelopeTests
{
    [Fact]
    public void LerTiposDeAto_TipoDeclaradoSemClassificacao_NaoRepeteValor()
    {
        const string valor = "EXEMPLO_ATO_SEM_CLASSIFICACAO";
        JsonObject documento = Documento(valor);
        LeitorEnvelope leitor = new();
        IReadOnlyList<FaseCronograma> fases = LeituraDoCronograma.LerCronogramaFases(leitor, documento);
        LeituraDosTiposDeAto.LerTiposDeAto(leitor, documento, fases, []);
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Be("Envelope malformado em 'tiposDeAto': um tipo declarado no cronograma não tem classificação congelada.").And.NotContain(valor);
        causa.Field.Should().BeNull();
    }

    private static JsonObject Documento(string valor)
    {
        OrigemCanonicaDaFase origem = OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(Guid.NewGuid(), "SELECAO", OrigemDataFase.NoEdital, false, false, false, false)) with { Nome = "Seleção" };
        FaseCronograma fase = FaseCronograma.Reidratar(ChaveLocal.Criar("fase_selecao").Value!, Guid.NewGuid(), origem,
            new DecisoesDeCronogramaDaFase(1, null, null, null), new PublicacoesDaFase([ProdutoDaFase.Criar(valor, null)], false, null), []);
        return new JsonObject
        {
            ["cronogramaFases"] = new JsonObject { ["origemCandidatos"] = "IMPORTACAO_EXTERNA", ["fases"] = new JsonArray(JsonNode.Parse(SerializacaoDoCronograma.SerializarFase(fase).ToJsonString())) },
            ["tiposDeAto"] = new JsonArray(),
        };
    }
}
