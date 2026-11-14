namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Montagens das suítes da reclassificação pelo host real: o perfil com as concessões, a implantação de outro catálogo
/// pela conciliação, a decisão das etapas e as leituras do banco. Ao começar e ao terminar, o catálogo gravado volta a
/// ser o da aplicação, para que as demais suítes não encontrem a instância como antiga.
/// </summary>
public abstract class ReclassificacaoNoHostTestBase : AprovacaoDaVersaoNoHostTestBase, IAsyncLifetime
{
    private protected const string RotaReclassificacoes = "/api/autorizacao/reclassificacoes";

    private protected ReclassificacaoNoHostTestBase(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    private protected CatalogoDaAplicacao Real => Fixture.Factory.Services.GetRequiredService<CatalogoDaAplicacao>();

    public async Task InitializeAsync()
    {
        await ResetarAsync();
        await ImplantarRealAsync();
    }

    public Task DisposeAsync() => ImplantarRealAsync();

    private protected Task ImplantarRealAsync() => CatalogoAlternativoDeTeste.ImplantarAsync(Fixture.Factory.Services, Real);

    private protected Task ImplantarAsync(string codigo, ClassificacaoDeProtecao classificacao, bool? deUnidade = null) =>
        CatalogoAlternativoDeTeste.ImplantarAsync(Fixture.Factory.Services, CatalogoAlternativoDeTeste.Com(Real, codigo, classificacao, deUnidade));

    /// <summary>Implanta o catálogo anterior com a permissão mais baixa e depois o da aplicação: uma subida de classe.</summary>
    private protected async Task SubirAClasseAsync(string codigo = Sensivel)
    {
        await ImplantarAsync(codigo, ClassificacaoDeProtecao.Interno);
        await ImplantarRealAsync();
    }

    /// <summary>O perfil em vigor com as permissões dadas e uma concessão de unidade para cada beneficiário.</summary>
    private protected async Task<Cenario> CenarioAsync(string[] permissoes, params Persona[] beneficiarios)
    {
        Guid perfil = await SemearPerfilAsync("Reclassificação", permissoes);
        Guid unidade = await SemearUnidadeAsync();
        Dictionary<Persona, Guid> concessoes = [];
        foreach (Persona pessoa in beneficiarios)
        {
            concessoes[pessoa] = await ConcessoesDeTeste.ConcederAsync(
                Fixture.Factory.Services, pessoa.Sub.ToString(), perfil, EscopoDaConcessao.DaUnidade(unidade), email: pessoa.Email);
        }

        return new Cenario(perfil, unidade, concessoes);
    }

    /// <summary>Concede o perfil do cenário a mais uma pessoa, depois do que o teste já fez com ele.</summary>
    private protected Task<Guid> ConcederDepoisAsync(Cenario cenario, Persona pessoa) =>
        ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, pessoa.Sub.ToString(), cenario.Perfil, EscopoDaConcessao.DaUnidade(cenario.Unidade), email: pessoa.Email);

    private protected async Task<string[]> PermissoesAsync(Persona pessoa, Guid perfil)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        IReadOnlyList<ConcessaoAtiva> ativas = await escopo.ServiceProvider.GetRequiredService<IConcessoesAtivasDoSujeito>()
            .ListarAsync(UsuarioRef.From(TestAuthHandler.DefaultIssuer, pessoa.Sub.ToString()).Value!, CancellationToken.None);

