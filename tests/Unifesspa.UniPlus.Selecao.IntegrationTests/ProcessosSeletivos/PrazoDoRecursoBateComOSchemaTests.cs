namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// Os dois prazos do recurso, o de interposição e o da 2ª instância, têm nas colunas da fase e da etapa a precisão e a escala
/// com que o decodificador do envelope os lê. Como em <see cref="LimitesDoEnvelopeBatemComOSchemaTests"/>, lê o modelo do EF
/// Core em memória, sem conexão: uma coluna mais estreita produz um envelope que o decodificador aprova e o INSERT recusa.
/// </summary>
public sealed class PrazoDoRecursoBateComOSchemaTests
{
    // A escala com que o decodificador lê todo prazo de recurso (o EscalaPadrao do codec). A precisão tem constante própria em
    // LimitesDoEnvelope; a escala não, e sem confrontá-la uma coluna numeric(18,2) passaria batido, arredondando o prazo que o
    // edital prometeu.
    private const int EscalaDoPrazo = 4;

    private static readonly string[] PrazosDoRecurso =
    [
        nameof(ParametrosRegraPrazoRecurso.PrazoDeInterposicao),
        nameof(ParametrosRegraPrazoRecurso.PrazoDaSegundaInstancia),
    ];

    [Fact(DisplayName = "Os prazos do recurso da fase têm a precisão e a escala que o decodificador do envelope lê")]
    public void PrazosDaFase_BatemComOSchema()
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();

        ConferirOsPrazos(ModeloEmMemoria.EmbutidoDe<RegraRecursoFase>(contexto, nameof(RegraRecursoFase.Parametros)));
    }

    [Fact(DisplayName = "Os prazos do recurso da etapa têm a precisão e a escala que o decodificador do envelope lê")]
    public void PrazosDaEtapa_BatemComOSchema()
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();

        ConferirOsPrazos(ModeloEmMemoria.EmbutidoDe<RecursoDaEtapa>(contexto, nameof(RecursoDaEtapa.Parametros)));
    }

    private static void ConferirOsPrazos(IEntityType parametros)
    {
        foreach (string prazo in PrazosDoRecurso)
        {
            IProperty coluna = parametros.GetNavigations().Single(n => n.Name == prazo).TargetEntityType
                .FindProperty(nameof(PrazoDoRecurso.Valor))!;

            coluna.GetPrecision().Should().Be(LimitesDoEnvelope.PrecisaoPrazo, $"LimitesDoEnvelope.PrecisaoPrazo espelha a coluna de {prazo}");
            coluna.GetScale().Should().Be(EscalaDoPrazo, $"uma coluna com menos casas arredonda {prazo} em silêncio, e a prova de ida e volta não enxerga arredondamento");
        }
    }
}
