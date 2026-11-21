namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class ListarNaturezasConceitoQueryHandlerTests
{
    [Fact(DisplayName = "O vocabulário devolve código e rótulo das naturezas na ordem declarada no enum")]
    public void Handle_DevolveAsNaturezasNaOrdemDoEnumComRotulo()
    {
        IReadOnlyList<CodigoComRotulo<NaturezaConceito>> naturezas =
            ListarNaturezasConceitoQueryHandler.Handle(new ListarNaturezasConceitoQuery());

        naturezas.Select(static n => (n.Codigo, n.Rotulo)).Should().Equal(
            ("NUMERICO", "Numérico"),
            ("CATEGORICO", "Categórico"));
    }
}
