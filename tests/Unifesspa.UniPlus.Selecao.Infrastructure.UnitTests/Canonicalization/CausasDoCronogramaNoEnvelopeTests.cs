namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CausasDoCronogramaNoEnvelopeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void LerCronograma_CategoriaRepetidaNasBancas_PropagaUmaCausaPorBanca(int quantidade)
    {
        JsonObject documento = Documento(quantidade);
        LeitorEnvelope leitor = new();
        LeituraDoCronograma.LerCronogramaFases(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(quantidade);
        for (int indice = 0; indice < quantidade; indice++)
        {
            ConferirCausaNoDocumento.Conferir(documento, causas[indice], "cronogramaFases.fases[0]",
                "BancaRequerida.CategoriaRepetidaNosCandidatosAtendidos",
                $"Na fase, a banca 'Banca de análise documental {indice + 1}' declara a mesma categoria de documento mais de uma vez nos candidatos atendidos.");
            causas[indice].Error.Message.Should().NotContain("VALOR_FICTICIO_X");
            causas[indice].Field.Should().NotContain("VALOR_FICTICIO_X");
        }
    }

    private static JsonObject Documento(int quantidade)
    {
        JsonObject fase = Fase();
        JsonArray bancas = fase["bancasRequeridas"]!.AsArray();
        JsonObject banca = bancas[0]!.AsObject();
        JsonArray categorias = banca["candidatosAtendidos"]!.AsArray();
        categorias.Add(categorias[0]!.DeepClone());
        if (quantidade == 2)
        {
            JsonObject segunda = banca.DeepClone().AsObject();
            segunda["id"] = Guid.NewGuid().ToString();
            segunda["chave"] = "banca_dois";
            segunda["posicaoNoTipo"] = 2;
            bancas.Add(segunda);
        }
        return new JsonObject
        {
            ["cronogramaFases"] = new JsonObject
            {
                ["origemCandidatos"] = OrigemCandidatos.ImportacaoExterna.Codigo(),
                ["fases"] = new JsonArray(fase),
            }
        };
    }

    private static JsonObject Fase()
    {
        TipoDeBancaCopiado tipo = new(Guid.NewGuid(), TipoDeBanca.AnaliseDocumental, "Banca", PapelDaBanca.AvaliaOMerito, false, false, []);
        CategoriaJulgada categoria = CategoriaJulgada.Criar(Guid.NewGuid(), "VALOR_FICTICIO_X", "Categoria fictícia");
        BancaRequerida banca = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, tipo, [categoria]);
        OrigemCanonicaDaFase origem = OrigensCanonicasDeTeste.Da(Guid.NewGuid(), "SELECAO", OrigemDataFase.NoEdital, false, false, false, false);
        FaseCronograma fase = FaseCronograma.Reidratar(ChaveLocal.Criar("fase_selecao").Value, Guid.NewGuid(), origem,
            new DecisoesDeCronogramaDaFase(1, null, null, null), new PublicacoesDaFase([], false, null), [banca]);
        return JsonNode.Parse(SerializacaoDoCronograma.SerializarFase(fase).ToJsonString())!.AsObject();
    }
}
