namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Paginacao;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// O que as listagens do módulo Seleção precisam para o teste de cursor: certames publicados pelo endpoint, clientes
/// com a identidade do token e a concessão de configurar processos por unidade.
/// </summary>
internal sealed class ApoioDosCursoresDeSelecao
{
    internal const string EmissorDeOutroProvedor = "https://outro-idp.test/realms/unifesspa";

    private readonly CascadingFixture _fixture;
    private readonly List<Guid> _perfisSemeados = [];
    private readonly List<string> _nomesDosCertames = [];

    internal ApoioDosCursoresDeSelecao(CascadingFixture fixture) => _fixture = fixture;

    internal IServiceProvider Servicos => _fixture.Factory.Services;

    internal static Guid UnidadeCeps => UnidadesDeTeste.Ceps.OrigemId;

    internal static Guid UnidadeCga => UnidadesDeTeste.Cga.OrigemId;

    internal ProvaDoCursor Prova(HttpClient cliente, string rota, string propriedadeDoId = "id") =>
        new(new ConsultaPaginada(cliente, rota, propriedadeDoId), new CursoresDeTeste(Servicos));

    internal HttpClient Anonimo() => _fixture.Factory.CreateClient();

    internal HttpClient Cliente(string conta, string? emissor = null)
    {
        HttpClient cliente = _fixture.Factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, conta);
        cliente.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, conta);
        if (emissor is not null)
        {
            cliente.DefaultRequestHeaders.Add(TestAuthHandler.IssuerHeader, emissor);
        }

        return cliente;
    }

    /// <summary>A pessoa passa a poder configurar processos nas unidades, pelo perfil concedido a cada uma.</summary>
    internal async Task ConcederConfiguracaoAsync(string conta, params Guid[] unidades)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(Servicos, PermissaoSelecao.ProcessosConfigurar.Codigo);
        _perfisSemeados.Add(perfil);
        foreach (Guid unidade in unidades)
        {
            await ConcessoesDeTeste.ConcederAsync(Servicos, conta, perfil, EscopoDaConcessao.DaUnidade(unidade));
        }
    }

    /// <summary>Publica <paramref name="quantidade"/> certames cujo nome começa por <paramref name="nome"/>.</summary>
    internal async Task PublicarCertamesAsync(string nome, int quantidade)
    {
        _nomesDosCertames.Add(nome);
        await TiposDeAtoSeeder.SemearAsync(Servicos);
        using HttpClient operador = _fixture.Factory.CreateClient();
        for (int i = 0; i < quantidade; i++)
        {
            (Guid processoId, Guid documentoId) = await SemearProcessoAsync(_fixture.Factory, nome);
            using HttpResponseMessage publicacao = await PublicarAsync(operador, processoId, documentoId);
            publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
        }
    }

    /// <summary>A situação das inscrições e a primeira modalidade ofertada do primeiro certame da busca, para o teste trocá-las.</summary>
    internal async Task<(string Situacao, string Modalidade)> PerfilDoCertameAsync(string nome)
    {
        using HttpClient cliente = Anonimo();
        string corpo = await cliente.GetStringAsync(new Uri($"/api/selecao/certames?q={nome}&limit=1", UriKind.Relative));
        using JsonDocument documento = JsonDocument.Parse(corpo);
        JsonElement certame = documento.RootElement.EnumerateArray().First();

        return (
            certame.GetProperty("situacao").GetProperty("codigo").GetString()!,
            certame.GetProperty("modalidadesOfertadas").EnumerateArray().First().GetString()!);
    }

    /// <summary>
    /// Apaga o que o apoio semeou: as concessões dos perfis que criou e os certames que publicou. O processo publicado não
    /// se apaga de vez, porque o banco retém o documento confirmado e as versões da configuração; ele é desativado, e a
    /// linha que a vitrine lê é removida.
    /// </summary>
    internal async Task RemoverSemeadosAsync()
    {
        await using AsyncServiceScope escopo = Servicos.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        foreach (Guid perfil in _perfisSemeados)
        {
            await RemoverConcessoesAsync(db, perfil);
        }

        foreach (string nome in _nomesDosCertames)
        {
            await DesativarCertamesAsync(db, nome);
        }

        _perfisSemeados.Clear();
        _nomesDosCertames.Clear();
    }

    private static async Task RemoverConcessoesAsync(UniPlusDbContext db, Guid perfil)
    {
        await db.Database.ExecuteSqlAsync($"DELETE FROM autorizacao.bases_da_permissao_concedida WHERE perfil_id = {perfil}");
        await db.Database.ExecuteSqlAsync($"DELETE FROM autorizacao.concessoes WHERE perfil_id = {perfil}");
    }

    private static async Task DesativarCertamesAsync(UniPlusDbContext db, string nome)
    {
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.certames_divulgados WHERE starts_with(nome, {nome})");
        await db.Database.ExecuteSqlAsync($"UPDATE selecao.processos_seletivos SET is_deleted = true WHERE starts_with(nome, {nome})");
    }

    internal static string NomeUnico(string prefixo) => $"{prefixo}{Guid.NewGuid().ToString("N")[..10]}";
}
