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
        TipoEtapa tipo = TipoEtapa.Criar("PROVA", "Prova", null, true, true).Value!;
        tipo.SubstituirConceitosAdmitidos(conceitos);
        return tipo;
    }

    [Fact(DisplayName = "O DTO traz código, nome e natureza de cada conceito admitido, ordenados pelo código")]
    public void ToDto_TrazOsConceitosAdmitidosOrdenados()
    {
        TipoEtapaDto dto = TipoComConceitos(TiposConceitoDeTeste.Numerico(), TiposConceitoDeTeste.Categorico()).ToDto();

        dto.ConceitosAdmitidos.Should().Equal(
            new ConceitoAdmitidoDto("NOTA_ENTREVISTA", "Nota da entrevista", "numerico"),
            new ConceitoAdmitidoDto("PARECER_ANALISE", "Parecer da análise", "categorico"));
    }

    [Fact(DisplayName = "O DTO do tipo sem conceito admitido traz lista vazia, não nula")]
    public void ToDto_SemConceitos_TrazListaVazia()
    {
        TipoEtapaDto dto = TipoComConceitos().ToDto();

        dto.ConceitosAdmitidos.Should().NotBeNull().And.BeEmpty();
    }
}
