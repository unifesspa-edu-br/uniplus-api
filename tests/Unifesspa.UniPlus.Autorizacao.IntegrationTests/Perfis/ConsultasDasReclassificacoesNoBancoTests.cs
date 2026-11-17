namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis.CenarioDaReclassificacao;

/// <summary>
/// As consultas do repositório das reclassificações sobre o PostgreSQL: os fatos que ainda não foram avisados, com o
/// limite, e as concessões com a permissão reclassificada, com a base viva quando há e o que a versão da concessão guarda quando não.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class ConsultasDasReclassificacoesNoBancoTests : PerfilEndpointTestBase, IAsyncLifetime
{
    private readonly CenarioDaReclassificacao _cenario;

    public ConsultasDasReclassificacoesNoBancoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
        _cenario = new CenarioDaReclassificacao(Fixture);
    }

    public Task InitializeAsync() => _cenario.ConciliarAsync();

    public Task DisposeAsync() => _cenario.ConciliarAsync();

    [Fact(DisplayName = "A reclassificação que sobe a classe é fato sem aviso até ganhar a linha de aviso")]
    public async Task FatosSemAviso_TrazAReclassificacaoAteSerAvisada()
    {
        (Guid perfil, _) = await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await _cenario.ConciliarAsync();
        Guid id = await _cenario.ReclassificacaoDoPerfilAsync(perfil);
        FatoSemAviso fato = new(OrigemDoAviso.Reclassificacao, id);

        IReadOnlyList<FatoSemAviso> antes = await FatosSemAvisoAsync(1000);
        await AvisarAsync(id);
        IReadOnlyList<FatoSemAviso> depois = await FatosSemAvisoAsync(1000);

        antes.Should().Contain(fato);
        depois.Should().NotContain(fato);
    }

    [Fact(DisplayName = "Os fatos sem aviso respeitam o limite pedido: um fato com limite 1 e nenhum com limite 0")]
    public async Task FatosSemAviso_RespeitamOLimite()
    {
        (Guid perfil, _) = await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await _cenario.ConciliarAsync();
        await _cenario.ReclassificacaoDoPerfilAsync(perfil);

        (await FatosSemAvisoAsync(1)).Should().ContainSingle();
        (await FatosSemAvisoAsync(0)).Should().BeEmpty();
    }

    [Fact(DisplayName = "As reclassificações vêm antes das divergências, e cada grupo da mais antiga para a mais nova")]
    public async Task FatosSemAviso_ReclassificacoesAntesDasDivergencias()
    {
        (Guid perfil, _) = await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await _cenario.ConciliarAsync();
        Guid reclassificacao = await _cenario.ReclassificacaoDoPerfilAsync(perfil);
        Guid primeiraDivergencia = await GravarDivergenciaAsync(perfil, "teste:ordem:primeira", 1);
        Guid segundaDivergencia = await GravarDivergenciaAsync(perfil, "teste:ordem:segunda", 2);

        IReadOnlyList<FatoSemAviso> fatos = await FatosSemAvisoAsync(1000);

        fatos.Select(f => f.Id).Should().ContainInOrder(reclassificacao, primeiraDivergencia, segundaDivergencia);
    }

    [Fact(DisplayName = "A concessão com a permissão traz a base da versão e, havendo base viva, a base viva")]
    public async Task ConcessoesComAPermissao_BaseDaVersaoEDepoisBaseViva()
    {
        (Guid perfil, _) = await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await _cenario.ConciliarAsync();
        Guid concessao = await _cenario.ConcessaoDoPerfilAsync(perfil);

        IReadOnlyList<ConcessaoComAPermissao> daVersao = await ConcessoesComAPermissaoAsync(perfil);
        await GravarBaseVivaAsync(perfil, concessao, ClassificacaoDeProtecao.Interno);
        IReadOnlyList<ConcessaoComAPermissao> viva = await ConcessoesComAPermissaoAsync(perfil);

        daVersao.Should().ContainSingle(c => c.Concessao.Id == concessao).Which.Base.Classificacao.Should().Be(ClassificacaoDeProtecao.Publico);
        viva.Should().ContainSingle(c => c.Concessao.Id == concessao).Which.Base.Classificacao.Should().Be(ClassificacaoDeProtecao.Interno);
    }

    private async Task<IReadOnlyList<FatoSemAviso>> FatosSemAvisoAsync(int limite)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IReclassificacaoRepository>().ListarFatosSemAvisoAsync(limite);
    }

    private async Task<IReadOnlyList<ConcessaoComAPermissao>> ConcessoesComAPermissaoAsync(Guid perfil)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IReclassificacaoRepository>()
            .ListarConcessoesComAPermissaoAsync(perfil, CodigoDePermissao.De(PermissaoReclassificada), DateTimeOffset.UtcNow, false);
    }

    private async Task AvisarAsync(Guid reclassificacaoId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IReclassificacaoRepository>()
            .Adicionar(AvisoDaReclassificacao.Registrar(OrigemDoAviso.Reclassificacao, reclassificacaoId, DateTimeOffset.UtcNow));
        await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().SaveChangesAsync();
    }

    private async Task GravarBaseVivaAsync(Guid perfil, Guid concessao, ClassificacaoDeProtecao classificacao)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid implantacao = await db.ImplantacoesDasPermissoes.OrderByDescending(c => c.Sequencia).Select(c => c.Id).FirstAsync();
        db.BasesDasPermissoes.Add(BaseDaPermissaoNaConcessao.Registrar(
            perfil, concessao, CodigoDePermissao.De(PermissaoReclassificada), new BaseDaConcessao(classificacao, false), OrigemDaBase.Conciliacao, implantacao, 0));
        await db.SaveChangesAsync();
    }


    private async Task<Guid> GravarDivergenciaAsync(Guid perfil, string codigo, int ordinal)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid implantacao = await db.ImplantacoesDasPermissoes.OrderByDescending(c => c.Sequencia).Select(c => c.Id).FirstAsync();
        DivergenciaDaGovernanca divergencia = DivergenciaDaGovernanca.DaComposicao(implantacao, perfil, codigo, TipoDeDivergencia.ComposicaoFaltante, DateTimeOffset.UtcNow, ordinal);
        db.Set<DivergenciaDaGovernanca>().Add(divergencia);
        await db.SaveChangesAsync();

        return divergencia.Id;
    }
}
