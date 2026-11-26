namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Os dois itens de seleção do formulário cujo catálogo ficou sem valor ativo depois de vinculados deixam a publicação recusada
/// com uma causa por item, cada uma no campo <c>fatoCodigo</c> do item no documento, e nenhuma outra causa.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ValoresSelecionaveisDeFatosSemOfertaNaPublicacaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public ValoresSelecionaveisDeFatosSemOfertaNaPublicacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A publicação recusa os dois itens de seleção sem valor ativo no catálogo, uma causa por item, cada uma no seu fatoCodigo")]
    public async Task Publicar_DoisItensSemValorAtivo_Retorna422ComOsDoisCaminhos()
    {
        (string primeiro, string segundo) = CodigosDosFatos();
        using HttpClient client = _fixture.Factory.CreateClient();
        (Guid processoId, Guid documentoId) = await ProcessoComItensSemOfertaAsync(client, primeiro, segundo);

        HttpResponseMessage resposta = await PublicacaoPeloEndpointDeTeste.PublicarAsync(client, processoId, documentoId);

        JsonElement[] erros = await ErrosDaRecusaAsync(resposta);
        erros.Should().HaveCount(2);
        erros.Select(static e => e.GetProperty("code").GetString()).Distinct().Should().ContainSingle();
        int posicaoDoPrimeiro = await PosicaoDoItemAsync(processoId, primeiro);
        int posicaoDoSegundo = await PosicaoDoItemAsync(processoId, segundo);
        erros.Select(static e => e.GetProperty("field").GetString()).Should().SatisfyRespectively(
            campo => campo.Should().MatchRegex($@"^/formularios/\d+/itens/{posicaoDoPrimeiro}/codigoDoDado$"),
            campo => campo.Should().MatchRegex($@"^/formularios/\d+/itens/{posicaoDoSegundo}/codigoDoDado$"));
    }

    private static (string Primeiro, string Segundo) CodigosDosFatos()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        return ($"SELECAO_A_{sufixo}", $"SELECAO_B_{sufixo}");
    }

    private async Task<(Guid ProcessoId, Guid DocumentoId)> ProcessoComItensSemOfertaAsync(HttpClient client, string primeiro, string segundo)
    {
        await SemearFatosAsync(primeiro, segundo);
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        (Guid processoId, Guid documentoId) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(_fixture.Factory, "Itens sem oferta");
        await GravarItensAsync(client, processoId, primeiro, segundo);
        await DesativarOsValoresAsync(primeiro, segundo);

        return (processoId, documentoId);
    }

    private static async Task<JsonElement[]> ErrosDaRecusaAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);

        return [.. problema.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.Clone())];
    }

    private async Task SemearFatosAsync(string primeiro, string segundo)
    {
        FatoCandidato doPrimeiro = FatoCategorico(primeiro);
        FatoCandidato doSegundo = FatoCategorico(segundo);
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.AddRange(doPrimeiro, doSegundo);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
    }

    private static FatoCandidato FatoCategorico(string codigo)
    {
        FatoCandidato fato = CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, "Fato de seleção", "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, FonteValoresFato.Catalogo, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, "Finalidade de teste", HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;
        fato.AdicionarValorDominio($"V_{codigo}", "Opção única.", 0, ativo: true).IsSuccess.Should().BeTrue();

        return fato;
    }

    private static async Task GravarItensAsync(HttpClient client, Guid processoId, string primeiro, string segundo)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens", UriKind.Relative))
        {
            Content = JsonContent.Create(Itens(primeiro, segundo)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        HttpResponseMessage gravado = await client.SendAsync(request);

        gravado.IsSuccessStatusCode.Should().BeTrue(await gravado.Content.ReadAsStringAsync());
    }

    private static object Itens(string primeiro, string segundo) => new
    {
        itens = new[]
        {
            Item(primeiro, ordem: 0),
            Item(segundo, ordem: 1),
        },
        grupos = Array.Empty<object>(),
    };

    private static object Item(string fatoCodigo, int ordem) =>
        FormularioDeInscricaoHttp.Item(fatoCodigo, ordem, "Item", "ESCOLHA_UNICA", "NUNCA", paginaCodigo: FormularioDeTeste.Secao);

    private async Task DesativarOsValoresAsync(string primeiro, string segundo)
    {
        string valorDoPrimeiro = $"V_{primeiro}";
        string valorDoSegundo = $"V_{segundo}";
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE configuracao.opcoes_dos_dados SET ativo = false WHERE codigo IN ({valorDoPrimeiro}, {valorDoSegundo})");
    }

    // O formulário de inscrição guarda outros itens além dos gravados, e o caminho do documento é a posição por ordem entre eles.
    private async Task<int> PosicaoDoItemAsync(Guid processoId, string item)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid formularioId = await FormulariosDoProcessoDeTeste.IdentificadorDoFormularioAsync(db, processoId, FormulariosDoProcessoDeTeste.Inscricao);
        string[] itensEmOrdem = await db.Set<FatoColetado>().AsNoTracking()
            .Where(f => f.ProcessoSeletivoId == processoId && f.GrupoColetadoId == null && f.FormularioId == formularioId)
            .OrderBy(f => f.Ordem).Select(f => f.FatoCodigo).ToArrayAsync();

        return Array.IndexOf(itensEmOrdem, item);
    }
}
