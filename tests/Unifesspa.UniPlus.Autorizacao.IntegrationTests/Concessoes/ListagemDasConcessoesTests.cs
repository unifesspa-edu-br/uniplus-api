namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Concessoes;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// A listagem das concessões do repositório sobre concessões reais no PostgreSQL: cada critério do filtro, as situações e
/// o cursor nas duas direções. O perfil institucional isola o cenário; a unidade, o perfil de unidade.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class ListagemDasConcessoesTests : ConcessoesEmVigenciaTestBase
{
    public ListagemDasConcessoesTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "A situação recorta a listagem; a ausência não recorta")]
    [InlineData(SituacaoDaConcessao.Ativa, new[] { SemPrazo, PrazoFuturo })]
    [InlineData(SituacaoDaConcessao.Revogada, new[] { Revogada })]
    [InlineData(SituacaoDaConcessao.Vencida, new[] { Vencida, NoLimite })]
    [InlineData(null, new[] { SemPrazo, PrazoFuturo, Revogada, Vencida, NoLimite })]
    public async Task Listar_PorSituacao_Recorta(SituacaoDaConcessao? situacao, string[] esperados)
    {
        (Guid perfil, Dictionary<string, Guid> ids) = await MontarAsync();

        Guid[] listados = await ListarIdsAsync(Filtro(situacao) with { PerfilId = perfil });

        listados.Should().BeEquivalentTo(esperados.Select(e => ids[e]));
    }

    [Fact]
    public async Task Listar_SituacaoDesconhecida_LancaExcecao()
    {
        FiltroDeConcessoes filtro = Filtro((SituacaoDaConcessao)99);

        Func<Task> listar = async () => await ListarIdsAsync(filtro);

        await listar.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "A unidade recorta a listagem para as concessões dela")]
    public async Task Listar_PorUnidade_SoDaUnidade()
    {
        (_, Dictionary<string, Guid> ids) = await MontarAsync();

        Guid[] listados = await ListarIdsAsync(Filtro(null) with { UnidadeId = Unidade });

        listados.Should().Equal(ids[NaUnidade]);
    }

    [Fact(DisplayName = "A pessoa com emissor e conta informados recorta a listagem para as concessões dela")]
    public async Task Listar_PorPessoa_SoDaPessoa()
    {
        (Guid perfil, Dictionary<string, Guid> ids) = await MontarAsync();
        Identidade pessoa = Pessoa(Titular(PrazoFuturo));

        Guid[] listados = await ListarIdsAsync(Filtro(null, pessoa.Emissor, pessoa.ContaId) with { PerfilId = perfil });

        listados.Should().Equal(ids[PrazoFuturo]);
    }

    [Theory(DisplayName = "Só o emissor, ou só a conta, não recorta a pessoa; os dois vazios, que contam como informados, recortam")]
    [InlineData("emissor", null, 5)]
    [InlineData(null, "conta", 5)]
    [InlineData(null, null, 5)]
    [InlineData("", "", 0)]
    public async Task Listar_ParDaPessoa_SoRecortaComOsDoisInformados(string? emissor, string? contaId, int esperado)
    {
        (Guid perfil, _) = await MontarAsync();

        Guid[] listados = await ListarIdsAsync(Filtro(null, emissor, contaId) with { PerfilId = perfil });

        listados.Should().HaveCount(esperado);
    }

    [Fact(DisplayName = "O cursor percorre a listagem inteira para a frente, em ordem de identificador, sem página antes da primeira nem depois da última")]
    public async Task Listar_ComCursor_PercorreParaAFrente()
    {
        (Guid perfil, Dictionary<string, Guid> ids) = await MontarAsync();

        List<Pagina> paginas = await PercorrerAsync(Filtro(null) with { PerfilId = perfil });

        paginas.Select(p => p.Ids.Length).Should().Equal(2, 2, 1);
        paginas.SelectMany(p => p.Ids).Should().Equal(ids.Where(c => c.Key != NaUnidade).Select(c => c.Value).Order());
        (paginas[0].Anterior, paginas[^1].Proximo).Should().Be((null, null));
    }

    [Fact(DisplayName = "O cursor volta da última página até a primeira, com as mesmas páginas da ida")]
    public async Task Listar_ComCursor_VoltaAteAPrimeira()
    {
        (Guid perfil, _) = await MontarAsync();
        FiltroDeConcessoes filtro = Filtro(null) with { PerfilId = perfil };
        List<Pagina> ida = await PercorrerAsync(filtro);

        Pagina penultima = await PaginaAsync(filtro, ida[^1].Anterior, PaginationDirection.Prev);
        Pagina inicio = await PaginaAsync(filtro, penultima.Anterior, PaginationDirection.Prev);

        penultima.Ids.Should().Equal(ida[1].Ids);
        inicio.Ids.Should().Equal(ida[0].Ids);
    }

    private FiltroDeConcessoes Filtro(SituacaoDaConcessao? situacao, string? emissor = null, string? contaId = null) =>
        new(emissor, contaId, null, null, situacao, Agora);

    private async Task<Guid[]> ListarIdsAsync(FiltroDeConcessoes filtro)
    {
        (IReadOnlyList<ConcessaoComPerfil> itens, _, _) = await ListarAsync(filtro, null, 50, PaginationDirection.Next);

        return [.. itens.Select(i => i.Id)];
    }

    private async Task<(IReadOnlyList<ConcessaoComPerfil> Itens, Guid? Anterior, Guid? Proximo)> ListarAsync(
        FiltroDeConcessoes filtro, Guid? depoisDe, int limite, PaginationDirection direcao)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IConcessaoRepository>().ListarPaginadoAsync(filtro, depoisDe, limite, direcao);
    }

    private async Task<List<Pagina>> PercorrerAsync(FiltroDeConcessoes filtro)
    {
        List<Pagina> paginas = [await PaginaAsync(filtro, null, PaginationDirection.Next)];
        while (paginas[^1].Proximo is { } proximo)
        {
            paginas.Add(await PaginaAsync(filtro, proximo, PaginationDirection.Next));
        }

        return paginas;
    }

    private async Task<Pagina> PaginaAsync(FiltroDeConcessoes filtro, Guid? depoisDe, PaginationDirection direcao)
    {
        (IReadOnlyList<ConcessaoComPerfil> itens, Guid? anterior, Guid? proximo) = await ListarAsync(filtro, depoisDe, 2, direcao);

        return new Pagina([.. itens.Select(i => i.Id)], anterior, proximo);
    }

    private sealed record Pagina(Guid[] Ids, Guid? Anterior, Guid? Proximo);
}