        return [.. ativas.Where(c => c.PerfilId == perfil).SelectMany(static c => c.Permissoes)];
    }

    private protected async Task<Guid> ReclassificacaoAsync(Guid perfil, string codigo)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Reclassificacoes.AsNoTracking()
            .Where(r => r.PerfilId == perfil && r.Codigo == codigo).OrderByDescending(r => r.OrdemNaTrilha).Select(r => r.Id).FirstAsync();
    }

    private protected async Task<long> ContarAsync(string sql, params object[] parametros)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.SqlQueryRaw<long>(sql, parametros).SingleAsync();
    }

    private protected async Task<string?> ConsultarTextoAsync(string sql, params object[] parametros)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.SqlQueryRaw<string>(sql, parametros).FirstOrDefaultAsync();
    }

    private protected async Task<string> FilaDaProtecaoAsync()
    {
        using HttpClient protecao = await ProtecaoAsync();
        using HttpResponseMessage fila = await protecao.GetAsync(new Uri($"{RotaReclassificacoes}?etapa={EtapaProtecao}", UriKind.Relative));
        string corpo = await fila.Content.ReadAsStringAsync();
        fila.StatusCode.Should().Be(HttpStatusCode.OK, corpo);

        return corpo;
    }

    private protected Task<long> ReclassificacoesAsync(Guid perfil) =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.reclassificacoes_da_permissao WHERE perfil_id = {0}""", perfil);

    private protected Task<long> LiberacoesAsync(Guid reclassificacao) =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.liberacoes_da_reclassificacao WHERE reclassificacao_id = {0}""", reclassificacao);

    private protected Task<long> BasesDaOrigemAsync(Guid perfil, string origem) =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.bases_da_permissao_na_concessao WHERE perfil_id = {0} AND origem = {1}""", perfil, origem);

    /// <summary>A origem da última linha de base da concessão para o código, ou nada se ela não tem base.</summary>
    private protected async Task<string?> OrigemDaBaseAsync(Guid concessao, string codigo)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.SqlQuery<string>(
            $"""SELECT origem AS "Value" FROM autorizacao.bases_da_permissao_na_concessao WHERE concessao_id = {concessao} AND codigo = {codigo} ORDER BY ordem_na_trilha DESC LIMIT 1""")
            .FirstOrDefaultAsync();
    }

    private protected Task<long> AvisosAsync(Guid perfil) =>
        ContarAsync(
            """
            SELECT count(*) AS "Value" FROM autorizacao.avisos_da_reclassificacao a
            JOIN autorizacao.reclassificacoes_da_permissao r ON a.origem = 'reclassificacao' AND a.origem_id = r.id
            WHERE r.perfil_id = {0}
            """,
            perfil);

    /// <summary>Rebaixa o retrato da permissão na versão de número dado, como o de uma versão criada antes da troca de catálogo.</summary>
    private protected Task RebaixarRetratoAsync(Guid perfil, int numero, string codigo, string classificacao) =>
        ExecutarSqlAsync(
            """
            UPDATE autorizacao.versao_do_perfil_permissoes SET classificacao = {0}
            WHERE codigo = {1} AND versao_id IN (SELECT id FROM autorizacao.versoes_do_perfil WHERE perfil_id = {2} AND numero = {3})
            """,
            classificacao, codigo, perfil, numero);

    private protected async Task ProcessarAvisosAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<ProcessamentoDosAvisosDaReclassificacao>().ExecutarAsync(CancellationToken.None);
    }

    private protected async Task LerODocumentoAsync(Guid perfil, Persona leitor)
    {
        await ExecutarSqlAsync(
            """
            INSERT INTO autorizacao.leituras_do_documento_da_versao (id, documento_id, leitor_emissor, leitor_sub, instante)
            SELECT {0}, d.id, {1}, {2}, now() FROM autorizacao.documentos_da_versao d WHERE d.perfil_id = {3}
            """,
            Guid.CreateVersion7(), TestAuthHandler.DefaultIssuer, leitor.Sub.ToString(), perfil);
    }

    /// <summary>O decisor da etapa: a proteção de dados ou a gestão de acessos, já com o documento lido.</summary>
    /// <summary>
    /// Dispara as operações na ordem dada contra o perfil travado pelo teste: cada uma só entra depois de a anterior ficar
    /// à espera de uma trava, então a fila do banco é a ordem declarada. Solta a trava e espera todas terminarem.
    /// </summary>
    private protected Task EmFilaAsync(Guid perfil, params Func<Task>[] operacoes) =>
        EmFilaSobAsync("SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR UPDATE", perfil, operacoes);

    /// <summary>A mesma fila, sob a trava da concessão, que a revogação disputa sem passar pela do perfil.</summary>
    private protected Task EmFilaSobAConcessaoAsync(Guid concessao, params Func<Task>[] operacoes) =>
        EmFilaSobAsync("SELECT 1 FROM autorizacao.concessoes WHERE id = {0} FOR UPDATE", concessao, operacoes);

    /// <summary>Executa algo enquanto a operação espera a trava do perfil, e só então solta a trava.</summary>
    private protected async Task EnquantoEmEsperaAsync(Guid perfil, Func<Task> operacao, Func<Task> enquanto)
    {
        await using TransacaoRetidaDeTeste trava = await TravarAsync("SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR UPDATE", perfil);
        Task disparada = operacao();
        await trava.AguardarBloqueioAsync();
        await enquanto();
        await trava.ConfirmarAsync();
        await disparada;
    }

    private async Task EmFilaSobAsync(string sql, Guid id, Func<Task>[] operacoes)
    {
        await using TransacaoRetidaDeTeste trava = await TravarAsync(sql, id);
        List<Task> disparadas = [];
        foreach (Func<Task> operacao in operacoes)
        {
            disparadas.Add(operacao());
            await trava.AguardarBloqueioAsync(disparadas.Count);
        }

        await trava.ConfirmarAsync();
        await Task.WhenAll(disparadas);
    }

    private async Task<TransacaoRetidaDeTeste> TravarAsync(string sql, Guid id)
    {
        TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await trava.Servicos.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(sql, id);

        return trava;
    }

    /// <summary>Concede à pessoa a permissão da etapa e a leitura do documento, para que a decisão dela só dependa da trava.</summary>
    private protected async Task<HttpClient> PrepararDecisorAsync(Persona decisor, string etapa, Guid perfil)
    {
        HttpClient cliente = await ClienteComPermissoesAsync(decisor, etapa == EtapaProtecao ? AprovarProtecaoDeDados : AprovarFuncional);
        await LerODocumentoAsync(perfil, decisor);

        return cliente;
    }

    private protected static async Task<(HttpStatusCode Status, string Corpo)> LerRespostaAsync(Task<HttpResponseMessage> chamada)
    {
        using HttpResponseMessage resposta = await chamada;

        return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync());
    }

    private protected async Task<HttpResponseMessage> DecidirAsync(
        Persona decisor, string etapa, Guid perfil, Guid reclassificacao, params Guid[] concessoes)
    {
        using HttpClient cliente = await ClienteComPermissoesAsync(decisor, etapa == EtapaProtecao ? AprovarProtecaoDeDados : AprovarFuncional);
        await LerODocumentoAsync(perfil, decisor);
        object corpo = concessoes.Length == 0
            ? new { etapa, resultado = "liberada", todas = true }
            : new { etapa, resultado = "liberada", concessoes, todas = false };

        return await EnviarAsync(cliente, HttpMethod.Post, $"{RotaReclassificacoes}/{reclassificacao}/decisoes", corpo);
    }

    private protected async Task LiberarAsync(Persona decisor, string etapa, Guid perfil, Guid reclassificacao, params Guid[] concessoes)
    {
        using HttpResponseMessage resposta = await DecidirAsync(decisor, etapa, perfil, reclassificacao, concessoes);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    private protected static async Task ConferirRecusaDaReclassificacaoAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(status, corpo);
        corpo.Should().Contain($"uniplus.autorizacao.reclassificacao.{codigo}");
    }

    private protected sealed record Cenario(Guid Perfil, Guid Unidade, IReadOnlyDictionary<Persona, Guid> Concessoes)
    {
        public Guid De(Persona pessoa) => Concessoes[pessoa];
    }
}
