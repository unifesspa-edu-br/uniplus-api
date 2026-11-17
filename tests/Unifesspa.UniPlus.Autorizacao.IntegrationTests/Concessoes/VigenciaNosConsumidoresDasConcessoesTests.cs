namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Concessoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// As consultas que só consideram a concessão vigente no instante, sobre concessões reais no PostgreSQL: a que vence
/// exatamente no instante, a revogada e a vencida ficam de fora, e a de unidade entra onde o escopo não importa.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class VigenciaNosConsumidoresDasConcessoesTests : ConcessoesEmVigenciaTestBase
{
    public VigenciaNosConsumidoresDasConcessoesTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Os códigos que o perfil dá vêm só das concessões vigentes dele")]
    public async Task CodigosDasConcessoesDoPerfil_SoDasVigentes()
    {
        (Guid perfil, Dictionary<string, Guid> ids) = await MontarAsync();
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        Guid[] concessoes = await db.CodigosDasConcessoesDoPerfil(perfil, Agora).Select(k => k.ConcessaoId).Distinct().ToArrayAsync();

        concessoes.Should().BeEquivalentTo([ids[SemPrazo], ids[PrazoFuturo]]);
    }

    [Fact(DisplayName = "Só o perfil com alguma concessão vigente é listado para a revisão")]
    public async Task PerfisComConcessaoAtiva_SoComVigente()
    {
        (Guid comVigente, _) = await MontarAsync();
        Guid soVencida = await CriarPerfilComumAsync();
        await ConcederComoAsync(soVencida, "so-vencida", EscopoDaConcessao.DaUnidade(Guid.NewGuid()), TimeSpan.FromMinutes(-1), revogada: false);
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        IReadOnlyList<Perfil> perfis = await scope.ServiceProvider.GetRequiredService<IRevisaoDeAcessosRepository>().ListarPerfisComConcessaoAtivaAsync(Agora);

        perfis.Select(p => p.Id).Should().Contain(comVigente).And.NotContain(soVencida);
    }

    [Fact(DisplayName = "As concessões ativas dos perfis da revisão são só as vigentes, em ordem de identificador")]
    public async Task ConcessoesAtivasDosPerfis_SoVigentesEmOrdem()
    {
        (Guid perfil, Dictionary<string, Guid> ids) = await MontarAsync();
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        IReadOnlyList<(Guid ConcessaoId, Guid PerfilId, Identidade Beneficiario)> ativas = await scope.ServiceProvider
            .GetRequiredService<IRevisaoDeAcessosRepository>().ListarConcessoesAtivasDosPerfisAsync([perfil], Agora);

        ativas.Select(a => a.ConcessaoId).Should().Equal(new[] { ids[SemPrazo], ids[PrazoFuturo] }.Order());
    }
}
