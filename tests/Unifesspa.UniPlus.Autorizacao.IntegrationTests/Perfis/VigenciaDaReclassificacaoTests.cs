namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

using static Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis.CenarioDaReclassificacao;

/// <summary>Quando a reclassificação deixa de valer porque o catálogo mudou de novo depois dela.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class VigenciaDaReclassificacaoTests : PerfilEndpointTestBase, IAsyncLifetime
{
    private readonly CenarioDaReclassificacao _cenario;

    public VigenciaDaReclassificacaoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
        _cenario = new CenarioDaReclassificacao(Fixture);
    }

    public Task InitializeAsync() => _cenario.ConciliarAsync();

    public Task DisposeAsync() => _cenario.ConciliarAsync();

    [Fact(DisplayName = "A reclassificação da permissão que saiu do catálogo deixa de valer: sai da fila e não aceita decisão")]
    public async Task PermissaoRemovidaDoCatalogo_DeixaDeValer()
    {
        (Guid perfil, _) = await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await _cenario.ConciliarAsync();
        Guid id = await _cenario.ReclassificacaoDoPerfilAsync(perfil);

        await _cenario.GravarCatalogoSemAPermissaoAsync(PermissaoReclassificada, new string('b', 64));

        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IReclassificacaoRepository reclassificacoes = scope.ServiceProvider.GetRequiredService<IReclassificacaoRepository>();
        (await reclassificacoes.ListarVigentesQueSuspendemAsync()).Should().NotContain(r => r.Id == id);
        (await reclassificacoes.EstaVigenteAsync((await reclassificacoes.ObterAsync(id))!)).Should().BeFalse();
    }

    [Fact(DisplayName = "A reclassificação deixa de valer quando a permissão volta ao catálogo com outra classe")]
    public async Task PermissaoDeVoltaComOutraClasse_DeixaDeValer()
    {
        (Guid perfil, _) = await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await _cenario.ConciliarAsync();
        Guid id = await _cenario.ReclassificacaoDoPerfilAsync(perfil);

        await _cenario.GravarCatalogoAnteriorAsync(PermissaoReclassificada, ClassificacaoDeProtecao.Publico, deUnidade: false, new string('c', 64));

        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IReclassificacaoRepository reclassificacoes = scope.ServiceProvider.GetRequiredService<IReclassificacaoRepository>();
        (await reclassificacoes.ListarVigentesQueSuspendemAsync()).Should().NotContain(r => r.Id == id);
        (await reclassificacoes.EstaVigenteAsync((await reclassificacoes.ObterAsync(id))!)).Should().BeFalse();
    }

    [Fact(DisplayName = "A permissão que volta ao catálogo acima da base rebaixada da concessão suspende com reclassificação vigente")]
    public async Task PermissaoDeVoltaAcimaDaBaseRebaixada_TemReclassificacaoVigente()
    {
        Guid perfil = await SemearPerfilVigenteAsync(PermissaoReclassificada, OutraPermissaoDoPerfil);
        string pessoa = $"pessoa.{Guid.NewGuid():N}";
        await ConcessoesDeTeste.ConcederAsync(Fixture.Factory.Services, pessoa, perfil);
        await GravarBaseRebaixadaAsync(perfil, await _cenario.ConcessaoDoPerfilAsync(perfil), ClassificacaoDeProtecao.Publico);
        await _cenario.GravarCatalogoSemAPermissaoAsync(PermissaoReclassificada, new string('d', 64));

        await _cenario.ConciliarAsync();

        (await _cenario.PermissoesDaPessoaAsync(pessoa)).Should().NotContain(PermissaoReclassificada).And.Contain(OutraPermissaoDoPerfil);
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IReclassificacaoRepository reclassificacoes = scope.ServiceProvider.GetRequiredService<IReclassificacaoRepository>();
        (await reclassificacoes.ListarVigentesQueSuspendemAsync()).Should().ContainSingle(r => r.PerfilId == perfil && r.Codigo == PermissaoReclassificada);
    }

    private async Task GravarBaseRebaixadaAsync(Guid perfil, Guid concessao, ClassificacaoDeProtecao classificacao)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid implantacao = await db.ImplantacoesDasPermissoes.OrderByDescending(c => c.Sequencia).Select(c => c.Id).FirstAsync();
        db.BasesDasPermissoes.Add(BaseDaPermissaoNaConcessao.Registrar(
            perfil, concessao, PermissaoReclassificada, new BaseDaConcessao(classificacao, false), OrigemDaBase.Conciliacao, implantacao, 0));
        await db.SaveChangesAsync();
    }
}
