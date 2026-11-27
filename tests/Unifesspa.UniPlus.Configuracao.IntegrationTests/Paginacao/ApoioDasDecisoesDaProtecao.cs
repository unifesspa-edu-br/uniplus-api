namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

/// <summary>
/// Semeia, pelo HTTP do host, decisões da proteção do catálogo para o teste de cursor: Isabella cadastra e solicita a
/// aprovação de tipos de documento, e o Representante do controlador, Betina, aprova cada um no primeiro ciclo.
/// </summary>
internal sealed class ApoioDasDecisoesDaProtecao
{
    internal const string Rota = "/api/configuracao/admin/protecao-do-catalogo/decisoes";
    internal const string ContaDeQuemConsulta = "betina.neves";

    private const string Base = "/api/configuracao/admin/protecao-do-catalogo";
    private const string Tipos = "/api/configuracao/admin/tipos-documento";
    private const string ContaDeQuemCadastra = "isabella.figueiredo";
    private const string Finalidade = "Comprovação documental do candidato.";

    private readonly ConfiguracaoEndpointFixture _fixture;

    internal ApoioDasDecisoesDaProtecao(ConfiguracaoEndpointFixture fixture) => _fixture = fixture;

    internal HttpClient ClienteDeQuemConsulta() => ClienteDe(ContaDeQuemConsulta, "Betina Marina Valentina das Neves");

    /// <summary>Cada item aprovado tem uma única decisão na busca da marca, então o identificador da decisão é o do item.</summary>
    internal ProvaDoCursor Prova() =>
        new(new ConsultaPaginada(ClienteDeQuemConsulta(), Rota, "itemId"), new CursoresDeTeste(_fixture.Factory.Services));

    /// <summary>Registra uma aprovação por item e devolve a marca que está no nome de todos eles, para a busca isolar o conjunto.</summary>
    internal async Task<string> RegistrarAprovacoesAsync(int quantidade)
    {
        await PrepararContasAsync();
        string marca = $"Hz{Guid.NewGuid().ToString("N")[..8]}";
        using HttpClient quemCadastra = ClienteDe(ContaDeQuemCadastra, "Isabella Priscila Figueiredo");
        IReadOnlyList<Guid> itens = await CadastrarTiposAsync(quemCadastra, marca, quantidade);

        await SolicitarAsync(quemCadastra, itens);
        using HttpClient quemAprova = ClienteDeQuemConsulta();
        foreach (Guid item in itens)
        {
            await AprovarAsync(quemAprova, item);
        }

        return marca;
    }

    /// <summary>Remove os atos registrados e os tipos de documento cadastrados com a marca no nome.</summary>
    internal async Task RemoverAprovacoesAsync(string marca)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marca);

        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM configuracao.atos_da_protecao_do_catalogo WHERE starts_with(item_nome, {marca})");
        await db.Database.ExecuteSqlAsync($"DELETE FROM configuracao.tipos_de_documento WHERE starts_with(nome, {marca})");
    }

    private static async Task<IReadOnlyList<Guid>> CadastrarTiposAsync(HttpClient cliente, string marca, int quantidade)
    {
        List<Guid> itens = [];
        for (int i = 0; i < quantidade; i++)
        {
            itens.Add(await CadastrarTipoAsync(cliente, $"{marca} Documento {i}"));
        }

        return itens;
    }

    private HttpClient ClienteDe(string conta, string nome)
    {
        HttpClient cliente = _fixture.Factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, conta);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, nome);

        return cliente;
    }

    private async Task PrepararContasAsync()
    {
        IServiceProvider servicos = _fixture.Factory.Services;
        await ConcessoesDeTeste.RemoverDaPessoaAsync(servicos, ContaDeQuemCadastra);
        await ConcessoesDeTeste.RemoverDaPessoaAsync(servicos, ContaDeQuemConsulta);
        await ConcessoesDeTeste.ConcederPermissoesAsync(servicos, ContaDeQuemCadastra, PermissaoConfiguracao.TiposDocumentoManter.Codigo);
        await ConcessoesDeTeste.ConcederGovernancaAsync(
            servicos, ContaDeQuemConsulta, TipoDeGovernanca.RepresentanteDoControlador, "betina.neves@unifesspa.edu.br");
    }

    private static async Task<Guid> CadastrarTipoAsync(HttpClient cliente, string nome)
    {
        using HttpResponseMessage criado = await EnviarAsync(cliente, Tipos, new
        {
            codigo = $"TIPO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
            nome,
            categoria = "SAUDE",
            classificacaoProtecao = "INTERNO",
            finalidadeTratamento = Finalidade,
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        criado.StatusCode.Should().Be(HttpStatusCode.Created, await criado.Content.ReadAsStringAsync());

        return await criado.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task SolicitarAsync(HttpClient cliente, IEnumerable<Guid> itens)
    {
        using HttpResponseMessage resposta = await EnviarAsync(
            cliente, $"{Base}/solicitacoes", new { itens = itens.Select(id => new { cadastro = "TIPO_DOCUMENTO", id }) });
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task AprovarAsync(HttpClient cliente, Guid item)
    {
        using HttpResponseMessage resposta = await EnviarAsync(cliente, Base, new
        {
            cadastro = "TIPO_DOCUMENTO",
            itemId = item,
            decisao = "APROVACAO",
            classificacaoProtecao = "INTERNO",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
            finalidadeTratamento = Finalidade,
            ciclo = 1,
            manifestacoesNoCiclo = 0,
        });
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string rota, object corpo)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Post, new Uri(rota, UriKind.Relative)) { Content = JsonContent.Create(corpo) };
        pedido.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        return await cliente.SendAsync(pedido);
    }
}
