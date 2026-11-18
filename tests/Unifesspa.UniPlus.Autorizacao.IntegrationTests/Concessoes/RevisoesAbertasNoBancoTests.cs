namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Concessoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// As revisões das concessões efetivamente abertas, sobre revisões e concessões reais no PostgreSQL: a revisão só vale enquanto o
/// período não terminou e há concessão vigente fora da nova versão. O predicado é um só, usado pela decisão de acesso, pelo
/// pedido em perfil em revisão e pelo monitoramento.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class RevisoesAbertasNoBancoTests : ConcessoesEmVigenciaTestBase
{
    private const string Permissao = "configuracao:cursos:manter";
    private const string OutraPermissao = "configuracao:campi:manter";

    public RevisoesAbertasNoBancoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A revisão com concessão vigente fora da nova versão e período ainda vigente está aberta, e todos os consumidores concordam")]
    public async Task RevisaoComConcessaoPorConfirmar_EstaAberta()
    {
        (Guid perfil, _) = await PerfilComRevisaoAsync();

        (await TemRevisaoAbertaAsync(perfil)).Should().BeTrue();
        (await TemRevisaoPorEncerrarAsync(perfil)).Should().BeFalse();
        (await RevisoesEfetivamenteAbertasDoPerfilAsync(perfil)).Should().Be(1);
    }

    [Fact(DisplayName = "A revisão cuja única concessão foi revogada deixa de estar aberta e precisa ser encerrada")]
    public async Task RevisaoSemConcessaoVigente_FaltaEncerrar()
    {
        (Guid perfil, Guid concessao) = await PerfilComRevisaoAsync();
        await RevogarConcessaoAsync(concessao);

        (await TemRevisaoAbertaAsync(perfil)).Should().BeFalse();
        (await TemRevisaoPorEncerrarAsync(perfil)).Should().BeTrue();
        (await RevisoesEfetivamenteAbertasDoPerfilAsync(perfil)).Should().Be(0);
    }

    [Fact(DisplayName = "A contagem das revisões abertas do monitoramento soma a revisão efetivamente aberta e não a sem concessão vigente")]
    public async Task Monitoramento_ContaSoAsEfetivamenteAbertas()
    {
        long antes = await RevisoesDoMonitoramentoAsync();
        (Guid perfil, Guid concessao) = await PerfilComRevisaoAsync();

        long comAberta = await RevisoesDoMonitoramentoAsync();
        await RevogarConcessaoAsync(concessao);
        long semConcessao = await RevisoesDoMonitoramentoAsync();

        (comAberta - antes).Should().Be(1, "a revisão do perfil novo está aberta");
        (semConcessao - antes).Should().Be(0, "sem concessão vigente a revisão não conta");
        (await RevisoesEfetivamenteAbertasDoPerfilAsync(perfil)).Should().Be(0);
    }

    // Um perfil institucional com a versão 1 em vigor e uma concessão nela, e uma revisão aberta agora para a versão 2.
    private async Task<(Guid Perfil, Guid Concessao)> PerfilComRevisaoAsync()
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(Fixture.Factory.Services, Permissao);
        Guid concessao = await ConcederAsync(perfil, Pessoa(Titular("revisão")), EscopoDaConcessao.DaInstituicao());
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        Perfil carregado = (await scope.ServiceProvider.GetRequiredService<IConcessaoRepository>().ObterParaConcederAsync(perfil))!;
        VersaoDoPerfil nova = VersaoAlvoDe(carregado, scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>());
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.Set<VersaoDoPerfil>().Add(nova);
        db.Set<RevisaoDasConcessoesDoPerfil>().Add(RevisaoDasConcessoesDoPerfil.Abrir(perfil, nova, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        return (perfil, concessao);
    }

    private static VersaoDoPerfil VersaoAlvoDe(Perfil perfil, ICatalogoDePermissoesConsultavel catalogo) =>
        PerfisDeTeste.Garantir(PerfisDeTeste.CriarVersao(
            perfil,
            PerfisDeTeste.Conteudo(perfil.Nome, perfil.Descricao, [Permissao, OutraPermissao], catalogo),
            PerfisDeTeste.SemContexto,
            PerfisDeTeste.AutoriaDoAutor,
            DateTimeOffset.UtcNow,
            catalogo));

    private async Task RevogarConcessaoAsync(Guid concessao)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Concessao encontrada = await db.Set<Concessao>().SingleAsync(c => c.Id == concessao);
        encontrada.Revogar(new AtoDoAgente(Diogo, DateTimeOffset.UtcNow), "Revogada para o teste das revisões.").IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private async Task<bool> TemRevisaoAbertaAsync(Guid perfil)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IRevisaoDasConcessoesRepository>().TemRevisaoAbertaAsync(perfil, DateTimeOffset.UtcNow);
    }

    private async Task<bool> TemRevisaoPorEncerrarAsync(Guid perfil)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IRevisaoDasConcessoesRepository>().TemRevisaoPorEncerrarAsync(perfil, DateTimeOffset.UtcNow);
    }

    private async Task<int> RevisoesEfetivamenteAbertasDoPerfilAsync(Guid perfil)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>()
            .EfetivamenteAbertas(DateTimeOffset.UtcNow).CountAsync(j => j.PerfilId == perfil);
    }

    private async Task<long> RevisoesDoMonitoramentoAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<ILeituraDoMonitoramento>().LerAsync(DateTimeOffset.UtcNow)).RevisoesAbertas;
    }
}
