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
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A conferência do catálogo contra o banco real: o catálogo anterior é simulado por uma linha nova de
/// <c>implantacoes_das_permissoes</c> com a classe ou o escopo de uma permissão alterados, e a conciliação compara o catálogo da
/// aplicação com ela.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ReclassificacaoDoCatalogoTests : PerfilEndpointTestBase, IAsyncLifetime
{
    private const string Codigo = "selecao:leiautes-inep:consultar";
    private const string Outra = ConsultarCatalogo;

    public ReclassificacaoDoCatalogoTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    public Task InitializeAsync() => ConciliarAsync();

    public Task DisposeAsync() => ConciliarAsync();

    [Fact(DisplayName = "O catálogo da aplicação fica gravado na subida e a conciliação repetida não grava de novo")]
    public async Task Conciliacao_EhIdempotente()
    {
        string impressao = Fixture.Factory.Services.GetRequiredService<CatalogoDaAplicacao>().Impressao;
        long antes = await ContarCatalogosAsync();

        await ConciliarAsync();
        await ConciliarAsync();

        (await ImpressaoAtualAsync()).Should().Be(impressao);
        (await ContarCatalogosAsync()).Should().Be(antes);
    }

    [Fact(DisplayName = "A subida de classe suspende só a permissão reclassificada e o resto do perfil continua")]
    public async Task Subida_SuspendeSoAPermissaoReclassificada()
    {
        (Guid perfil, string paulo) = await PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);

        await ConciliarAsync();

        IReadOnlyList<string> permissoes = await PermissoesDaPessoaAsync(paulo);
        permissoes.Should().NotContain(Codigo).And.Contain(Outra);
        (await ContarReclassificacoesAsync(perfil)).Should().Be(1);
    }

    [Fact(DisplayName = "A descida de classe não suspende, não gera aviso e grava a base conciliada")]
    public async Task Descida_NaoSuspendeENaoAvisa()
    {
        (Guid perfil, string pessoa) = await PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Sensivel, deUnidade: false);

        await ConciliarAsync();

        (await PermissoesDaPessoaAsync(pessoa)).Should().Contain(Codigo);
        (await ContarReclassificacoesAsync(perfil)).Should().Be(1);
        (await FatosSemAvisoDoPerfilAsync(perfil)).Should().Be(0);
        (await ContarBasesConciliadasAsync(perfil)).Should().Be(1);
    }

    [Fact(DisplayName = "A mudança de escopo suspende a permissão ainda que a classe não suba")]
    public async Task Escopo_SuspendePelaMudancaDeEscopo()
    {
        (Guid perfil, string pessoa) = await PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Interno, deUnidade: true);

        await ConciliarAsync();

        (await PermissoesDaPessoaAsync(pessoa)).Should().NotContain(Codigo).And.Contain(Outra);
        (await FatosSemAvisoDoPerfilAsync(perfil)).Should().Be(1);
    }

    [Fact(DisplayName = "A rotina avisa cada fato uma só vez e grava uma linha de aviso por fato")]
    public async Task Avisos_UmaLinhaPorFato()
    {
        (Guid perfil, _) = await PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await ConciliarAsync();

        await ProcessarAvisosAsync();
        await ProcessarAvisosAsync();

        (await FatosSemAvisoDoPerfilAsync(perfil)).Should().Be(0);
        (await ContarAvisosDoPerfilAsync(perfil)).Should().Be(1);
    }

    [Fact(DisplayName = "A instância cujo catálogo difere do banco falha fechada na leitura do acesso e responde 503")]
    public async Task InstanciaAntiga_RespondeServicoIndisponivel()
    {
        await GravarCatalogoAnteriorAsync(Codigo, ClassificacaoDeProtecao.Publico, false, new string('b', 64));
        string pessoa = $"pessoa.{Guid.NewGuid():N}";
        await ConcessoesDeTeste.ConcederPermissoesAsync(Fixture.Factory.Services, pessoa, Outra);

        Func<Task> leitura = () => PermissoesDaPessoaAsync(pessoa);
        using HttpClient cliente = Cliente(pessoa);
        using HttpResponseMessage resposta = await cliente.GetAsync(new Uri(Rota, UriKind.Relative));

        await leitura.Should().ThrowAsync<CatalogoDesatualizadoException>();
        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact(DisplayName = "A proteção de dados libera a subida de classe, a permissão volta à pessoa e a concessão liberada sai da reclassificação")]
    public async Task Liberacao_DevolveAPermissaoERecusaARepeticao()
    {
        (Guid perfil, string pessoa) = await PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await ConciliarAsync();
        Guid reclassificacao = await ReclassificacaoDoPerfilAsync(perfil);
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Diogo, TipoDeGovernanca.ProtecaoDeDados);
        await LerODocumentoDaVersaoAsync(perfil, Diogo);
        using HttpClient dpo = Cliente(Diogo);
        object corpo = new { etapa = "protecao_de_dados", resultado = "liberada", todas = true };

        using HttpResponseMessage primeira = await EnviarAsync(dpo, HttpMethod.Post, $"/api/autorizacao/reclassificacoes/{reclassificacao}/decisoes", corpo);
        Guid concessao = await ConcessaoDoPerfilAsync(perfil);
        using HttpResponseMessage segunda = await EnviarAsync(
            dpo, HttpMethod.Post, $"/api/autorizacao/reclassificacoes/{reclassificacao}/decisoes",
            new { etapa = "protecao_de_dados", resultado = "liberada", concessoes = new[] { concessao }, todas = false });

        primeira.StatusCode.Should().Be(HttpStatusCode.OK, await primeira.Content.ReadAsStringAsync());
        (await PermissoesDaPessoaAsync(pessoa)).Should().Contain(Codigo);
        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain("concessao_fora_da_reclassificacao");
    }

    [Fact(DisplayName = "A fila da proteção de dados lista a reclassificação, o detalhe traz a concessão suspensa e quem não tem a permissão da etapa recebe 403")]
    public async Task Fila_EDetalhe_ListamAReclassificacao()
    {
        (Guid perfil, _) = await PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await ConciliarAsync();
        Guid reclassificacao = await ReclassificacaoDoPerfilAsync(perfil);
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Diogo, TipoDeGovernanca.ProtecaoDeDados);
        using HttpClient dpo = Cliente(Diogo);
        using HttpClient semPermissao = Cliente($"pessoa.{Guid.NewGuid():N}");

        using HttpResponseMessage fila = await dpo.GetAsync(new Uri("/api/autorizacao/reclassificacoes?etapa=protecao_de_dados", UriKind.Relative));
        using HttpResponseMessage detalhe = await dpo.GetAsync(new Uri($"/api/autorizacao/reclassificacoes/{reclassificacao}", UriKind.Relative));
        using HttpResponseMessage negada = await semPermissao.GetAsync(new Uri("/api/autorizacao/reclassificacoes?etapa=protecao_de_dados", UriKind.Relative));
        using HttpResponseMessage invalida = await dpo.GetAsync(new Uri("/api/autorizacao/reclassificacoes?etapa=outra", UriKind.Relative));
        using HttpResponseMessage inexistente = await dpo.GetAsync(new Uri($"/api/autorizacao/reclassificacoes/{Guid.NewGuid()}", UriKind.Relative));

        fila.StatusCode.Should().Be(HttpStatusCode.OK);
        (await fila.Content.ReadAsStringAsync()).Should().Contain(reclassificacao.ToString());
        detalhe.StatusCode.Should().Be(HttpStatusCode.OK, await detalhe.Content.ReadAsStringAsync());
        (await detalhe.Content.ReadAsStringAsync()).Should().Contain("protecao_de_dados");
        negada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        invalida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "A divergência da governança aparece na tela de quem a vê e é negada a quem não tem permissão")]
    public async Task Divergencias_SoParaQuemTemPermissao()
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Diogo, TipoDeGovernanca.ProtecaoDeDados);
        using HttpClient dpo = Cliente(Diogo);
        using HttpClient semPermissao = Cliente($"pessoa.{Guid.NewGuid():N}");

        using HttpResponseMessage permitida = await dpo.GetAsync(new Uri("/api/autorizacao/governanca/divergencias", UriKind.Relative));
        using HttpResponseMessage negada = await semPermissao.GetAsync(new Uri("/api/autorizacao/governanca/divergencias", UriKind.Relative));

        permitida.StatusCode.Should().Be(HttpStatusCode.OK);
        negada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "A decisão com resultado, etapa ou alvo inválidos é recusada antes de qualquer gravação")]
    public async Task Decisao_ComPedidoInvalido_ERecusada()
    {
        (Guid perfil, _) = await PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await ConciliarAsync();
        Guid reclassificacao = await ReclassificacaoDoPerfilAsync(perfil);
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Diogo, TipoDeGovernanca.ProtecaoDeDados);
        using HttpClient dpo = Cliente(Diogo);
        string rota = $"/api/autorizacao/reclassificacoes/{reclassificacao}/decisoes";

        using HttpResponseMessage etapa = await EnviarAsync(dpo, HttpMethod.Post, rota, new { etapa = "x", resultado = "liberada", todas = true });
        using HttpResponseMessage resultado = await EnviarAsync(dpo, HttpMethod.Post, rota, new { etapa = "protecao_de_dados", resultado = "x", todas = true });
        using HttpResponseMessage alvo = await EnviarAsync(dpo, HttpMethod.Post, rota, new { etapa = "protecao_de_dados", resultado = "liberada", todas = false });
        using HttpResponseMessage mantidaSemMotivo = await EnviarAsync(dpo, HttpMethod.Post, rota, new { etapa = "protecao_de_dados", resultado = "mantida", todas = true });
        using HttpResponseMessage inexistente = await EnviarAsync(
            dpo, HttpMethod.Post, $"/api/autorizacao/reclassificacoes/{Guid.NewGuid()}/decisoes", new { etapa = "protecao_de_dados", resultado = "liberada", todas = true });

        new[] { etapa, resultado, alvo, mantidaSemMotivo }.Select(static r => r.StatusCode).Should().OnlyContain(static c => c == HttpStatusCode.UnprocessableEntity);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "A reclassificação da permissão que saiu do catálogo deixa de valer: sai da fila e não aceita decisão")]
    public async Task PermissaoRemovidaDoCatalogo_DeixaDeValer()
    {
        (Guid perfil, _) = await PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao.Publico, deUnidade: false);
        await ConciliarAsync();
        Guid id = await ReclassificacaoDoPerfilAsync(perfil);

        await GravarCatalogoSemAPermissaoAsync(Codigo, new string('b', 64));

        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IReclassificacaoRepository reclassificacoes = scope.ServiceProvider.GetRequiredService<IReclassificacaoRepository>();
        (await reclassificacoes.ListarVigentesQueSuspendemAsync()).Should().NotContain(r => r.Id == id);
        (await reclassificacoes.EstaVigenteAsync((await reclassificacoes.ObterAsync(id))!)).Should().BeFalse();
    }

    private async Task<Guid> ConcessaoDoPerfilAsync(Guid perfil)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Concessoes
            .Where(c => c.PerfilId == perfil).Select(c => c.Id).SingleAsync();
    }

    private async Task<Guid> ReclassificacaoDoPerfilAsync(Guid perfil)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Reclassificacoes
            .Where(r => r.PerfilId == perfil).Select(r => r.Id).SingleAsync();
    }

    private Task LerODocumentoDaVersaoAsync(Guid perfil, string leitor) =>
        ExecutarAsync(
            """
            INSERT INTO autorizacao.leituras_do_documento_da_versao (id, documento_id, leitor_emissor, leitor_sub, instante)
            SELECT {0}, d.id, {1}, {2}, now() FROM autorizacao.documentos_da_versao d WHERE d.perfil_id = {3}
            """,
            Guid.CreateVersion7(), TestAuthHandler.DefaultIssuer, leitor, perfil);

    private async Task<(Guid Perfil, string Pessoa)> PerfilConcedidoComCatalogoAnteriorAsync(ClassificacaoDeProtecao anterior, bool deUnidade)
    {
        Guid perfil = await SemearPerfilVigenteAsync(Codigo, Outra);
        string pessoa = $"pessoa.{Guid.NewGuid():N}";
        await ConcessoesDeTeste.ConcederAsync(Fixture.Factory.Services, pessoa, perfil);
        Guid versao = await VersaoVigenteAsync(perfil);
        await ExecutarAsync(
            "UPDATE autorizacao.versao_do_perfil_permissoes SET classificacao = {0}, de_unidade = {1} WHERE versao_id = {2} AND codigo = {3}",
            anterior.ToString().ToLowerInvariant(), deUnidade, versao, Codigo);
        await GravarCatalogoAnteriorAsync(Codigo, anterior, deUnidade, new string('a', 64));

        return (perfil, pessoa);
    }

    private Task GravarCatalogoAnteriorAsync(string codigo, ClassificacaoDeProtecao classificacao, bool deUnidade, string impressao)
    {
        Guid id = Guid.CreateVersion7();

        return ExecutarAsync(
            """
            WITH atual AS (SELECT id FROM autorizacao.implantacoes_das_permissoes ORDER BY sequencia DESC LIMIT 1),
            novo AS (INSERT INTO autorizacao.implantacoes_das_permissoes (id, impressao, implantada_em) VALUES ({0}, {1}, now()) RETURNING id)
            INSERT INTO autorizacao.permissoes_implantadas (implantacao_id, codigo, modulo, classificacao, de_unidade, reservada)
            SELECT novo.id, p.codigo, p.modulo,
                   CASE WHEN p.codigo = {2} THEN {3} ELSE p.classificacao END,
                   CASE WHEN p.codigo = {2} THEN {4} ELSE p.de_unidade END,
                   p.reservada
            FROM autorizacao.permissoes_implantadas p, atual, novo
            WHERE p.implantacao_id = atual.id
            """,
            id, impressao, codigo, classificacao.ToString().ToLowerInvariant(), deUnidade);
    }

    private Task GravarCatalogoSemAPermissaoAsync(string codigo, string impressao) =>
        ExecutarAsync(
            """
            WITH atual AS (SELECT id FROM autorizacao.implantacoes_das_permissoes ORDER BY sequencia DESC LIMIT 1),
            novo AS (INSERT INTO autorizacao.implantacoes_das_permissoes (id, impressao, implantada_em) VALUES ({0}, {1}, now()) RETURNING id)
            INSERT INTO autorizacao.permissoes_implantadas (implantacao_id, codigo, modulo, classificacao, de_unidade, reservada)
            SELECT novo.id, p.codigo, p.modulo, p.classificacao, p.de_unidade, p.reservada
            FROM autorizacao.permissoes_implantadas p, atual, novo
            WHERE p.implantacao_id = atual.id AND p.codigo <> {2}
            """,
            Guid.CreateVersion7(), impressao, codigo);

    private async Task ConciliarAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ConciliacaoDoCatalogo>().ExecutarAsync(CancellationToken.None);
    }

    private async Task ProcessarAvisosAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProcessamentoDosAvisosDaReclassificacao>().ExecutarAsync(CancellationToken.None);
    }

    private async Task<IReadOnlyList<string>> PermissoesDaPessoaAsync(string sub)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UsuarioRef pessoa = UsuarioRef.From(TestAuthHandler.DefaultIssuer, sub).Value!;
        IReadOnlyList<ConcessaoAtiva> concessoes = await scope.ServiceProvider
            .GetRequiredService<IConcessoesAtivasDoSujeito>().ListarAsync(pessoa, CancellationToken.None);

        return [.. concessoes.SelectMany(static c => c.Permissoes)];
    }

    private Task<long> ContarAsync(string sql, params object[] parametros) => Consultar(sql, parametros);

    private async Task<long> Consultar(string sql, object[] parametros)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        return await db.Database.SqlQueryRaw<long>(sql, parametros).SingleAsync();
    }

    private Task<long> ContarCatalogosAsync() =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.implantacoes_das_permissoes""");

    private Task<long> ContarReclassificacoesAsync(Guid perfil) =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.reclassificacoes_da_permissao WHERE perfil_id = {0}""", perfil);

    private Task<long> ContarBasesConciliadasAsync(Guid perfil) =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.bases_da_permissao_na_concessao WHERE perfil_id = {0} AND origem = 'conciliacao'""", perfil);

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
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        return (await db.ImplantacoesDasPermissoes.AsNoTracking().OrderByDescending(c => c.Sequencia).Select(c => c.Impressao).FirstAsync()).Trim();
    }
}
