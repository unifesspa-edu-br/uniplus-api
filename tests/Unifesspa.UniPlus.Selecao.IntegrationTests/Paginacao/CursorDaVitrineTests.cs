namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Paginacao;

using System.Net;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Paginacao.ApoioDosCursoresDeSelecao;

/// <summary>
/// O cursor da vitrine pública de certames continua só a situação, a modalidade, a busca, a ordenação e o conjunto de
/// certames que o emitiram; a revisão do conjunto muda quando uma publicação o altera.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class CursorDaVitrineTests : IAsyncLifetime
{
    private const string Rota = "/api/selecao/certames";
    private const string PropriedadeDoId = "processoSeletivoId";

    private readonly ApoioDosCursoresDeSelecao _apoio;

    public CursorDaVitrineTests(CascadingFixture fixture) => _apoio = new ApoioDosCursoresDeSelecao(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _apoio.RemoverSemeadosAsync();

    [Theory(DisplayName = "Acrescentar um filtro ao cursor da vitrine recusa a continuação")]
    [InlineData("situacao", "ENCERRADAS")]
    [InlineData("modalidade", "AMPLA_CONCORRENCIA")]
    [InlineData("sort", "-nome")]
    public async Task Continuar_FiltroAcrescentado_Recusa(string parametro, string valor)
    {
        (ProvaDoCursor prova, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"q={nome}", parametro, valor);
        }
    }

    [Fact(DisplayName = "Trocar a situação, a modalidade ou a ordenação no cursor da vitrine recusa a continuação")]
    public async Task Continuar_FiltroTrocado_Recusa()
    {
        (ProvaDoCursor prova, string nome) = await ProvaAsync();
        using (prova)
        {
            (string situacao, string modalidade) = await _apoio.PerfilDoCertameAsync(nome);
            string outraSituacao = situacao == "ENCERRADAS" ? "EM_BREVE" : "ENCERRADAS";

            await prova.RecusaTrocaAsync($"q={nome}&situacao={situacao}", "situacao", outraSituacao);
            await prova.RecusaTrocaAsync($"q={nome}&modalidade={modalidade}", "modalidade", "OUTRA_MODALIDADE");
            await prova.RecusaTrocaAsync($"q={nome}&sort=nome", "sort", "-nome");
        }
    }

    [Theory(DisplayName = "Trocar a coluna de ordenação do cursor da vitrine, mantendo o sentido, recusa a continuação")]
    [InlineData("nome", "inscricoesDe")]
    [InlineData("nome", "inscricoesAte")]
    [InlineData("nome", "divulgadoEm")]
    [InlineData("-nome", "-inscricoesDe")]
    public async Task Continuar_ColunaTrocadaComOMesmoSentido_Recusa(string emitida, string reapresentada)
    {
        (ProvaDoCursor prova, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"q={nome}&sort={emitida}", "sort", reapresentada);
        }
    }

    [Fact(DisplayName = "Trocar a busca no cursor da vitrine recusa a continuação")]
    public async Task Continuar_BuscaTrocada_Recusa()
    {
        (ProvaDoCursor prova, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaTrocaAsync($"q={nome}", "q", $"{nome} outra");
        }
    }

    [Fact(DisplayName = "Os contadores pedidos e a busca com espaços nas pontas não mudam a consulta")]
    public async Task Continuar_EquivalentesDaConsulta_Continua()
    {
        (ProvaDoCursor prova, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.ContinuaComAsync($"q={nome}", "incluir_contadores", "true");
            await prova.ContinuaComAsync($"q={nome}", "q", $"  {nome} ");
            await prova.ConfereOPercursoEquivalenteAsync($"q={nome}", 1, link => LinkDePaginacao.ComParametro(link, "q", $"  {nome} "));
        }
    }

    [Fact(DisplayName = "Publicar outro certame do mesmo recorte muda a revisão e recusa o cursor emitido antes")]
    public async Task Continuar_RevisaoMudada_RecusaPorDivergencia()
    {
        (ProvaDoCursor prova, string nome) = await ProvaAsync();
        using (prova)
        {
            string proximo = await prova.ProximoAsync($"q={nome}");
            await _apoio.PublicarCertamesAsync(nome, 1);

            await prova.ConfereRecusaPorDivergenciaAsync(proximo);
        }
    }

    [Fact(DisplayName = "O cursor da vitrine não leva o texto da busca, a situação nem resumo de conta")]
    public async Task Cursor_Payload_NaoLevaABusca()
    {
        (ProvaDoCursor prova, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.ConferePayloadAsync($"q={nome}", vinculadoAConta: false, nome);
        }
    }

    [Fact(DisplayName = "Situação inválida com o cursor de outra consulta é recusada pelo filtro, não pelo cursor")]
    public async Task Continuar_SituacaoInvalida_RecusaPeloFiltro()
    {
        (ProvaDoCursor prova, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaPeloFiltroAsync(
                $"q={nome}", "situacao", "encerradas", HttpStatusCode.BadRequest, "uniplus.selecao.certame.filtro_de_situacao_invalido");
        }
    }

    [Fact(DisplayName = "Cursor sem a chave, de outra coleção, malformado, de outra consulta ou vencido é recusado")]
    public async Task Cursor_Adulterado_Recusa()
    {
        (ProvaDoCursor prova, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.RecusaCursorAdulteradoAsync($"q={nome}");
            await prova.RecusaCursorVencidoAsync($"q={nome}");
        }
    }

    [Fact(DisplayName = "A conferência vale também no link prev da vitrine")]
    public async Task Cursor_LinkAnterior_ConfereAConsulta()
    {
        (ProvaDoCursor prova, string nome) = await ProvaAsync();
        using (prova)
        {
            await prova.ConfereOLinkAnteriorAsync($"q={nome}", static link => LinkDePaginacao.ComParametro(link, "sort", "-nome"));
        }
    }

    private async Task<(ProvaDoCursor Prova, string Nome)> ProvaAsync()
    {
        string nome = NomeUnico("Vitrine");
        await _apoio.PublicarCertamesAsync(nome, 3);

        return (_apoio.Prova(_apoio.Anonimo(), Rota, PropriedadeDoId), nome);
    }
}
