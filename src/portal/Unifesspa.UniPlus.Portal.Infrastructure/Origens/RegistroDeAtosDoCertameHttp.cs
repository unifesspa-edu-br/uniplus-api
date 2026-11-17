namespace Unifesspa.UniPlus.Portal.Infrastructure.Origens;

using System.Net.Http.Headers;
using System.Web;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Refit;

using Unifesspa.UniPlus.Portal.Application.Certames.Origens;

/// <summary>
/// Percorre as páginas da consulta unificada de atos de Publicações e entrega os atos de um
/// certame na ordem da origem, com o que impediu a leitura de terminar.
/// </summary>
internal sealed partial class RegistroDeAtosDoCertameHttp : IRegistroDeAtosDoCertame
{
    private readonly IPublicacoesAtosApi _api;
    private readonly OrigensOptions _opcoes;
    private readonly ILogger<RegistroDeAtosDoCertameHttp> _logger;

    public RegistroDeAtosDoCertameHttp(
        IPublicacoesAtosApi api,
        IOptions<OrigensOptions> opcoes,
        ILogger<RegistroDeAtosDoCertameHttp> logger)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);

        _api = api;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    public async Task<LeituraDosAtos> LerAtosAsync(Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        List<AtoDaOrigem> atos = [];
        PaginaDeAtosLida pagina;
        string? cursor = null;
        int numero = 0;

        do
        {
            pagina = await LerPaginaAsync(processoSeletivoId, cursor, ++numero, cancellationToken).ConfigureAwait(false);
            atos.AddRange(pagina.Atos);
            cursor = pagina.ProximoCursor;
        }
        while (!pagina.Terminou);

        return new LeituraDosAtos(atos, pagina.Falha);
    }

    /// <summary>
    /// A falha de envio chega dentro da resposta, sem exceção (ver <c>ContratoPublicoDoCertameHttp</c>);
    /// só o cancelamento pedido por quem chama continua saindo como exceção.
    /// </summary>
    private async Task<PaginaDeAtosLida> LerPaginaAsync(
        Guid processoSeletivoId, string? cursor, int numero, CancellationToken cancellationToken)
    {
        IApiResponse<List<AtoNormativoPayload>> resposta = await _api
            .ListarAsync(processoSeletivoId, cursor, cursor is null ? _opcoes.ItensPorPaginaDeAtos : null, cancellationToken)
            .ConfigureAwait(false);

        using (resposta)
        {
            return resposta is { IsSuccessStatusCode: true, Content: { } conteudo, Headers: { } cabecalhos }
                ? DaResposta(conteudo, cabecalhos, numero)
                : PaginaDeAtosLida.Interrompida([], FalhaDaResposta(resposta, numero));
        }
    }

    /// <summary>
    /// Inclui o cursor recusado no meio do caminho (expirado, por exemplo) — a origem não tem mais
    /// como continuar a partir dele — e o corpo fora do contrato.
    /// </summary>
    private FalhaNaLeituraDosAtos FalhaDaResposta(IApiResponse<List<AtoNormativoPayload>> resposta, int numero) =>
        resposta.StatusCode is { } status
            ? RespostaInesperada((int)status, numero)
            : SemResposta(resposta.Error, numero);

    private FalhaNaLeituraDosAtos SemResposta(Exception? erro, int numero)
    {
        LogOrigemSemResposta(_logger, numero, erro);

        return FalhaDaPagina(numero);
    }

    private FalhaNaLeituraDosAtos RespostaInesperada(int status, int numero)
    {
        LogRespostaInesperada(_logger, numero, status);

        return FalhaDaPagina(numero);
    }

    /// <summary>
    /// Uma falha na primeira página é a origem fora; depois dela, é o percurso que se interrompeu —
    /// e a página já tem parte da linha do tempo para mostrar.
    /// </summary>
    private static FalhaNaLeituraDosAtos FalhaDaPagina(int numero) =>
        numero == 1 ? FalhaNaLeituraDosAtos.OrigemIndisponivel : FalhaNaLeituraDosAtos.FalhaNaPaginacao;

    private PaginaDeAtosLida DaResposta(List<AtoNormativoPayload> pagina, HttpResponseHeaders cabecalhos, int numero)
    {
        (List<AtoDaOrigem> aceitos, bool foraDoContrato) = AtosAceitos(pagina, numero);

        return foraDoContrato
            ? PaginaDeAtosLida.Interrompida(aceitos, FalhaDaPagina(numero))
            : ProximaPagina(aceitos, cabecalhos, numero);
    }

    private (List<AtoDaOrigem> Aceitos, bool ForaDoContrato) AtosAceitos(List<AtoNormativoPayload> pagina, int numero)
    {
        List<AtoDaOrigem> aceitos = [.. pagina.Select(AtoDaOrigemDe).TakeWhile(static ato => ato is not null).OfType<AtoDaOrigem>()];
        bool foraDoContrato = aceitos.Count < pagina.Count;

        if (foraDoContrato)
        {
            LogAtoForaDoContrato(_logger, numero);
        }

        return (aceitos, foraDoContrato);
    }

    /// <summary>
    /// A anotação de nulidade não alcança os elementos da lista desserializada: um ato nulo na página
    /// é ato fora do contrato, como o ato sem id ou sem tipo.
    /// </summary>
    private static AtoDaOrigem? AtoDaOrigemDe(AtoNormativoPayload? ato) =>
        ato is { Id: var id, TipoCodigo: { } tipoCodigo }
        && id != Guid.Empty
        && !string.IsNullOrWhiteSpace(tipoCodigo)
            ? new AtoDaOrigem(
                id,
                tipoCodigo,
                ato.TipoNome,
                ato.Ano,
                ato.Numero,
                ato.DataDoAto,
                ato.CongelaConfiguracao,
                ato.MotivoRetificacao)
            : null;

    private PaginaDeAtosLida ProximaPagina(List<AtoDaOrigem> atos, HttpResponseHeaders cabecalhos, int numero)
    {
        if (!HaProximaPagina(cabecalhos, out string? cursor))
        {
            return PaginaDeAtosLida.Final(atos);
        }

        return cursor is null
            ? CursorIlegivel(atos, numero)
            : ContinuaOuAtingeOLimite(atos, cursor, numero);
    }

    /// <summary>
    /// A origem anuncia uma próxima página que não há como pedir. Encerrar aqui como se a lista
    /// tivesse acabado omitiria os atos mais recentes em silêncio.
    /// </summary>
    private PaginaDeAtosLida CursorIlegivel(List<AtoDaOrigem> atos, int numero)
    {
        LogProximaPaginaIlegivel(_logger, numero);

        return PaginaDeAtosLida.Interrompida(atos, FalhaNaLeituraDosAtos.FalhaNaPaginacao);
    }

    private PaginaDeAtosLida ContinuaOuAtingeOLimite(List<AtoDaOrigem> atos, string cursor, int numero) =>
        numero >= _opcoes.LimiteDePaginasDeAtos
            ? PaginaDeAtosLida.Interrompida(atos, FalhaNaLeituraDosAtos.LimiteDePaginas)
            : PaginaDeAtosLida.Continua(atos, cursor);

    /// <summary>
    /// Diz se a resposta anuncia uma próxima página (link <c>rel="next"</c>) e extrai o cursor
    /// dela; o cursor sai nulo quando o link existe mas não há como lê-lo.
    /// </summary>
    /// <remarks>
    /// Só o cursor é aproveitado; o endereço do link não é seguido. A origem monta o link com o
    /// host que ela enxerga, que atrás de proxy não é o endereço configurado aqui — e seguir um
    /// endereço ditado pela resposta mandaria a próxima requisição para um destino que ninguém
    /// configurou, pelo mesmo motivo que o cliente não segue redirecionamento.
    /// </remarks>
    internal static bool HaProximaPagina(HttpResponseHeaders cabecalhos, out string? cursor)
    {
        string? trecho = LinksDoCabecalho(cabecalhos).FirstOrDefault(static link => link.Contains("rel=\"next\"", StringComparison.Ordinal));
        cursor = trecho is null ? null : CursorDoLink(trecho);

        return trecho is not null;
    }

    private static IEnumerable<string> LinksDoCabecalho(HttpResponseHeaders cabecalhos) =>
        cabecalhos.TryGetValues("Link", out IEnumerable<string>? valores)
            ? valores.SelectMany(static valor => valor.Split(',')).Select(static parte => parte.Trim())
            : [];

    private static string? CursorDoLink(string trecho)
    {
        int inicio = trecho.IndexOf('<', StringComparison.Ordinal);
        int fim = trecho.IndexOf('>', StringComparison.Ordinal);

        if (inicio < 0 || fim <= inicio || !Uri.TryCreate(trecho[(inicio + 1)..fim], UriKind.Absolute, out Uri? endereco))
        {
            return null;
        }

        string? proximo = HttpUtility.ParseQueryString(endereco.Query)["cursor"];

        return string.IsNullOrEmpty(proximo) ? null : proximo;
    }

    private readonly record struct PaginaDeAtosLida(
        IReadOnlyList<AtoDaOrigem> Atos,
        FalhaNaLeituraDosAtos? Falha,
        string? ProximoCursor)
    {
        public bool Terminou => ProximoCursor is null;

        public static PaginaDeAtosLida Final(IReadOnlyList<AtoDaOrigem> atos) => new(atos, null, null);

        public static PaginaDeAtosLida Interrompida(IReadOnlyList<AtoDaOrigem> atos, FalhaNaLeituraDosAtos falha) =>
            new(atos, falha, null);

        public static PaginaDeAtosLida Continua(IReadOnlyList<AtoDaOrigem> atos, string cursor) => new(atos, null, cursor);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publicações não respondeu à página {Pagina} da consulta de atos do certame.")]
    private static partial void LogOrigemSemResposta(ILogger logger, int pagina, Exception? excecao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publicações respondeu {Status} à página {Pagina} da consulta de atos do certame.")]
    private static partial void LogRespostaInesperada(ILogger logger, int pagina, int status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Publicações anunciou, depois da página {Pagina}, uma próxima página sem cursor legível.")]
    private static partial void LogProximaPaginaIlegivel(ILogger logger, int pagina);

    [LoggerMessage(Level = LogLevel.Error, Message = "Publicações devolveu, na página {Pagina}, ato sem os campos obrigatórios do contrato.")]
    private static partial void LogAtoForaDoContrato(ILogger logger, int pagina);
}
