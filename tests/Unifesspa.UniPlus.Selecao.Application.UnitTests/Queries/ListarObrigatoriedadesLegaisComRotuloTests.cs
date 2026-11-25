namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ListarObrigatoriedadesLegaisComRotuloTests
{
    [Fact]
    public async Task Handle_PaginaComTiposRepetidos_ConsultaCodigosDistintosSemReservado()
    {
        IObrigatoriedadeLegalRepository regras = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipos = Substitute.For<ITipoProcessoReader>();
        IReadOnlyList<ObrigatoriedadeLegal> pagina = [Regra("TIPO_A"), Regra("TIPO_A"), Regra(TodosOsTiposDeProcesso.Codigo)];
        regras.ListarPaginadoAsync(default, 10, PaginationDirection.Next, null, null, true, default)
            .Returns((pagina, null, null));
        tipos.ObterNomesPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), default)
            .Returns(new Dictionary<string, string> { ["TIPO_A"] = "Tipo A" });

        ListarObrigatoriedadesLegaisResult resultado = await ListarObrigatoriedadesLegaisQueryHandler.Handle(new(null, 10, PaginationDirection.Next, null, null, true), regras, tipos, default);

        resultado.Items.Select(item => item.TipoProcesso.Rotulo).Should().Equal("Tipo A", "Tipo A", TodosOsTiposDeProcesso.Rotulo);
        await tipos.Received(1).ObterNomesPorCodigosAsync(
            Arg.Is<IReadOnlyCollection<string>>(codigos => codigos.Count == 1 && codigos.Contains("TIPO_A")), default);
    }

    private static ObrigatoriedadeLegal Regra(string codigo) => ObrigatoriedadeLegal.Criar(
        codigo, CategoriaObrigatoriedade.Outros, "REGRA", new ConcorrenciaDuplaObrigatoria(),
        new TextoNormativo("Descrição", "Lei", null, null), new DateOnly(2026, 1, 1), null).Value!;
}
