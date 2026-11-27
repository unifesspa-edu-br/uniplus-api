namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Paginacao;

using System.Net;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using static Unifesspa.UniPlus.Autorizacao.IntegrationTests.Paginacao.ApoioDosCursoresDeAutorizacao;

/// <summary>
/// O cursor da lista de concessões continua só a consulta que o emitiu: situação, perfil, unidade e a pessoa (o par
/// emissor e conta) ficam amarrados ao cursor, e o que não recorta a lista não o invalida.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class CursorDasConcessoesTests : IAsyncLifetime
{
    private const string Gestor = "diogo.souza";
    private const string PessoaConsultada = "leandro.viana";
    private const string OutroIdentificador = "019a0000-0000-7000-8000-0000000000aa";

    private readonly ApoioDosCursoresDeAutorizacao _apoio;

    public CursorDasConcessoesTests(AutorizacaoEndpointFixture fixture) => _apoio = new ApoioDosCursoresDeAutorizacao(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverSemeadosAsync();

    [Theory(DisplayName = "Acrescentar um filtro ao cursor da lista sem filtro recusa a continuação")]
    [InlineData("situacao", "ATIVA")]
    [InlineData("perfil", OutroIdentificador)]
    [InlineData("unidade", OutroIdentificador)]
    public async Task Continuar_FiltroAcrescentado_Recusa(string parametro, string valor)
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync(string.Empty, parametro, valor);
    }

    [Theory(DisplayName = "Trocar o valor do filtro no cursor da lista recusa a continuação")]
    [InlineData("situacao", "ATIVA", "REVOGADA")]
    [InlineData("situacao", "ATIVA", "VENCIDA")]
    public async Task Continuar_FiltroTrocado_Recusa(string parametro, string inicial, string novo)
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaTrocaAsync($"{parametro}={inicial}", parametro, novo);
    }

    [Fact(DisplayName = "Informar o par emissor e conta no meio do percurso recusa como outra consulta")]
    public async Task Continuar_ParDaPessoaAcrescentado_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaAsync(
            string.Empty,
            static link => LinkDePaginacao.ComParametro(LinkDePaginacao.ComParametro(link, "emissor", EmissorDeOutroProvedor), "contaId", PessoaConsultada));
    }

    [Fact(DisplayName = "Informar só o emissor no meio do percurso é recusado pelo filtro da pessoa, não pelo cursor")]
    public async Task Continuar_SoOEmissor_RecusaPeloFiltroDaPessoa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaPeloFiltroAsync(
            string.Empty, "emissor", EmissorDeOutroProvedor, HttpStatusCode.BadRequest, "uniplus.autorizacao.concessao.filtro_de_pessoa_invalido");
    }

    [Fact(DisplayName = "O cursor da lista de uma pessoa não leva o emissor, a conta nem resumo de conta")]
    public async Task Cursor_ListaDeUmaPessoa_NaoLevaAIdentificacao()
    {
        await _apoio.SemearConcessoesDaContaAsync(PessoaConsultada, 2);
        using ProvaDoCursor prova = await ProvaAsync();
        string pessoa = $"emissor={Uri.EscapeDataString(TestAuthHandler.DefaultIssuer)}&contaId={PessoaConsultada}";

        await prova.ConferePayloadAsync(pessoa, vinculadoAConta: false, PessoaConsultada, TestAuthHandler.DefaultIssuer);
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado ou de outra consulta é recusado")]
    public async Task Continuar_CursorAdulterado_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaCursorAdulteradoAsync(string.Empty);
    }

    [Fact(DisplayName = "A chave com uma coluna de ordenação a mais, mantido o resumo, é recusada como cursor de outra consulta")]
    public async Task Continuar_ChaveComColunaAMais_Recusa()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaChaveComColunaAMaisAsync(string.Empty);
    }

    [Fact(DisplayName = "O cursor vencido da lista responde 410")]
    public async Task Continuar_CursorVencido_Responde410()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.RecusaCursorVencidoAsync(string.Empty);
    }

    [Fact(DisplayName = "A conferência vale também no link prev: com a mesma consulta volta, com outra recusa")]
    public async Task Continuar_LinkAnterior_ConfereAConsulta()
    {
        using ProvaDoCursor prova = await ProvaAsync();

        await prova.ConfereOLinkAnteriorAsync(string.Empty, static link => LinkDePaginacao.ComParametro(link, "situacao", "ATIVA"));
    }

    [Fact(DisplayName = "O percurso inteiro com a pessoa repetida com espaços nas pontas a cada página não pula nem repete concessão")]
    public async Task Percorrer_PessoaComEspacosNasPontas_NaoPulaNemRepete()
    {
        await _apoio.SemearConcessoesDaContaAsync(PessoaConsultada, 3);
        using ProvaDoCursor prova = await ProvaAsync();
        string pessoa = $"emissor={Uri.EscapeDataString(TestAuthHandler.DefaultIssuer)}&contaId={PessoaConsultada}";

        await prova.ConfereOPercursoEquivalenteAsync(
            pessoa, 2, static link => LinkDePaginacao.ComParametro(link, "emissor", $" {TestAuthHandler.DefaultIssuer} "));
    }

    private async Task<ProvaDoCursor> ProvaAsync()
    {
        await _apoio.SemearConcessoesAsync(5);
        HttpClient gestor = await _apoio.ClienteDeGovernancaAsync(Gestor, TipoDeGovernanca.GestorDeAcessos);

        return new ProvaDoCursor(new ConsultaPaginada(gestor, RotaDasConcessoes), _apoio.Cursores);
    }
}
