namespace Unifesspa.UniPlus.Host.IntegrationTests.Governanca;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O perfil de Encarregado de dados isolado, pelo host real: consulta pedidos, concessões, revisões, perfis, a proteção
/// do catálogo de tipos de documento e de fatos do candidato e as providências sobre concessões, com cada leitura
/// registrada pela permissão que a autorizou, e é recusado em toda fila de decisão e em toda operação que decide, revoga
/// ou solicita a aprovação da proteção daquele catálogo, inclusive responder ou atender providência. Cecília Moura é a
/// Encarregada.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class ConsultasDoEncarregadoNoHostTests : PedidosDeConcessaoTestBase
{
    private const int TamanhoDaPagina = 3;

    private static readonly Persona Cecilia = new("cecilia.moura", "Cecília Ester Aurora Moura", "cecilia_ester_moura@uniplus-test.local");

    public ConsultasDoEncarregadoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "O Encarregado isolado consulta cada leitura da matriz, e a leitura grava a permissão que a autorizou")]
    [InlineData("/api/autorizacao/pedidos-de-concessao?fila=todos", "autorizacao:pedidos:consultar")]
    [InlineData("/api/autorizacao/concessoes", "autorizacao:concessoes:consultar")]
    [InlineData("/api/autorizacao/instalacoes/tentativas-sem-pendencia", "autorizacao:concessoes:consultar")]
    [InlineData("/api/autorizacao/revisoes", "autorizacao:revisoes:consultar")]
    [InlineData("/api/autorizacao/perfis", "autorizacao:perfis:consultar-versoes")]
    [InlineData("/api/autorizacao/governanca/divergencias", "autorizacao:perfis:consultar-versoes")]
    [InlineData("/api/configuracao/admin/protecao-do-catalogo", "configuracao:protecao-do-catalogo:consultar")]
    [InlineData("/api/configuracao/admin/protecao-do-catalogo/decisoes", "configuracao:protecao-do-catalogo:consultar")]
    [InlineData("/api/autorizacao/providencias", "autorizacao:providencias:solicitar")]
    public async Task Leitura_DoEncarregadoIsolado_PermitidaERegistrada(string rota, string permissao)
    {
        using HttpClient cecilia = await EncarregadaAsync();
        int antes = Registro.Registrados.Count;

        using HttpResponseMessage resposta = await cecilia.GetAsync(new Uri(rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        DaCecilia(antes).Should().Contain(r => r.Permissao == permissao && r.Resultado == ResultadoDoAcesso.Permitido);
    }

    [Theory(DisplayName = "O Encarregado isolado é recusado em toda fila de decisão e em toda operação que decide, revoga, solicita a aprovação da proteção ou responde e atende providência")]
    [InlineData("GET", "/api/autorizacao/pedidos-de-concessao?fila=gestor")]
    [InlineData("GET", "/api/autorizacao/pedidos-de-concessao?fila=protecao_de_dados")]
    [InlineData("GET", "/api/autorizacao/versoes-de-perfil?fila=protecao_de_dados")]
    [InlineData("GET", "/api/autorizacao/janelas-de-migracao?etapa=protecao_de_dados")]
    [InlineData("GET", "/api/autorizacao/reclassificacoes?etapa=protecao_de_dados")]
    [InlineData("POST", "/api/autorizacao/revisoes")]
    [InlineData("POST", "/api/configuracao/admin/protecao-do-catalogo")]
    [InlineData("POST", "/api/configuracao/admin/protecao-do-catalogo/solicitacoes")]
    [InlineData("POST", "/api/autorizacao/providencias/019a0000-0000-7000-8000-0000000000f1/resposta")]
    [InlineData("POST", "/api/autorizacao/providencias/019a0000-0000-7000-8000-0000000000f1/atendimento")]
    public async Task Decisao_DoEncarregadoIsolado_Recusada(string metodo, string rota)
    {
        using HttpClient cecilia = await EncarregadaAsync();

        using HttpResponseMessage resposta = await EnviarAsync(cecilia, new HttpMethod(metodo), rota, new { }, Guid.NewGuid().ToString());

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "O Encarregado isolado não revoga concessão: 403 e a concessão continua ativa")]
    public async Task Revogacao_DoEncarregadoIsolado_Recusada()
    {
        await ResetarAsync();
        await SemearConcessaoAsync(Leandro, PerfisDeSistema.GestorDeAcessosId, EscopoDaConcessao.DaInstituicao());
        Guid concessao = (await ConcessoesAsync(Leandro, PerfisDeSistema.GestorDeAcessosId)).Single().Id;
        using HttpClient cecilia = await EncarregadaAsync();

        using HttpResponseMessage resposta = await RevogarAsync(cecilia, concessao, "Teste.", Leandro.Email);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ConcessoesAsync(Leandro, PerfisDeSistema.GestorDeAcessosId)).Single().RevogadaEm.Should().BeNull();
    }

    [Fact(DisplayName = "Com uma permissão comum compatível, o Encarregado continua exercendo-a")]
    public async Task PermissaoComumCompativel_ContinuaValendo()
    {
        using HttpClient cecilia = await EncarregadaAsync();
        using HttpResponseMessage semAPermissao = await cecilia.GetAsync(new Uri("/api/configuracao/admin/tipos-processo", UriKind.Relative));

        await ConcessoesDeTeste.ConcederPermissoesAsync(Fixture.Factory.Services, Cecilia.Sub.ToString(), "configuracao:tipos-processo:consultar");
        using HttpResponseMessage comAPermissao = await cecilia.GetAsync(new Uri("/api/configuracao/admin/tipos-processo", UriKind.Relative));

        (semAPermissao.StatusCode, comAPermissao.StatusCode).Should().Be((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    [Fact(DisplayName = "Vencida a concessão de Encarregado, a consulta é recusada com 403 e a negativa é registrada")]
    public async Task ConcessaoVencida_ConsultaRecusadaComNegativaRegistrada()
    {
        using HttpClient cecilia = await EncarregadaAsync();
        await ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '3 days', valida_ate = now() - interval '1 minute' WHERE beneficiario_sub = {0} AND perfil_id = {1}",
            Cecilia.Sub.ToString(),
            PerfisDeSistema.EncarregadoDeDadosId);
        int antes = Registro.Registrados.Count;

        using HttpResponseMessage resposta = await cecilia.GetAsync(new Uri("/api/autorizacao/concessoes", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        DaCecilia(antes).Should().Contain(r => r.Permissao == "autorizacao:concessoes:consultar" && r.Resultado == ResultadoDoAcesso.Negado);
    }

    [Fact(DisplayName = "A página cheia da fila de todos os pedidos grava a decisão da fila e um registro por pedido exibido")]
    public async Task ListagemDePedidos_PaginaCheia_UmRegistroPorPedido()
    {
        await ResetarAsync();
        (Guid perfil, Guid unidade) = (await SemearPerfilInternoAsync(), await SemearUnidadeAsync());
        foreach (Persona solicitante in new[] { Betina, Leandro, Marlene, Isabella })
        {
            await CriarAsync(Cliente(solicitante), perfil, unidade, null);
        }

        IReadOnlyList<RegistroDecisaoAcesso> registros = await RegistrosDaLeituraAsync($"{Rota}?fila=todos&limit={TamanhoDaPagina}");

        registros.Should().HaveCount(TamanhoDaPagina + 1, "a decisão da fila e um registro por pedido exibido");
        registros.Count(static r => r.ResourceId is not null && r.ResourceTipo == "pedido-de-concessao").Should().Be(TamanhoDaPagina);
    }

    [Fact(DisplayName = "A página cheia das concessões grava a decisão da borda e um registro por concessão exibida")]
    public async Task ListagemDeConcessoes_PaginaCheia_UmRegistroPorConcessao()
    {
        await ResetarAsync();
        (Guid perfil, Guid unidade) = (await SemearPerfilInternoAsync(), await SemearUnidadeAsync());
        foreach (Persona beneficiario in new[] { Betina, Leandro, Marlene, Isabella })
        {
            await SemearConcessaoAsync(beneficiario, perfil, EscopoDaConcessao.DaUnidade(unidade));
        }

        IReadOnlyList<RegistroDecisaoAcesso> registros = await RegistrosDaLeituraAsync($"/api/autorizacao/concessoes?situacao=todas&limit={TamanhoDaPagina}");

        registros.Should().HaveCount(TamanhoDaPagina + 1, "a decisão da borda e um registro por concessão exibida");
        registros.Count(static r => r.ResourceId is not null && r.ResourceTipo == "concessao").Should().Be(TamanhoDaPagina);
    }

    [Fact(DisplayName = "A página cheia das revisões grava a decisão da borda e um registro por revisão exibida")]
    public async Task ListagemDeRevisoes_PaginaCheia_UmRegistroPorRevisao()
    {
        await ResetarAsync();
        for (int i = 0; i <= TamanhoDaPagina; i++)
        {
            await SemearRevisaoVaziaAsync();
        }

        IReadOnlyList<RegistroDecisaoAcesso> registros = await RegistrosDaLeituraAsync($"/api/autorizacao/revisoes?limit={TamanhoDaPagina}");

        registros.Should().HaveCount(TamanhoDaPagina + 1, "a decisão da borda e um registro por revisão exibida");
        registros.Count(static r => r.ResourceId is not null && r.ResourceTipo == ResourceTiposAutorizacao.Revisao).Should().Be(TamanhoDaPagina);
    }

    private async Task SemearRevisaoVaziaAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRevisaoDeAcessosRepository>()
            .AdicionarAsync(RevisaoDeAcessos.Abrir(IdentidadeDe(Marlene), [], DateTimeOffset.UtcNow));
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }

    // Os registros que uma só leitura da Encarregada mandou ao destino, conferida a página cheia.
    private async Task<IReadOnlyList<RegistroDecisaoAcesso>> RegistrosDaLeituraAsync(string rota)
    {
        using HttpClient cecilia = await EncarregadaAsync();
        int antes = Registro.Registrados.Count;

        JsonElement pagina = await LerAsync(cecilia, rota);

        (pagina.ValueKind == JsonValueKind.Array ? pagina : pagina.GetProperty("items")).GetArrayLength().Should().Be(TamanhoDaPagina);
        return [.. DaCecilia(antes)];
    }

    private RegistroDeDecisoesDeTeste Registro => Fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>();

    private IEnumerable<RegistroDecisaoAcesso> DaCecilia(int antes) =>
        Registro.Registrados.Skip(antes).Where(static r => r.AtorSubject == Cecilia.Sub.ToString());

    // Cecília só com o perfil de Encarregado de dados, recém-concedido e vigente.
    private async Task<HttpClient> EncarregadaAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Cecilia.Sub.ToString());
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Cecilia.Sub.ToString(), TipoDeGovernanca.EncarregadoDeDados, Cecilia.Email);
        return Cliente(Cecilia);
    }
}
