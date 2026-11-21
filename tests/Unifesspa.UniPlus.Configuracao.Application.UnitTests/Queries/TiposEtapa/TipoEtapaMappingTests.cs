namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries.TiposEtapa;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public sealed class TipoEtapaMappingTests
{
    private static TipoEtapa TipoComConceitos(params TipoConceito[] conceitos)
    {
        TipoEtapa tipo = TipoEtapa.Criar("PROVA", "Prova", null, true, true, false).Value!;
        tipo.SubstituirConceitosAdmitidos(conceitos);
        return tipo;
    }

    [Fact(DisplayName = "O DTO traz código, nome e natureza de cada conceito admitido, ordenados pelo código")]
    public void ToDto_TrazOsConceitosAdmitidosOrdenados()
    {
        TipoEtapaDto dto = TipoComConceitos(TiposConceitoDeTeste.Numerico(), TiposConceitoDeTeste.Categorico()).ToDto();

        dto.Avaliacao.ConceitosAdmitidos.Select(static c => (c.Codigo, c.Nome, c.Natureza.Codigo, c.Natureza.Rotulo)).Should().Equal(
            ("NOTA_ENTREVISTA", "Nota da entrevista", "NUMERICO", "Numérico"),
            ("PARECER_ANALISE", "Parecer da análise", "CATEGORICO", "Categórico"));
    }

    [Fact(DisplayName = "O DTO do tipo sem conceito admitido traz lista vazia, não nula")]
    public void ToDto_SemConceitos_TrazListaVazia()
    {
        TipoEtapaDto dto = TipoComConceitos().ToDto();

        dto.Avaliacao.ConceitosAdmitidos.Should().NotBeNull().And.BeEmpty();
    }
}
