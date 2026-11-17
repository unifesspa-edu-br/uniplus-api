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
/// As janelas de migração efetivamente abertas, sobre janelas e concessões reais no PostgreSQL: a janela só vale enquanto o
/// prazo não passou e há concessão vigente fora da versão-alvo. O predicado é um só, usado pela decisão de acesso, pelo
/// pedido em perfil em migração e pelo monitoramento.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class JanelasAbertasNoBancoTests : ConcessoesEmVigenciaTestBase
{
    private const string Permissao = "configuracao:cursos:manter";
    private const string OutraPermissao = "configuracao:campi:manter";

    public JanelasAbertasNoBancoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A janela com concessão vigente fora da versão-alvo e prazo por vencer está aberta, e todos os consumidores concordam")]
    public async Task JanelaComConcessaoPorMigrar_EstaAberta()
    {
        (Guid perfil, _) = await PerfilComJanelaAsync();

        (await TemJanelaAbertaAsync(perfil)).Should().BeTrue();
        (await TemJanelaPorMaterializarAsync(perfil)).Should().BeFalse();
        (await JanelasEfetivamenteAbertasDoPerfilAsync(perfil)).Should().Be(1);
    }

    [Fact(DisplayName = "A janela cuja única concessão foi revogada deixa de estar aberta e passa a faltar materializar")]
    public async Task JanelaSemConcessaoVigente_FaltaMaterializar()
    {
        (Guid perfil, Guid concessao) = await PerfilComJanelaAsync();
        await RevogarConcessaoAsync(concessao);

        (await TemJanelaAbertaAsync(perfil)).Should().BeFalse();
        (await TemJanelaPorMaterializarAsync(perfil)).Should().BeTrue();
        (await JanelasEfetivamenteAbertasDoPerfilAsync(perfil)).Should().Be(0);
    }

    [Fact(DisplayName = "A contagem das janelas abertas do monitoramento soma a janela efetivamente aberta e não a sem concessão vigente")]
    public async Task Monitoramento_ContaSoAsEfetivamenteAbertas()
    {
        long antes = await JanelasDoMonitoramentoAsync();
        (Guid perfil, Guid concessao) = await PerfilComJanelaAsync();

        long comAberta = await JanelasDoMonitoramentoAsync();
        await RevogarConcessaoAsync(concessao);
        long semConcessao = await JanelasDoMonitoramentoAsync();

        (comAberta - antes).Should().Be(1, "a janela do perfil novo está aberta");
        (semConcessao - antes).Should().Be(0, "sem concessão vigente a janela não conta");
        (await JanelasEfetivamenteAbertasDoPerfilAsync(perfil)).Should().Be(0);
    }

    // Um perfil institucional com a versão 1 em vigor e uma concessão nela, e uma janela aberta agora para a versão 2.
    private async Task<(Guid Perfil, Guid Concessao)> PerfilComJanelaAsync()
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(Fixture.Factory.Services, Permissao);
        Guid concessao = await ConcederAsync(perfil, Pessoa(Titular("janela")), EscopoDaConcessao.DaInstituicao());
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        Perfil carregado = (await scope.ServiceProvider.GetRequiredService<IConcessaoRepository>().ObterParaConcederAsync(perfil))!;
        VersaoDoPerfil nova = VersaoAlvoDe(carregado, scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>());
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.Set<VersaoDoPerfil>().Add(nova);
        db.Set<JanelaDeMigracao>().Add(JanelaDeMigracao.Abrir(perfil, nova, DateTimeOffset.UtcNow));
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
        encontrada.Revogar(new AtoDoAgente(Diogo, DateTimeOffset.UtcNow), "Revogada para o teste das janelas.").IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private async Task<bool> TemJanelaAbertaAsync(Guid perfil)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IJanelaDeMigracaoRepository>().TemJanelaAbertaAsync(perfil, DateTimeOffset.UtcNow);
    }

    private async Task<bool> TemJanelaPorMaterializarAsync(Guid perfil)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IJanelaDeMigracaoRepository>().TemJanelaPorMaterializarAsync(perfil, DateTimeOffset.UtcNow);
    }

    private async Task<int> JanelasEfetivamenteAbertasDoPerfilAsync(Guid perfil)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>()
            .EfetivamenteAbertas(DateTimeOffset.UtcNow).CountAsync(j => j.PerfilId == perfil);
    }

    private async Task<long> JanelasDoMonitoramentoAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<ILeituraDoMonitoramento>().LerAsync(DateTimeOffset.UtcNow)).JanelasAbertas;
    }
}
