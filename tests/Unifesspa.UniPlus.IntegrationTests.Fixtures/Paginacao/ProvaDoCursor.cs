namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// As conferências que toda listagem por cursor repete: continuar sob outra consulta recusa, sob consulta equivalente
/// continua, o cursor adulterado ou vencido é recusado e o payload decifrado não leva dado pessoal. Cada listagem informa
/// a consulta inicial, que precisa ter mais de uma página, e a alteração que quer provar. É dona da consulta (e do
/// cliente HTTP) que recebe.
/// </summary>
public sealed class ProvaDoCursor : IDisposable
{
    private const int TamanhoDoResumo = 43;
    private const string ColecaoDeOutroRecurso = "recurso-de-outra-colecao";
    private const string ResumoDeOutraConsulta = "resumo-de-outra-consulta";

    private readonly ConsultaPaginada _consulta;
    private readonly CursoresDeTeste _cursores;

    public ProvaDoCursor(ConsultaPaginada consulta, CursoresDeTeste cursores)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(cursores);

        _consulta = consulta;
        _cursores = cursores;
    }

    public void Dispose() => _consulta.Dispose();

    /// <summary>O link <c>next</c> da primeira página da consulta.</summary>
    public Task<string> ProximoAsync(string consulta) => _consulta.ProximoAsync(consulta);

    public Task<string> AnteriorDaSegundaAsync(string consulta) => _consulta.AnteriorDaSegundaAsync(consulta);

    public Task<HttpResponseMessage> ObterAsync(string link) => _consulta.ObterAsync(link);

    /// <summary>O cursor emitido antes de o conjunto mudar é recusado como cursor de outra consulta.</summary>
    public async Task ConfereRecusaPorDivergenciaAsync(string link)
    {
        using HttpResponseMessage resposta = await _consulta.ObterAsync(link);

        await RecusaDeCursor.DeOutraConsulta.ConferirAsync(resposta);
    }

    /// <summary>Troca, acrescenta ou retira o parâmetro no link da continuação e espera a recusa de outra consulta.</summary>
    public Task RecusaTrocaAsync(string consulta, string parametro, params string[] valores) =>
        RecusaAsync(consulta, link => LinkDePaginacao.ComParametro(link, parametro, valores));

    public async Task RecusaAsync(string consulta, Func<string, string> alteracao)
    {
        ArgumentNullException.ThrowIfNull(alteracao);

        string proximo = await _consulta.ProximoAsync(consulta);

        using HttpResponseMessage resposta = await _consulta.ObterAsync(alteracao(proximo));

        await RecusaDeCursor.DeOutraConsulta.ConferirAsync(resposta);
    }

    /// <summary>A alteração não muda a consulta: a continuação responde 200.</summary>
    public Task ContinuaComAsync(string consulta, string parametro, params string[] valores) =>
        ContinuaAsync(consulta, link => LinkDePaginacao.ComParametro(link, parametro, valores));

    public async Task ContinuaAsync(string consulta, Func<string, string> alteracao)
    {
        ArgumentNullException.ThrowIfNull(alteracao);

        string proximo = await _consulta.ProximoAsync(consulta);

        using HttpResponseMessage resposta = await _consulta.ObterAsync(alteracao(proximo));

        await RespostaDaListagem.ConferirStatusAsync(resposta, HttpStatusCode.OK);
    }

    /// <summary>
    /// A ordem das recusas: o filtro inválido é recusado pelo próprio filtro, mesmo com o cursor de outra consulta, porque a
    /// validação do filtro vem antes da conferência do cursor.
    /// </summary>
    public async Task RecusaPeloFiltroAsync(string consulta, string parametro, string valorInvalido, HttpStatusCode status, string codigo)
    {
        string proximo = await _consulta.ProximoAsync(consulta);

        using HttpResponseMessage resposta = await _consulta.ObterAsync(LinkDePaginacao.ComParametro(proximo, parametro, valorInvalido));

        string corpo = await RespostaDaListagem.ConferirStatusAsync(resposta, status);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("code").GetString().Should().Be(codigo);
    }

    /// <summary>
    /// Sem a chave ou de outra coleção, o cursor é inválido; com a chave malformada ou com o resumo de outra consulta,
    /// é de outra consulta.
    /// </summary>
    public async Task RecusaCursorAdulteradoAsync(string consulta)
    {
        string proximo = await _consulta.ProximoAsync(consulta);

        await ConferirAsync(proximo, static p => p with { SortKey = null }, RecusaDeCursor.Invalida);
        await ConferirAsync(proximo, static p => p with { ResourceTag = ColecaoDeOutroRecurso }, RecusaDeCursor.Invalida);
        await ConferirAsync(proximo, static p => p with { SortKey = "chave sem forma" }, RecusaDeCursor.DeOutraConsulta);
        await ConferirAsync(proximo, ComOResumoDeOutraConsulta, RecusaDeCursor.DeOutraConsulta);
    }

    /// <summary>
    /// Troca a coluna de ordenação do ponto de continuação por um código que o contrato não publica, mantendo o resumo da
    /// consulta: a recusa vem da leitura da coluna, não da conferência do resumo.
    /// </summary>
    public async Task RecusaColunaComCodigoDesconhecidoAsync(string consulta, int coluna, string codigo)
    {
        string proximo = await _consulta.ProximoAsync(consulta);

        await ConferirAsync(proximo, payload => ComAColuna(payload, coluna, codigo), RecusaDeCursor.DeOutraConsulta);
    }

    /// <summary>
    /// Mantém o resumo e o instante, mas leva uma coluna de ordenação a mais do que a listagem lê: a recusa vem da forma
    /// da chave, não da conferência do resumo. Para as listagens ordenadas só pelo <c>Id</c>, que não têm coluna.
    /// </summary>
    public async Task RecusaChaveComColunaAMaisAsync(string consulta)
    {
        string proximo = await _consulta.ProximoAsync(consulta);

        await ConferirAsync(proximo, ComColunaAMais, RecusaDeCursor.DeOutraConsulta);
    }

    public async Task RecusaCursorVencidoAsync(string consulta)
    {
        string proximo = await _consulta.ProximoAsync(consulta);

        await ConferirAsync(proximo, static p => p with { ExpiresAt = DateTimeOffset.UnixEpoch }, RecusaDeCursor.Expirada);
    }

    /// <summary>O link <c>prev</c> volta com a mesma consulta e é recusado com outra.</summary>
    public async Task ConfereOLinkAnteriorAsync(string consulta, Func<string, string> alteracao)
    {
        ArgumentNullException.ThrowIfNull(alteracao);

        string anterior = await _consulta.AnteriorDaSegundaAsync(consulta);

        using HttpResponseMessage mesma = await _consulta.ObterAsync(anterior);
        using HttpResponseMessage outra = await _consulta.ObterAsync(alteracao(anterior));

        await RespostaDaListagem.ConferirStatusAsync(mesma, HttpStatusCode.OK);
        await RecusaDeCursor.DeOutraConsulta.ConferirAsync(outra);
    }

    /// <summary>
    /// O payload decifrado inteiro não carrega nenhum dos textos; o resumo da conta é de 43 caracteres quando a listagem
    /// vincula o cursor à conta, e nulo quando não vincula.
    /// </summary>
    public async Task ConferePayloadAsync(string consulta, bool vinculadoAConta, params string[] textosProibidos)
    {
        ArgumentNullException.ThrowIfNull(textosProibidos);

        ConteudoDoCursor conteudo = await _cursores.DecifrarAsync(await _consulta.ProximoAsync(consulta));

        conteudo.Resumo.Should().HaveLength(TamanhoDoResumo);
        ConferirOResumoDaConta(conteudo, vinculadoAConta);
        foreach (string texto in textosProibidos)
        {
            conteudo.Texto.Should().NotContainEquivalentOf(texto);
        }
    }

    /// <summary>O percurso inteiro com a consulta alterada a cada continuação traz os mesmos itens, sem pular nem repetir.</summary>
    public async Task ConfereOPercursoEquivalenteAsync(string consulta, int limite, Func<string, string> alteracao)
    {
        ArgumentNullException.ThrowIfNull(alteracao);

        IReadOnlyList<Guid> direto = await _consulta.PercorrerAsync(consulta, limite);
        IReadOnlyList<Guid> equivalente = await _consulta.PercorrerAsync(consulta, limite, alteracao);

        direto.Should().HaveCountGreaterThan(limite).And.OnlyHaveUniqueItems();
        equivalente.Should().Equal(direto);
    }

    private static void ConferirOResumoDaConta(ConteudoDoCursor conteudo, bool vinculadoAConta)
    {
        if (vinculadoAConta)
        {
            conteudo.Payload.ResumoDaConta.Should().HaveLength(TamanhoDoResumo);
        }
        else
        {
            conteudo.Payload.ResumoDaConta.Should().BeNull();
        }
    }

    private async Task ConferirAsync(string proximo, Func<CursorPayload, CursorPayload> adulteracao, RecusaDeCursor esperada)
    {
        string adulterado = await _cursores.AlterarAsync(proximo, adulteracao);

        using HttpResponseMessage resposta = await _consulta.ObterAsync(adulterado);

        await esperada.ConferirAsync(resposta);
    }

    private static CursorPayload ComAColuna(CursorPayload payload, int coluna, string codigo)
    {
        ConteudoDoCursor conteudo = ConteudoDoCursor.De(payload);
        string[] colunas = [.. conteudo.Colunas];
        colunas[coluna] = codigo;

        return payload with { SortKey = CompositeSortKey.Serialize([conteudo.Resumo, conteudo.InstanteCongelado, .. colunas]) };
    }

    private static CursorPayload ComColunaAMais(CursorPayload payload)
    {
        ConteudoDoCursor conteudo = ConteudoDoCursor.De(payload);

        return payload with { SortKey = CompositeSortKey.Serialize([conteudo.Resumo, conteudo.InstanteCongelado, .. conteudo.Colunas, "coluna-a-mais"]) };
    }

    // Mantém o instante e as colunas do item de fronteira: só o resumo da consulta é outro.
    private static CursorPayload ComOResumoDeOutraConsulta(CursorPayload payload)
    {
        ConteudoDoCursor conteudo = ConteudoDoCursor.De(payload);

        return payload with { SortKey = CompositeSortKey.Serialize([ResumoDeOutraConsulta, conteudo.InstanteCongelado, .. conteudo.Colunas]) };
    }
}
