namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A instância cujo catálogo não é o do banco, ou o banco sem catálogo, contra o PostgreSQL real: a leitura do acesso
/// falha fechada com ou sem concessões, a rota anônima responde, a conciliação ainda não confirmada não é vista pela
/// metade e a primeira conferência sobre a carga não grava reclassificação.
/// </summary>
[Collection(InstanciaAntigaCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class InstanciaAntigaTests : PerfilEndpointTestBase, IAsyncLifetime
{
    public InstanciaAntigaTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    public async Task InitializeAsync()
    {
        await ExecutarAsync("TRUNCATE autorizacao.implantacoes_das_permissoes CASCADE");
        await ConciliarAsync();
        await ExecutarAsync("TRUNCATE autorizacao.implantacoes_das_permissoes CASCADE");
    }

    public Task DisposeAsync() => ConciliarAsync();

    [Theory(DisplayName = "Sem catálogo gravado a leitura do acesso falha fechada, com ou sem concessões")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SemCatalogo_RecusaALeitura(bool comConcessao)
    {
        string pessoa = $"leandro.viana.{Guid.NewGuid():N}";
        if (comConcessao)
        {
            await ConcessoesDeTeste.ConcederPermissoesAsync(Fixture.Factory.Services, pessoa, ConsultarCatalogo);
        }

        await FluentActions.Awaiting(() => PermissoesAsync(pessoa)).Should().ThrowAsync<CatalogoDesatualizadoException>();
    }

    [Fact(DisplayName = "Com o catálogo gravado e a pessoa sem concessões, a leitura devolve a lista vazia")]
    public async Task CatalogoPresente_SemConcessoes_ListaVazia()
    {
        await ConciliarAsync();

        (await PermissoesAsync($"betina.neves.{Guid.NewGuid():N}")).Should().BeEmpty();
    }

    [Fact(DisplayName = "A rota anônima não lê concessões e responde mesmo com o banco sem catálogo")]
    public async Task RotaAnonima_RespondeSemCatalogo()
    {
        using HttpClient anonimo = Fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await anonimo.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "A conciliação ainda não confirmada não é vista pela leitura, que recusa; confirmada, a leitura segue")]
    public async Task ConciliacaoNaoConfirmada_NaoEhVistaPelaMetade()
    {
        string pessoa = $"diogo.souza.{Guid.NewGuid():N}";
        await ConcessoesDeTeste.ConcederPermissoesAsync(Fixture.Factory.Services, pessoa, ConsultarCatalogo);
        string impressao = Fixture.Factory.Services.GetRequiredService<CatalogoDaAplicacao>().Impressao;
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction aberta = await escopo.ServiceProvider
            .GetRequiredService<UniPlusDbContext>().Database.BeginTransactionAsync();
        await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(
            "INSERT INTO autorizacao.implantacoes_das_permissoes (id, impressao, implantada_em) VALUES ({0}, {1}, now())", Guid.CreateVersion7(), impressao);

        await FluentActions.Awaiting(() => PermissoesAsync(pessoa)).Should().ThrowAsync<CatalogoDesatualizadoException>();
        await aberta.CommitAsync();

        (await PermissoesAsync(pessoa)).Should().Contain(ConsultarCatalogo);
    }

    [Fact(DisplayName = "A primeira conferência sobre a carga e os retratos atuais não grava nenhuma reclassificação")]
    public async Task PrimeiraConferencia_GravaZeroReclassificacoes()
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(Fixture.Factory.Services, $"leandro.viana.{Guid.NewGuid():N}", ConsultarCatalogo);

        await ConciliarAsync();

        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        (await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Reclassificacoes.CountAsync()).Should().Be(0);
    }

    private async Task ConciliarAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<ConciliacaoDoCatalogo>().ExecutarAsync(CancellationToken.None);
    }

    private async Task<IReadOnlyList<string>> PermissoesAsync(string sub)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        UsuarioRef pessoa = UsuarioRef.From(TestAuthHandler.DefaultIssuer, sub).Value!;
        IReadOnlyList<ConcessaoAtiva> ativas = await escopo.ServiceProvider
            .GetRequiredService<IConcessoesAtivasDoSujeito>().ListarAsync(pessoa, CancellationToken.None);

        return [.. ativas.SelectMany(static c => c.Permissoes)];
    }
}
