namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Paginacao;

using System.Net;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

/// <summary>
/// O cursor da lista de manutenção dos dados do candidato continua só a origem e o estado que o emitiram; a origem
/// inválida é recusada pelo filtro antes de o cursor ser conferido.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class CursorDosDadosDoCandidatoTests
{
    private const string Rota = "/api/configuracao/admin/fatos-candidato";
    private const string CaminhoDoId = "fato.id";

    private readonly ApoioDosCursoresDeConfiguracao _apoio;

    public CursorDosDadosDoCandidatoTests(ConfiguracaoEndpointFixture fixture) => _apoio = new ApoioDosCursoresDeConfiguracao(fixture);

    [Theory(DisplayName = "Acrescentar um filtro ao cursor da lista de dados do candidato recusa a continuação")]
    [InlineData("origem", "DECLARADO")]
    [InlineData("ativo", "true")]
    public async Task Continuar_FiltroAcrescentado_Recusa(string parametro, string valor)
    {
        using ProvaDoCursor prova = _apoio.Prova(Rota, CaminhoDoId);

        await prova.RecusaTrocaAsync(string.Empty, parametro, valor);
    }

    [Theory(DisplayName = "Trocar o valor do filtro no cursor da lista de dados do candidato recusa a continuação")]
    [InlineData("origem", "DECLARADO", "CALCULADO")]
    [InlineData("ativo", "true", "false")]
    public async Task Continuar_FiltroTrocado_Recusa(string parametro, string inicial, string novo)
    {
        using ProvaDoCursor prova = _apoio.Prova(Rota, CaminhoDoId);

        await prova.RecusaTrocaAsync($"{parametro}={inicial}", parametro, novo);
    }

    [Fact(DisplayName = "O mesmo estado dito de novo continua, e o percurso inteiro não pula nem repete dado")]
    public async Task Continuar_MesmoFiltro_Continua()
    {
        using ProvaDoCursor prova = _apoio.Prova(Rota, CaminhoDoId);

        await prova.ContinuaComAsync("ativo=true", "ativo", "true");
        await prova.ConfereOPercursoEquivalenteAsync("ativo=true", 5, static link => LinkDePaginacao.ComParametro(link, "ativo", "true"));
    }

    [Fact(DisplayName = "Origem inválida com o cursor de outra consulta é recusada pelo filtro, não pelo cursor")]
    public async Task Continuar_OrigemInvalida_RecusaPeloFiltro()
    {
        using ProvaDoCursor prova = _apoio.Prova(Rota, CaminhoDoId);

        await prova.RecusaPeloFiltroAsync(
            string.Empty, "origem", "declarado", HttpStatusCode.BadRequest, "uniplus.configuracao.fato_candidato.filtro_de_origem_invalida");
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado, de outra consulta ou vencido é recusado")]
    public async Task Cursor_Adulterado_Recusa()
    {
        using ProvaDoCursor prova = _apoio.Prova(Rota, CaminhoDoId);

        await prova.RecusaCursorAdulteradoAsync(string.Empty);
        await prova.RecusaCursorVencidoAsync(string.Empty);
        await prova.ConferePayloadAsync("origem=DECLARADO", vinculadoAConta: false, "DECLARADO");
    }

    [Fact(DisplayName = "A conferência vale também no link prev da lista de dados do candidato")]
    public async Task Cursor_LinkAnterior_ConfereAConsulta()
    {
        using ProvaDoCursor prova = _apoio.Prova(Rota, CaminhoDoId);

        await prova.ConfereOLinkAnteriorAsync(string.Empty, static link => LinkDePaginacao.ComParametro(link, "ativo", "true"));
    }
}
