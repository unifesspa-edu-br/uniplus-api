namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.NotasDoEnem;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Leiautes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ImportacaoDeNotasPreparadaTests
{
    [Fact]
    public void Criar_ColecoesDeOrigemAlteradas_ConservaLinhasAreasEIndices()
    {
        List<ResultadoDaAreaNoRetorno> areas = [new("REDACAO", 600m, SituacaoDaProvaNoEnem.Presente)];
        List<LinhaDeRetorno> linhas = [new(1, "***.999.999-**", null, null, string.Empty, areas)];
        Dictionary<string, IndiceCegoDoCpf> indices = new(StringComparer.Ordinal) { [linhas[0].Cpf] = IndiceCegoDoCpf.De([1]) };
        ImportacaoDeNotasPreparada preparada = new(Leiaute(), new string('a', 64), linhas, indices);
        areas.Clear();
        linhas.Clear();
        indices.Clear();
        preparada.Linhas.Should().ContainSingle().Which.Areas.Should().ContainSingle().Which.Nota.Should().Be(600m);
        preparada.IndicePorCpf.Should().ContainSingle();
    }

    private static LeiauteDeRetornoDoInep Leiaute() => (LeiauteDeRetornoDoInep)CatalogoDeLeiautesDoInep.Padrao
        .Obter("INEP_ENEM_RETORNO", "v1", TipoDeLeiauteDoInep.Retorno)!;
}
