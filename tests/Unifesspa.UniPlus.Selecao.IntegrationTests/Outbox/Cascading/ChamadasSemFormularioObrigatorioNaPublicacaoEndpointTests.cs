namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// A fase organizada em chamadas sem formulário obrigatório recusa a publicação com 422, uma recusa por fase, cada uma com o
/// código estável do módulo e a posição da fase no cronograma do documento.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ChamadasSemFormularioObrigatorioNaPublicacaoEndpointTests
{
    private const string Codigo = "uniplus.selecao.formulario_processo.chamadas_sem_formulario_obrigatorio";

    private readonly CascadingFixture _fixture;

    public ChamadasSemFormularioObrigatorioNaPublicacaoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A publicação com duas fases em chamadas sem formulário obrigatório responde 422 com o código e a posição de cada fase")]
    public async Task DuasFasesEmChamadasSemObrigatorio_RecusaComCodigoECampoPosicional()
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        OpcoesDoProcessoSeletivoPublicavel opcoes = new()
        {
            Nome = $"{nameof(DuasFasesEmChamadasSemObrigatorio_RecusaComCodigoECampoPosicional)} {Guid.CreateVersion7()}",
            FasesAdicionais =
            [
                FaseDoCronogramaPublicavelDeTeste.Criar("HABILITACAO", 2, new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 2, 10, 0, 0, 0, TimeSpan.Zero), organizadaEmChamadas: true),
                FaseDoCronogramaPublicavelDeTeste.Criar("AVALIACAO", 3, new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 3, 10, 0, 0, 0, TimeSpan.Zero), organizadaEmChamadas: true),
            ],
        };
        (Guid processoId, Guid documentoId) = await SemearAsync(opcoes);

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        (string Codigo, string Campo)[] recusas =
        [
            .. problema.RootElement.GetProperty("errors").EnumerateArray()
                .Where(static erro => erro.GetProperty("code").GetString() == Codigo)
                .Select(static erro => (erro.GetProperty("code").GetString()!, erro.GetProperty("field").GetString()!)),
        ];
        recusas.Should().Equal((Codigo, "/cronograma/fases/1"), (Codigo, "/cronograma/fases/2"));
    }

    private async Task<(Guid ProcessoId, Guid DocumentoId)> SemearAsync(OpcoesDoProcessoSeletivoPublicavel opcoes)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, opcoes);
        return (processo.Id, documento.Id);
    }
}
