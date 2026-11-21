namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class ListarVocabulariosDaAvaliacaoQueryHandlerTests
{
    [Fact(DisplayName = "O vocabulário dos resultados ternários devolve código e rótulo na ordem declarada no enum")]
    public void Handle_Ternarios_DevolveNaOrdemDoEnumComRotulo()
    {
        IReadOnlyList<CodigoComRotulo<Ternario>> ternarios =
            ListarTernariosQueryHandler.Handle(new ListarTernariosQuery());

        ternarios.Select(static t => (t.Codigo, t.Rotulo)).Should().Equal(
            ("INDETERMINADO", "Indeterminado"),
            ("VERDADEIRO", "Verdadeiro"),
            ("FALSO", "Falso"));
    }

    [Fact(DisplayName = "O vocabulário dos estados de fato devolve código e rótulo na ordem declarada no enum")]
    public void Handle_EstadosFato_DevolveNaOrdemDoEnumComRotulo()
    {
        IReadOnlyList<CodigoComRotulo<EstadoFato>> estados =
            ListarEstadosFatoQueryHandler.Handle(new ListarEstadosFatoQuery());

        estados.Select(static e => (e.Codigo, e.Rotulo)).Should().Equal(
            ("INDETERMINADO", "Indeterminado"),
            ("NAO_APLICAVEL", "Não se aplica"),
            ("RESOLVIDO", "Resolvido"),
            ("NAO_INFORMADO", "Não informado"));
    }
}
