namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Net;

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

/// <summary>A conferência do catálogo contra o banco real: o que a conciliação grava, suspende e avisa.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class ReclassificacaoDoCatalogoTests : PerfilEndpointTestBase, IAsyncLifetime
{
    private readonly CenarioDaReclassificacao _cenario;

    public ReclassificacaoDoCatalogoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
        _cenario = new CenarioDaReclassificacao(Fixture);
    }

    public Task InitializeAsync() => _cenario.ConciliarAsync();

    public Task DisposeAsync() => _cenario.ConciliarAsync();

    [Fact(DisplayName = "O catálogo da aplicação fica gravado na subida e a conciliação repetida não grava de novo")]
    public async Task Conciliacao_EhIdempotente()
    {
        string impressao = Fixture.Factory.Services.GetRequiredService<CatalogoDaAplicacao>().Impressao;
        long antes = await ContarCatalogosAsync();

        await _cenario.ConciliarAsync();
        await _cenario.ConciliarAsync();

        (await ImpressaoAtualAsync()).Should().Be(impressao);
        (await ContarCatalogosAsync()).Should().Be(antes);
    }

    [Fact(DisplayName = "A subida de classe suspende só a permissão reclassificada e o resto do perfil continua")]
    public async Task Subida_SuspendeSoAPermissaoReclassificada()
    {
        (Guid perfil, string paulo) = await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);

        await _cenario.ConciliarAsync();

        IReadOnlyList<string> permissoes = await _cenario.PermissoesDaPessoaAsync(paulo);
        permissoes.Should().NotContain(PermissaoReclassificada).And.Contain(OutraPermissaoDoPerfil);
        (await ContarReclassificacoesAsync(perfil)).Should().Be(1);
    }

    [Fact(DisplayName = "O monitoramento conta a reclassificação que suspende a permissão enquanto a liberação não vem")]
    public async Task Monitoramento_ContaAReclassificacaoPendente()
    {
        long antes = (await LerMonitoramentoAsync()).ReclassificacoesPendentes;
        await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);

        await _cenario.ConciliarAsync();

        (await LerMonitoramentoAsync()).ReclassificacoesPendentes.Should().Be(antes + 1);
    }

    [Fact(DisplayName = "A descida de classe não suspende, não gera aviso e grava a base conciliada")]
    public async Task Descida_NaoSuspendeENaoAvisa()
    {
        (Guid perfil, string pessoa) = await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Sensivel, deUnidade: false);

        await _cenario.ConciliarAsync();

        (await _cenario.PermissoesDaPessoaAsync(pessoa)).Should().Contain(PermissaoReclassificada);
        (await ContarReclassificacoesAsync(perfil)).Should().Be(1);
        (await FatosSemAvisoDoPerfilAsync(perfil)).Should().Be(0);
        (await ContarBasesConciliadasAsync(perfil)).Should().Be(1);
    }

    [Fact(DisplayName = "A mudança de escopo suspende a permissão ainda que a classe não suba")]
    public async Task Escopo_SuspendePelaMudancaDeEscopo()
    {
        (Guid perfil, string pessoa) = await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Interno, deUnidade: true);

        await _cenario.ConciliarAsync();

        (await _cenario.PermissoesDaPessoaAsync(pessoa)).Should().NotContain(PermissaoReclassificada).And.Contain(OutraPermissaoDoPerfil);
        (await FatosSemAvisoDoPerfilAsync(perfil)).Should().Be(1);
    }

    [Fact(DisplayName = "A rotina avisa cada fato uma só vez e grava uma linha de aviso por fato")]
    public async Task Avisos_UmaLinhaPorFato()
    {
        (Guid perfil, _) = await _cenario.PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await _cenario.ConciliarAsync();

        await ProcessarAvisosAsync();
        await ProcessarAvisosAsync();

        (await FatosSemAvisoDoPerfilAsync(perfil)).Should().Be(0);
        (await ContarAvisosDoPerfilAsync(perfil)).Should().Be(1);
    }

    [Fact(DisplayName = "A instância cujo catálogo difere do banco falha fechada na leitura do acesso e responde 503")]
    public async Task InstanciaAntiga_RespondeServicoIndisponivel()
    {
        await _cenario.GravarCatalogoAnteriorAsync(PermissaoReclassificada, ClassificacaoDeProtecao.Publico, false, new string('b', 64));
        string pessoa = $"pessoa.{Guid.NewGuid():N}";
        await ConcessoesDeTeste.ConcederPermissoesAsync(Fixture.Factory.Services, pessoa, OutraPermissaoDoPerfil);

        Func<Task> leitura = () => _cenario.PermissoesDaPessoaAsync(pessoa);
        using HttpClient cliente = Cliente(pessoa);
        using HttpResponseMessage resposta = await cliente.GetAsync(new Uri(Rota, UriKind.Relative));

        await leitura.Should().ThrowAsync<CatalogoDesatualizadoException>();
        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private async Task<AmostraDaGovernanca> LerMonitoramentoAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ILeituraDoMonitoramento>().LerAsync(DateTimeOffset.UtcNow);
    }

    private async Task ProcessarAvisosAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProcessamentoDosAvisosDaReclassificacao>().ExecutarAsync(CancellationToken.None);
    }

    private async Task<long> ContarAsync(string sql, params object[] parametros)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await db.Database.SqlQueryRaw<long>(sql, parametros).SingleAsync();
    }

    private Task<long> ContarCatalogosAsync() =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.implantacoes_das_permissoes""");

    private Task<long> ContarReclassificacoesAsync(Guid perfil) =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.reclassificacoes_da_permissao WHERE perfil_id = {0}""", perfil);

    private Task<long> ContarBasesConciliadasAsync(Guid perfil) =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.bases_da_permissao_concedida WHERE perfil_id = {0} AND origem = 'conciliacao'""", perfil);

    private Task<long> FatosSemAvisoDoPerfilAsync(Guid perfil) =>
        ContarAsync(
            """
            SELECT count(*) AS "Value" FROM autorizacao.reclassificacoes_da_permissao r
            WHERE r.perfil_id = {0} AND (autorizacao.ordem_da_classificacao(r.classificacao_nova) > autorizacao.ordem_da_classificacao(r.classificacao_anterior) OR r.de_unidade_novo <> r.de_unidade_anterior)
              AND NOT EXISTS (SELECT 1 FROM autorizacao.avisos_da_reclassificacao a WHERE a.origem = 'reclassificacao' AND a.origem_id = r.id)
            """,
            perfil);

    private Task<long> ContarAvisosDoPerfilAsync(Guid perfil) =>
        ContarAsync(
            """
            SELECT count(*) AS "Value" FROM autorizacao.avisos_da_reclassificacao a
            JOIN autorizacao.reclassificacoes_da_permissao r ON a.origem = 'reclassificacao' AND a.origem_id = r.id
            WHERE r.perfil_id = {0}
            """,
            perfil);

    private async Task<string> ImpressaoAtualAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return (await db.ImplantacoesDasPermissoes.AsNoTracking().OrderByDescending(c => c.Sequencia).Select(c => c.Impressao).FirstAsync()).Trim();
    }
}
