namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Paginacao;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

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
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

/// <summary>
/// O que as listagens do módulo Autorização precisam para o teste de cursor: clientes com a identidade do token e as
/// permissões de governança, e os dados que fazem a listagem ter mais de uma página.
/// </summary>
internal sealed class ApoioDosCursoresDeAutorizacao
{
    internal const string EmissorDeOutroProvedor = "https://outro-idp.test/realms/unifesspa";
    internal const string RotaDosPedidos = "/api/autorizacao/pedidos-de-concessao";
    internal const string RotaDasConcessoes = "/api/autorizacao/concessoes";
    internal const string RotaDasProvidencias = "/api/autorizacao/providencias";
    internal const string RotaDasRevisoes = "/api/autorizacao/revisoes";

    // Permissão de unidade: só se concede no escopo de uma unidade.
    private const string PermissaoDeUnidade = "teste:inscricoes:operar";

    // Permissão institucional: só se concede no escopo da instituição, o padrão da concessão sem escopo informado.
    private const string PermissaoInstitucional = "configuracao:tipos-processo:consultar";
    private const string FundamentoDaProvidencia = "O servidor deixou a banca do edital 12/2026 e mantém acesso a documentos sensíveis.";

    private readonly AutorizacaoEndpointFixture _fixture;
    private readonly List<Guid> _perfisSemeados = [];
    private readonly List<Guid> _revisoesSemeadas = [];
    private readonly List<Guid> _concessoesDeGovernancaFeitas = [];

