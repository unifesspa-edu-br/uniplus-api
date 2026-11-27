namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao.ApoioDasDecisoesDaProtecao;

/// <summary>
/// O cursor da lista das decisões da proteção do catálogo continua só a consulta que o emitiu: ato, cadastro, período,
/// busca e o recorte das próprias decisões; os atos dados em outra ordem ou repetidos são a mesma consulta.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class CursorDasDecisoesDaProtecaoTests : IAsyncLifetime
{
    private readonly ApoioDasDecisoesDaProtecao _apoio;
    private readonly List<string> _marcas = [];

    public CursorDasDecisoesDaProtecaoTests(ConfiguracaoEndpointFixture fixture) => _apoio = new ApoioDasDecisoesDaProtecao(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (string marca in _marcas)
        {
            await _apoio.RemoverAprovacoesAsync(marca);
        }
    }

    [Theory(DisplayName = "Acrescentar um filtro ao cursor da lista de decisões recusa a continuação")]
    [InlineData("ato", "APROVACAO")]
    [InlineData("cadastro", "TIPO_DOCUMENTO")]
    [InlineData("de", "2020-01-01")]
    [InlineData("ate", "2999-12-31")]
    [InlineData("minhas", "true")]
    public async Task Continuar_FiltroAcrescentado_Recusa(string parametro, string valor)
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"busca={marca}", parametro, valor);
        }
    }

    [Theory(DisplayName = "Trocar o valor do filtro no cursor da lista de decisões recusa a continuação")]
    [InlineData("ato", "APROVACAO", "DEVOLUCAO")]
    [InlineData("cadastro", "TIPO_DOCUMENTO", "DADO_DO_CANDIDATO")]
    [InlineData("de", "2020-01-01", "2020-01-02")]
    [InlineData("ate", "2999-12-31", "2999-12-30")]
    [InlineData("minhas", "true", "false")]
    public async Task Continuar_FiltroTrocado_Recusa(string parametro, string inicial, string novo)
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"busca={marca}&{parametro}={inicial}", parametro, novo);
        }
    }

    [Fact(DisplayName = "Trocar a busca no cursor da lista de decisões recusa a continuação")]
    public async Task Continuar_BuscaTrocada_Recusa()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"busca={marca}", "busca", $"{marca} Documento 0");
        }
    }

    [Fact(DisplayName = "Os atos em outra ordem, repetidos, a busca com espaços e o padrão dito de novo são a mesma consulta")]
    public async Task Continuar_EquivalentesDaConsulta_Continua()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.ContinuaComAsync($"busca={marca}&ato=APROVACAO&ato=DEVOLUCAO", "ato", "DEVOLUCAO", "APROVACAO", "DEVOLUCAO");
            await prova.ContinuaComAsync($"busca={marca}", "busca", $"  {marca} ");
            await prova.ContinuaComAsync($"busca={marca}", "minhas", "false");
            await prova.ContinuaComAsync($"busca={marca}", "ato", "APROVACAO", "AJUSTE_RECOMENDADO", "DEVOLUCAO");
        }
    }

    [Fact(DisplayName = "O percurso inteiro com a consulta equivalente a cada página não pula nem repete decisão")]
    public async Task Percorrer_ComConsultaEquivalente_NaoPulaNemRepete()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.ConfereOPercursoEquivalenteAsync($"busca={marca}&ato=APROVACAO", 1, link => LinkDePaginacao.ComParametro(link, "busca", $"  {marca} "));
        }
    }

    [Fact(DisplayName = "O cursor das decisões não leva a busca nem a conta, e o adulterado ou vencido é recusado")]
    public async Task Cursor_PayloadEAdulteracao()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.ConferePayloadAsync($"busca={marca}&minhas=true", vinculadoAConta: false, marca, ContaDeQuemConsulta);
            await prova.RecusaCursorAdulteradoAsync($"busca={marca}");
            await prova.RecusaCursorVencidoAsync($"busca={marca}");
        }
    }

    [Fact(DisplayName = "A conferência vale também no link prev da lista de decisões")]
    public async Task Cursor_LinkAnterior_ConfereAConsulta()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.ConfereOLinkAnteriorAsync($"busca={marca}", static link => LinkDePaginacao.ComParametro(link, "cadastro", "TIPO_DOCUMENTO"));
        }
    }

    private async Task<(ProvaDoCursor Prova, string Marca)> ProvaAsync()
    {
        string marca = await _apoio.RegistrarAprovacoesAsync(3);
        _marcas.Add(marca);

        return (_apoio.Prova(), marca);
    }
}
