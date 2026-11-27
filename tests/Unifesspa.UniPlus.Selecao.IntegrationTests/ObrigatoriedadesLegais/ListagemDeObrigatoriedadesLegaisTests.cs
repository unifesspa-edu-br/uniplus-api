namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

public sealed class ListagemDeObrigatoriedadesLegaisTests(ObrigatoriedadeLegalDbFixture fixture)
    : IClassFixture<ObrigatoriedadeLegalDbFixture>, IAsyncLifetime
{
    private static readonly string[] TiposSemeados = [TodosOsTiposDeProcesso.Codigo, "SISU", "TIPO_CURSO", "TIPO_A"];

    public Task InitializeAsync() => Task.CompletedTask;

    // Cada teste apaga as regras que semeou, para a listagem completa de um não contar as de outro.
    public async Task DisposeAsync()
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext("admin");
        await LimpezaDasObrigatoriedadesDoTeste.ApagarDosTiposAsync(contexto, TiposSemeados);
    }

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

    [Fact(DisplayName = "O cursor de uma consulta só continua com o mesmo filtro: trocar o tipo, a categoria ou a vigência recusa, e o tipo vazio é o mesmo que ausente")]
    public async Task ListarPaginado_CursorDeOutroFiltro_EhRecusado()
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext("admin");
        contexto.ObrigatoriedadesLegais.AddRange(Regra("TIPO_CURSO", "REGRA_UM"), Regra("TIPO_CURSO", "REGRA_DOIS"), Regra("TIPO_CURSO", "REGRA_TRES"));
        await contexto.SaveChangesAsync();
        ObrigatoriedadeLegalRepository repositorio = new(contexto);
        FiltroDasObrigatoriedadesLegais filtro = new("TIPO_CURSO", CategoriaObrigatoriedade.Outros, VigentesEm: null);
        (_, _, (string SortKey, Guid Id)? proximo) = await repositorio.ListarPaginadoAsync(null, null, 1, PaginationDirection.Next, filtro);
        proximo.Should().NotBeNull("três regras com limite 1 emitem a página seguinte");

        Task<object> Continuar(FiltroDasObrigatoriedadesLegais outro) => ContinuarAsync(repositorio, proximo!.Value, outro);

        await FluentActions.Awaiting(() => Continuar(filtro with { TipoProcessoCodigo = "OUTRO_TIPO" })).Should().ThrowAsync<CursorAnchorMismatchException>();
        await FluentActions.Awaiting(() => Continuar(filtro with { Categoria = null })).Should().ThrowAsync<CursorAnchorMismatchException>();
        await FluentActions.Awaiting(() => Continuar(filtro with { VigentesEm = new DateOnly(2026, 6, 15) })).Should().ThrowAsync<CursorAnchorMismatchException>();
        await FluentActions.Awaiting(() => Continuar(filtro)).Should().NotThrowAsync();
    }

    [Fact(DisplayName = "O tipo de processo vazio e o ausente dão o mesmo cursor")]
    public async Task ListarPaginado_TipoVazioEAusente_SaoOMesmoRecorte()
    {
        await using UniPlusDbContext contexto = fixture.CreateDbContext("admin");
        contexto.ObrigatoriedadesLegais.AddRange(Regra("TIPO_A", "REGRA_A"), Regra("TIPO_A", "REGRA_B"));
        await contexto.SaveChangesAsync();
        ObrigatoriedadeLegalRepository repositorio = new(contexto);
        (_, _, (string SortKey, Guid Id)? proximo) = await repositorio.ListarPaginadoAsync(
            null, null, 1, PaginationDirection.Next, new FiltroDasObrigatoriedadesLegais(null, null, null));

        Func<Task<object>> continuar = () => ContinuarAsync(repositorio, proximo!.Value, new FiltroDasObrigatoriedadesLegais(string.Empty, null, null));

        await continuar.Should().NotThrowAsync();
    }

    private static async Task<object> ContinuarAsync(
        ObrigatoriedadeLegalRepository repositorio, (string SortKey, Guid Id) ponto, FiltroDasObrigatoriedadesLegais filtro) =>
        await repositorio.ListarPaginadoAsync(ponto.SortKey, ponto.Id, 1, PaginationDirection.Next, filtro);

    private static async Task<string[]> ListarAsync(ObrigatoriedadeLegalRepository repositorio, string codigo)
    {
        (IReadOnlyList<ObrigatoriedadeLegal> itens, _, _) = await repositorio.ListarPaginadoAsync(
            null, null, 20, PaginationDirection.Next, new FiltroDasObrigatoriedadesLegais(codigo, null, null));
        return [.. itens.Select(regra => regra.RegraCodigo)];
    }

    private static ObrigatoriedadeLegal Regra(string tipo, string codigo) => ObrigatoriedadeLegal.Criar(
        tipo, CategoriaObrigatoriedade.Outros, codigo, new ConcorrenciaDuplaObrigatoria(),
        new TextoNormativo("Descrição", "Lei", null, null), new DateOnly(2026, 1, 1), null).Value!;
}