    internal ApoioDosCursoresDeAutorizacao(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

    internal IServiceProvider Servicos => _fixture.Factory.Services;

    internal CursoresDeTeste Cursores => new(Servicos);

    internal static string EmailDe(string conta) => $"{conta}@unifesspa.edu.br";

    internal HttpClient Cliente(string conta, string? emissor = null, string? email = null, string emailVerificado = "true")
    {
        HttpClient cliente = _fixture.Factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, conta);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, conta);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email ?? EmailDe(conta));
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.EmailVerifiedHeader, emailVerificado);
        if (emissor is not null)
        {
            cliente.DefaultRequestHeaders.Add(TestAuthHandler.IssuerHeader, emissor);
        }

        return cliente;
    }

    /// <summary>O token só identifica a pessoa; a permissão vem da concessão do perfil de governança, como em produção.</summary>
    internal async Task<HttpClient> ClienteDeGovernancaAsync(string conta, TipoDeGovernanca governanca)
    {
        Guid perfil = ConcessoesDeTeste.PerfilDe(governanca);
        bool jaExistia = await ExisteConcessaoAsync(conta, perfil);
        Guid concessao = await ConcessoesDeTeste.ConcederGovernancaAsync(Servicos, conta, governanca);
        if (!jaExistia)
        {
            _concessoesDeGovernancaFeitas.Add(concessao);
        }

        return Cliente(conta);
    }

    internal async Task SemearConcessoesAsync(int quantidade)
    {
        Guid perfil = await CriarPerfilSemeadoAsync(PermissaoInstitucional);
        for (int i = 0; i < quantidade; i++)
        {
            await ConcessoesDeTeste.ConcederAsync(Servicos, $"beneficiaria.{Guid.NewGuid():N}", perfil);
        }
    }

    /// <summary>Duas concessões da mesma conta, em perfis diferentes: a consulta por pessoa passa de uma página.</summary>
    internal async Task SemearConcessoesDaContaAsync(string conta, int quantidade)
    {
        for (int i = 0; i < quantidade; i++)
        {
            Guid perfil = await CriarPerfilSemeadoAsync(PermissaoInstitucional);
            await ConcessoesDeTeste.ConcederAsync(Servicos, conta, perfil);
        }
    }

    internal async Task SemearProvidenciasAsync(int quantidade)
    {
        using HttpClient solicitante = await ClienteDeGovernancaAsync("cecilia.moura", TipoDeGovernanca.GestorDeProtecaoDeDados);
        for (int i = 0; i < quantidade; i++)
        {
            Guid perfil = await CriarPerfilSemeadoAsync(PermissaoDeUnidade);
            Guid concessao = await ConcessoesDeTeste.ConcederAsync(
                Servicos, $"beneficiaria.{Guid.NewGuid():N}", perfil, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
            await SolicitarProvidenciaAsync(solicitante, concessao);
        }
    }

    /// <summary>Revisões sem item: nascem concluídas, e mais de uma pode existir.</summary>
    internal async Task SemearRevisoesConcluidasAsync(int quantidade)
    {
        for (int i = 0; i < quantidade; i++)
        {
            await using AsyncServiceScope escopo = Servicos.CreateAsyncScope();
            RevisaoDeAcessos revisao = RevisaoDeAcessos.Abrir(
                new Identidade(TestAuthHandler.DefaultIssuer, "marlene.martins"), [], DateTimeOffset.UtcNow);
            await escopo.ServiceProvider.GetRequiredService<IRevisaoDeAcessosRepository>().AdicionarAsync(revisao);
            await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().SaveChangesAsync();
            _revisoesSemeadas.Add(revisao.Id);
        }
    }

    /// <summary>
    /// Apaga o que este apoio semeou: as concessões de governança que fez, e, dos perfis que criou, os pedidos, as
    /// providências, as bases, as concessões, as versões e o próprio perfil, além das revisões que abriu. Nenhuma dessas
    /// tabelas tem trava que impeça a exclusão; só a ordem das chaves estrangeiras importa.
    /// </summary>
    internal async Task RemoverSemeadosAsync()
    {
        foreach (Guid concessao in _concessoesDeGovernancaFeitas)
        {
            await RemoverConcessaoAsync(concessao);
        }

        foreach (Guid perfil in _perfisSemeados)
        {
            await RemoverPerfilAsync(perfil);
        }

        await RemoverRevisoesAsync();
        _concessoesDeGovernancaFeitas.Clear();
        _perfisSemeados.Clear();
    }

    private async Task RemoverRevisoesAsync()
    {
        foreach (Guid revisao in _revisoesSemeadas)
        {
            await ExecutarSqlAsync("DELETE FROM autorizacao.revisoes_de_acessos WHERE id = {0}", revisao);
        }

        _revisoesSemeadas.Clear();
    }

    /// <summary>Recomeça os pedidos da conta: apaga os que ela fez e cria <paramref name="quantidade"/> pedidos para si mesma.</summary>
    internal async Task SemearPedidosParaSiMesmaAsync(string conta, int quantidade)
    {
        await RemoverPedidosDoTesteDaContaAsync(conta);
        using HttpClient cliente = Cliente(conta);
        for (int i = 0; i < quantidade; i++)
        {
            await PedirParaSiMesmaAsync(cliente, i);
        }
    }

    // Só os pedidos dos perfis que este apoio criou: pedido de outro teste, mesmo da mesma conta, não é tocado.
    private async Task RemoverPedidosDoTesteDaContaAsync(string conta)
    {
        foreach (Guid perfil in _perfisSemeados)
        {
            await ExecutarSqlAsync(
                "DELETE FROM autorizacao.aceites_do_termo WHERE pedido_id IN "
                + "(SELECT id FROM autorizacao.pedidos_de_concessao WHERE perfil_id = {0} AND solicitante_conta_id = {1})", perfil, conta);
            await ExecutarSqlAsync(
                "DELETE FROM autorizacao.pedidos_de_concessao WHERE perfil_id = {0} AND solicitante_conta_id = {1}", perfil, conta);
        }
    }

    /// <summary>Leva à segunda etapa os pedidos da conta, para a fila da proteção de dados ter mais de uma página.</summary>
    internal async Task AguardarAProtecaoDeDadosAsync(string conta)
    {
        foreach (Guid perfil in _perfisSemeados)
        {
            await ExecutarSqlAsync(
                "UPDATE autorizacao.pedidos_de_concessao SET situacao = {0} WHERE perfil_id = {1} AND solicitante_conta_id = {2}",
                "AGUARDANDO_PROTECAO_DE_DADOS", perfil, conta);
        }
    }

    private async Task RemoverConcessaoAsync(Guid concessao)
    {
        await ExecutarSqlAsync("DELETE FROM autorizacao.providencias WHERE concessao_id = {0}", concessao);
        await ExecutarSqlAsync("DELETE FROM autorizacao.bases_da_permissao_concedida WHERE concessao_id = {0}", concessao);
        await ExecutarSqlAsync("DELETE FROM autorizacao.concessoes WHERE id = {0}", concessao);
    }

    private async Task RemoverPerfilAsync(Guid perfil)
    {
        await ExecutarSqlAsync(
            "DELETE FROM autorizacao.aceites_do_termo WHERE pedido_id IN "
            + "(SELECT id FROM autorizacao.pedidos_de_concessao WHERE perfil_id = {0})", perfil);
        await ExecutarSqlAsync("DELETE FROM autorizacao.pedidos_de_concessao WHERE perfil_id = {0}", perfil);
        await ExecutarSqlAsync(
            "DELETE FROM autorizacao.providencias WHERE concessao_id IN (SELECT id FROM autorizacao.concessoes WHERE perfil_id = {0})", perfil);
        await ExecutarSqlAsync("DELETE FROM autorizacao.bases_da_permissao_concedida WHERE perfil_id = {0}", perfil);
        await ExecutarSqlAsync("DELETE FROM autorizacao.concessoes WHERE perfil_id = {0}", perfil);
        await RemoverVersoesDoPerfilAsync(perfil);
        await ExecutarSqlAsync("DELETE FROM autorizacao.permissoes_do_perfil WHERE perfil_id = {0}", perfil);
        await ExecutarSqlAsync("DELETE FROM autorizacao.perfis WHERE id = {0} AND nome LIKE 'Perfil de teste %'", perfil);
    }

    private async Task RemoverVersoesDoPerfilAsync(Guid perfil)
    {
        await ExecutarSqlAsync("DELETE FROM autorizacao.vigencias_do_perfil WHERE perfil_id = {0}", perfil);
        await ExecutarSqlAsync(
            "DELETE FROM autorizacao.decisoes_da_versao WHERE versao_id IN (SELECT id FROM autorizacao.versoes_do_perfil WHERE perfil_id = {0})", perfil);
        await ExecutarSqlAsync(
            "DELETE FROM autorizacao.recalculos_da_aprovacao WHERE versao_id IN (SELECT id FROM autorizacao.versoes_do_perfil WHERE perfil_id = {0})", perfil);
        await ExecutarSqlAsync("DELETE FROM autorizacao.versoes_do_perfil WHERE perfil_id = {0}", perfil);
    }

    private async Task<bool> ExisteConcessaoAsync(string conta, Guid perfil)
    {
        await using AsyncServiceScope escopo = Servicos.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Concessoes.AnyAsync(
            c => c.BeneficiarioEmissor == TestAuthHandler.DefaultIssuer && c.BeneficiarioContaId == conta && c.PerfilId == perfil
                && c.RevogadaEm == null);
    }

    private async Task<Guid> CriarPerfilSemeadoAsync(string permissao)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(Servicos, permissao);
        _perfisSemeados.Add(perfil);

        return perfil;
    }

    private async Task PedirParaSiMesmaAsync(HttpClient cliente, int numero)
    {
        Guid perfil = await CriarPerfilSemeadoAsync(PermissaoInstitucional);
        using HttpResponseMessage criado = await EnviarAsync(
            cliente, RotaDosPedidos, new { perfilId = perfil, justificativa = $"Acompanha os tipos de processo seletivo, pedido {numero}." });
        criado.StatusCode.Should().Be(HttpStatusCode.Created, await criado.Content.ReadAsStringAsync());
    }

    private static async Task SolicitarProvidenciaAsync(HttpClient solicitante, Guid concessaoId)
    {
        using HttpResponseMessage criada = await EnviarAsync(
            solicitante, RotaDasProvidencias, new { concessaoId, tipo = "RETIRADA", fundamento = FundamentoDaProvidencia });
        criada.StatusCode.Should().Be(HttpStatusCode.Created, await criada.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string rota, object corpo)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Post, new Uri(rota, UriKind.Relative)) { Content = JsonContent.Create(corpo) };
        pedido.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        return await cliente.SendAsync(pedido);
    }

    private async Task ExecutarSqlAsync(string sql, params object[] parametros)
    {
        await using AsyncServiceScope escopo = Servicos.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(sql, parametros);
    }
}
