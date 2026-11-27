namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

public sealed class ListagemDeObrigatoriedadesLegaisTests(ObrigatoriedadeLegalDbFixture fixture)
    : IClassFixture<ObrigatoriedadeLegalDbFixture>
{
    [Fact]
    public async Task ListarPaginado_FiltroDeTipo_ComparaExatamente()
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext("admin");
        contexto.ObrigatoriedadesLegais.AddRange(Regra(TodosOsTiposDeProcesso.Codigo, "REGRA_TODOS"), Regra("SISU", "REGRA_SISU"));
        await contexto.SaveChangesAsync();
        ObrigatoriedadeLegalRepository repositorio = new(contexto);

        (await ListarAsync(repositorio, TodosOsTiposDeProcesso.Codigo)).Should().Equal("REGRA_TODOS");
        (await ListarAsync(repositorio, "SISU")).Should().Equal("REGRA_SISU");
        (await ListarAsync(repositorio, " SISU ")).Should().BeEmpty();
        (await ListarAsync(repositorio, "DESCONHECIDO")).Should().BeEmpty();
        (await ListarAsync(repositorio, "*")).Should().BeEmpty();
        (await ListarAsync(repositorio, "")).Should().HaveCount(2);
    }

    private static async Task<string[]> ListarAsync(ObrigatoriedadeLegalRepository repositorio, string codigo)
    {
        (IReadOnlyList<ObrigatoriedadeLegal> itens, _, _) = await repositorio.ListarPaginadoAsync(
            null, 20, PaginationDirection.Next, codigo, null, null);
        return [.. itens.Select(regra => regra.RegraCodigo)];
    }

    private static ObrigatoriedadeLegal Regra(string tipo, string codigo) => ObrigatoriedadeLegal.Criar(
        tipo, CategoriaObrigatoriedade.Outros, codigo, new ConcorrenciaDuplaObrigatoria(),
        new TextoNormativo("Descrição", "Lei", null, null), new DateOnly(2026, 1, 1), null).Value!;
}
