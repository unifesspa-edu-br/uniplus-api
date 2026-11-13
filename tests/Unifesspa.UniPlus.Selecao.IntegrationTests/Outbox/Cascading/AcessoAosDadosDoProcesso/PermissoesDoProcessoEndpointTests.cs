namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// As permissões de configurar, publicar e acompanhar o processo, decididas pela concessão na unidade dona dele
/// (ADR-0078, ADR-0151). Cada persona recebe só a concessão do cenário, e o papel <c>plataforma-admin</c> do token não
/// vale nada: sem concessão o processo responde 404, como o que não existe.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class PermissoesDoProcessoEndpointTests : IAsyncLifetime
{
    private readonly ConcessoesDasPersonas _concessoes;
    private readonly RequisicoesAutenticadas _http;
    private readonly MontagemDoProcesso _processos;

    public PermissoesDoProcessoEndpointTests(CascadingFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _concessoes = new ConcessoesDasPersonas(fixture);
        _http = new RequisicoesAutenticadas(fixture);
        _processos = new MontagemDoProcesso(fixture, _http);
    }

    public Task InitializeAsync() => _concessoes.RemoverDeTodasAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Configurar vale na unidade dona do processo; a concessão em outra unidade responde 404, como o processo inexistente")]
    public async Task Configurar_SoNaUnidadeDona()
    {
        Guid processoId = await _processos.SemearRascunhoAsync(nameof(Configurar_SoNaUnidadeDona));
        Guid unidade = await _processos.UnidadeDoProcessoAsync(processoId);
        await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, unidade, PermissaoSelecao.ProcessosConfigurar);
        await _concessoes.ConcederAsync(PersonasDoAcesso.Betina, Guid.CreateVersion7(), PermissaoSelecao.ProcessosConfigurar);

        using HttpResponseMessage dona = await PutTaxaAsync(PersonasDoAcesso.Leandro, processoId);
        using HttpResponseMessage outra = await PutTaxaAsync(PersonasDoAcesso.Betina, processoId);
        using HttpResponseMessage inexistente = await PutTaxaAsync(PersonasDoAcesso.Leandro, Guid.CreateVersion7());

        dona.StatusCode.Should().Be(HttpStatusCode.NoContent, await dona.Content.ReadAsStringAsync());
        outra.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "O papel plataforma-admin sem concessão não configura nem lê o processo")]
    public async Task AdminSemConcessao_NaoAcessa()
    {
        Guid processoId = await _processos.SemearRascunhoAsync(nameof(AdminSemConcessao_NaoAcessa));

        using HttpResponseMessage escrita = await PutTaxaAsync(PersonasDoAcesso.Diogo, processoId, RequisicoesAutenticadas.PapelDeAdministrador);
        using HttpResponseMessage leitura = await _http.GetAsync(PersonasDoAcesso.Diogo, RotaDoProcesso(processoId), RequisicoesAutenticadas.PapelDeAdministrador);

        escrita.StatusCode.Should().Be(HttpStatusCode.NotFound);
        leitura.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Acompanhar não lê o rascunho, nem as telas de trabalho, e não publica nem configura")]
    public async Task Acompanhar_NaoPublicaNemConfigura()
    {
        Guid processoId = await _processos.SemearRascunhoAsync(nameof(Acompanhar_NaoPublicaNemConfigura));
        await _concessoes.ConcederAsync(PersonasDoAcesso.Leandro, await _processos.UnidadeDoProcessoAsync(processoId), PermissaoSelecao.ProcessosAcompanhar);

        using HttpResponseMessage leitura = await _http.GetAsync(PersonasDoAcesso.Leandro, RotaDoProcesso(processoId));
        using HttpResponseMessage publicar = await _http.SendAsync(
            PersonasDoAcesso.Leandro, HttpMethod.Post, $"{RotaDoProcesso(processoId)}/publicacao", new { });
        using HttpResponseMessage conformidade = await _http.GetAsync(PersonasDoAcesso.Leandro, $"{RotaDoProcesso(processoId)}/conformidade");
        using HttpResponseMessage configurar = await PutTaxaAsync(PersonasDoAcesso.Leandro, processoId);

        leitura.StatusCode.Should().Be(HttpStatusCode.NotFound);
        conformidade.StatusCode.Should().Be(HttpStatusCode.NotFound);
        publicar.StatusCode.Should().Be(HttpStatusCode.NotFound);
        configurar.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string RotaDoProcesso(Guid processoId) =>
        $"/api/selecao/processos-seletivos/{processoId}";

    private Task<HttpResponseMessage> PutTaxaAsync(string persona, Guid processoId, string? papel = null) =>
        _http.SendAsync(
            persona, HttpMethod.Put, $"{RotaDoProcesso(processoId)}/taxa-inscricao", new { cobra = false, valor = (decimal?)null, fundamentos = (string[]?)null }, papel);
}
