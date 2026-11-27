namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento.RequisicoesDeTermoConsentimento;

/// <summary>
/// O cursor da lista de termos de consentimento continua só a busca que o emitiu e não leva o texto dela, e o cursor
/// vencido ou adulterado é recusado.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class CursorDosTermosDeConsentimentoTests : IAsyncLifetime
{
    private const string Rota = "/api/configuracao/termos-consentimento";

    private readonly ApoioDosCursoresDeConfiguracao _apoio;
    private readonly List<Guid> _criados = [];

    public CursorDosTermosDeConsentimentoTests(ConfiguracaoEndpointFixture fixture) => _apoio = new ApoioDosCursoresDeConfiguracao(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverTermosAsync(_criados);

    [Fact(DisplayName = "O cursor da lista de termos não leva o texto da busca nem resumo de conta")]
    public async Task Cursor_Payload_NaoLevaABusca()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.ConferePayloadAsync($"q={marca}", vinculadoAConta: false, marca);
        }
    }

    [Fact(DisplayName = "O cursor vencido da lista de termos responde 410")]
    public async Task Continuar_CursorVencido_Responde410()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaCursorVencidoAsync($"q={marca}");
        }
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado ou de outra consulta é recusado")]
    public async Task Continuar_CursorAdulterado_Recusa()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaCursorAdulteradoAsync($"q={marca}");
        }
    }

    [Fact(DisplayName = "A conferência vale também no link prev da lista de termos: com a mesma busca volta, com outra recusa")]
    public async Task Continuar_LinkAnterior_ConfereABusca()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.ConfereOLinkAnteriorAsync($"q={marca}", link => LinkDePaginacao.ComParametro(link, "q", $"{marca} Dois"));
        }
    }

    private async Task<(ProvaDoCursor Prova, string Marca)> ProvaAsync()
    {
        string marca = Guid.NewGuid().ToString("N")[..10];
        using HttpClient cliente = _apoio.Cliente();
        foreach (string numero in new[] { "Um", "Dois", "Tres" })
        {
            _criados.Add(await CriarTermoComNome(cliente, $"Termo {marca} {numero}"));
        }

        return (_apoio.Prova(Rota), marca);
    }
}
