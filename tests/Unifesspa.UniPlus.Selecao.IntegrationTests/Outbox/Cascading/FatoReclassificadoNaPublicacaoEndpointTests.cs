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
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// O fato do formulário que o catálogo reclassificou para derivado depois de vinculado deixa a publicação recusada com uma causa
/// por fato, cada uma no campo <c>fatoCodigo</c> do item ou do campo do grupo no documento, e nenhuma outra causa.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class FatoReclassificadoNaPublicacaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public FatoReclassificadoNaPublicacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A publicação recusa o item e o campo do grupo reclassificados para derivado, uma causa por fato, cada uma no seu fatoCodigo")]
    public async Task Publicar_ItemECampoDeGrupoReclassificados_Retorna422ComOsDoisCaminhos()
    {
        (string item, string campo) = CodigosDosFatos();
        using HttpClient client = _fixture.Factory.CreateClient();
        (Guid processoId, Guid documentoId) = await ProcessoComFatosReclassificadosAsync(client, item, campo);

        HttpResponseMessage resposta = await PublicacaoPeloEndpointDeTeste.PublicarAsync(client, processoId, documentoId);

        JsonElement[] erros = await ErrosDaRecusaAsync(resposta);
        erros.Select(static e => e.GetProperty("code").GetString()).Should().Equal(Enumerable.Repeat("uniplus.selecao.processo_seletivo.fato_coletado_nao_mais_declarado", 2));
        int posicaoDoItem = await PosicaoDoItemAsync(processoId, item);
        erros.Select(static e => e.GetProperty("field").GetString()).Should().SatisfyRespectively(
            campo => campo.Should().MatchRegex($@"^/formularios/\d+/itens/{posicaoDoItem}/fatoCodigo$"),
            campo => campo.Should().MatchRegex(@"^/formularios/\d+/grupos/0/subitens/0/fatoCodigo$"));
    }

    private static (string Item, string Campo) CodigosDosFatos()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        return ($"ITEM_{sufixo}", $"CAMPO_{sufixo}");
    }

    private async Task<(Guid ProcessoId, Guid DocumentoId)> ProcessoComFatosReclassificadosAsync(HttpClient client, string item, string campo)
    {
        await SemearFatosAsync(item, campo);
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        (Guid processoId, Guid documentoId) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(_fixture.Factory, "Fato reclassificado");
        await GravarItemEGrupoAsync(client, processoId, item, campo);
        await ReclassificarParaDerivadoAsync(item, campo);

        return (processoId, documentoId);
    }

    private static async Task<JsonElement[]> ErrosDaRecusaAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);

        return [.. problema.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.Clone())];
    }

    private async Task SemearFatosAsync(string item, string campo)
    {
        FatoCandidato doItem = FatoDeclarado(item, EscopoFato.Candidato);
        FatoCandidato doGrupo = FatoDeclarado(campo, EscopoFato.MembroGrupo);
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.AddRange(doItem, doGrupo);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
    }

    private static FatoCandidato FatoDeclarado(string codigo, EscopoFato escopo) => CriacaoDoFato.CriarDoAdministrador(
        codigo, "Fato do formulário", "Descrição do fato.", DominioFato.Booleano, CardinalidadeFato.Escalar, fonteValores: null, formato: null,
        "INSCRICAO", escopo, ClassificacaoDeProtecao.Pessoal, "Composição familiar", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;

    private static async Task GravarItemEGrupoAsync(HttpClient client, Guid processoId, string item, string campo)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens", UriKind.Relative))
        {
            Content = JsonContent.Create(ItemEGrupo(item, campo)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        HttpResponseMessage gravado = await client.SendAsync(request);

        gravado.IsSuccessStatusCode.Should().BeTrue(await gravado.Content.ReadAsStringAsync());
    }

    private static object ItemEGrupo(string item, string campo) => new
    {
        itens = new[] { FormularioDeInscricaoHttp.Item(item, 0, "Item", "BOOLEANO", "NUNCA", etapaCodigo: FormularioDeTeste.Secao) },
        grupos = new[]
        {
            FormularioDeInscricaoHttp.Grupo(
                "COMPOSICAO_FAMILIAR", 1, "Composição familiar", FormularioDeTeste.Secao,
                [FormularioDeInscricaoHttp.Item(campo, 0, "Campo", "BOOLEANO", "NUNCA")], new OcorrenciasDoGrupoInput(0, false, 10)),
        },
    };

    private async Task ReclassificarParaDerivadoAsync(string item, string campo)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE configuracao.fatos_do_candidato SET origem = 'DERIVADO' WHERE codigo IN ({item}, {campo})");
    }

    // O formulário de inscrição guarda outros itens além do gravado, e o caminho do documento é a posição por ordem entre eles.
    private async Task<int> PosicaoDoItemAsync(Guid processoId, string item)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string[] itensEmOrdem = await db.Set<FatoColetado>().AsNoTracking()
            .Where(f => f.ProcessoSeletivoId == processoId && f.GrupoColetadoId == null && f.Finalidade == Regras.Formularios.FinalidadeFormulario.Inscricao)
            .OrderBy(f => f.Ordem).Select(f => f.FatoCodigo).ToArrayAsync();

        return Array.IndexOf(itensEmOrdem, item);
    }
}
