namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Paginacao;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// O cursor da lista de unidades continua só a busca e o conjunto de tipos que o emitiram; os tipos em outra ordem ou
/// repetidos e a busca em outra caixa são a mesma consulta.
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
public sealed class CursorDasUnidadesTests : IAsyncLifetime
{
    private const string Rota = "/api/organizacao/unidades";

    // O parâmetro tipo leva o número do tipo de unidade.
    private const string Centro = "3";
    private const string Instituto = "4";
    private const string Faculdade = "5";

    private readonly OrganizacaoEndpointFixture _fixture;
    private readonly List<string> _marcas = [];

    public CursorDasUnidadesTests(OrganizacaoEndpointFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (string marca in _marcas)
        {
            await _fixture.RemoverUnidadesDaMarcaAsync(marca);
        }
    }

    [Fact(DisplayName = "Acrescentar um tipo ao cursor da lista sem tipo recusa a continuação")]
    public async Task Continuar_TipoAcrescentado_Recusa()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"q={marca}", "tipo", Centro);
        }
    }

    [Fact(DisplayName = "Trocar, acrescentar ou retirar um tipo no cursor da lista recusa a continuação")]
    public async Task Continuar_ConjuntoDeTiposAlterado_Recusa()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            string consulta = $"q={marca}&tipo={Centro}&tipo={Instituto}";

            await prova.RecusaTrocaAsync(consulta, "tipo", Centro);
            await prova.RecusaTrocaAsync(consulta, "tipo", Centro, Instituto, Faculdade);
            await prova.RecusaTrocaAsync(consulta, "tipo");
        }
    }

    [Fact(DisplayName = "Os tipos em outra ordem ou repetidos são o mesmo conjunto e continuam")]
    public async Task Continuar_TiposReordenadosOuRepetidos_Continuam()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            string consulta = $"q={marca}&tipo={Centro}&tipo={Instituto}";

            await prova.ContinuaComAsync(consulta, "tipo", Instituto, Centro);
            await prova.ContinuaComAsync(consulta, "tipo", Centro, Centro, Instituto);
            await prova.ConfereOPercursoEquivalenteAsync(
                consulta, 1, link => LinkDePaginacao.ComParametro(link, "tipo", Instituto, Centro, Instituto));
        }
    }

    [Fact(DisplayName = "Trocar a busca recusa a continuação, e a busca em outra caixa ou com espaços nas pontas continua")]
    public async Task Continuar_BuscaAlterada()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"q={marca}", "q", $"{marca}1");
            await prova.ContinuaComAsync($"q={marca}", "q", marca.ToUpperInvariant());
            await prova.ContinuaComAsync($"q={marca}", "q", $"  {marca} ");
        }
    }

    [Fact(DisplayName = "O cursor da lista de unidades não leva o texto da busca nem resumo de conta")]
    public async Task Cursor_Payload_NaoLevaABusca()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.ConferePayloadAsync($"q={marca}&tipo={Centro}", vinculadoAConta: false, marca);
        }
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado, de outra consulta ou vencido é recusado")]
    public async Task Cursor_Adulterado_Recusa()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaCursorAdulteradoAsync($"q={marca}");
            await prova.RecusaCursorVencidoAsync($"q={marca}");
        }
    }

    [Fact(DisplayName = "A conferência vale também no link prev da lista de unidades")]
    public async Task Cursor_LinkAnterior_ConfereAConsulta()
    {
        (ProvaDoCursor prova, string marca) = await ProvaAsync();
        using (prova)
        {
            await prova.ConfereOLinkAnteriorAsync($"q={marca}", link => LinkDePaginacao.ComParametro(link, "tipo", Centro));
        }
    }

    private async Task<(ProvaDoCursor Prova, string Marca)> ProvaAsync()
    {
        string marca = Guid.NewGuid().ToString("N")[..10];
        _marcas.Add(marca);
        await _fixture.SemearUnidadeAsync($"{marca}1", TipoUnidade.Centro);
        await _fixture.SemearUnidadeAsync($"{marca}2", TipoUnidade.Centro);
        await _fixture.SemearUnidadeAsync($"{marca}3", TipoUnidade.Instituto);
        await _fixture.SemearUnidadeAsync($"{marca}4", TipoUnidade.Instituto);
        ProvaDoCursor prova = new(
            new ConsultaPaginada(_fixture.Factory.CreateClient(), Rota), new CursoresDeTeste(_fixture.Factory.Services));

        return (prova, marca);
    }
}
