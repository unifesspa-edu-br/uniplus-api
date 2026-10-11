namespace Unifesspa.UniPlus.Portal.Infrastructure.Origens;

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
        string? cursor = null;
        int paginasLidas = 0;

        while (true)
        {
            // Uma falha na primeira página é a origem fora; depois dela, é o percurso que se
            // interrompeu — e a página já tem parte da linha do tempo para mostrar.
            FalhaNaLeituraDosAtos falhaDestaPagina = paginasLidas == 0
                ? FalhaNaLeituraDosAtos.OrigemIndisponivel
                : FalhaNaLeituraDosAtos.FalhaNaPaginacao;

            // A falha de envio chega dentro da resposta, sem exceção (ver ContratoPublicoDoCertameHttp);
            // só o cancelamento pedido por quem chama continua saindo como exceção.
            IApiResponse<List<AtoNormativoPayload>> resposta = await _api
                .ListarAsync(
                    processoSeletivoId,
                    cursor,
                    cursor is null ? _opcoes.ItensPorPaginaDeAtos : null,
                    cancellationToken)
                .ConfigureAwait(false);

            using (resposta)
            {
                if (resposta.StatusCode is null)
                {
                    LogOrigemSemResposta(_logger, paginasLidas + 1, resposta.Error);
                    return new LeituraDosAtos(atos, falhaDestaPagina);
                }

                if (!resposta.IsSuccessStatusCode || resposta.Content is not { } pagina)
                {
                    // Inclui o cursor recusado no meio do caminho (expirado, por exemplo) — a origem
                    // não tem mais como continuar a partir dele — e o corpo fora do contrato.
                    LogRespostaInesperada(_logger, paginasLidas + 1, (int)resposta.StatusCode);
                    return new LeituraDosAtos(atos, falhaDestaPagina);
                }

                foreach (AtoNormativoPayload ato in pagina)
                {
                    if (ato.Id == Guid.Empty || string.IsNullOrWhiteSpace(ato.TipoCodigo))
                    {
                        LogAtoForaDoContrato(_logger, paginasLidas + 1);
                        return new LeituraDosAtos(atos, falhaDestaPagina);
                    }

                    atos.Add(new AtoDaOrigem(
                        ato.Id,
                        ato.TipoCodigo,
                        ato.TipoNome,
                        ato.Ano,
                        ato.Numero,
                        ato.DataPublicacao,
                        ato.CongelaConfiguracao,
                        ato.MotivoRetificacao));
                }

                paginasLidas++;

                if (!HaProximaPagina(resposta.Headers, out cursor))
                {
                    return new LeituraDosAtos(atos, null);
                }

                if (cursor is null)
                {
                    // A origem anuncia uma próxima página que não há como pedir. Encerrar aqui como
                    // se a lista tivesse acabado cortaria os atos mais recentes em silêncio.
                    LogProximaPaginaIlegivel(_logger, paginasLidas);
                    return new LeituraDosAtos(atos, FalhaNaLeituraDosAtos.FalhaNaPaginacao);
                }
            }

            if (paginasLidas >= _opcoes.TetoDePaginasDeAtos)
            {
                return new LeituraDosAtos(atos, FalhaNaLeituraDosAtos.TetoDePaginas);
            }
        }
    }

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
    internal static bool HaProximaPagina(System.Net.Http.Headers.HttpResponseHeaders cabecalhos, out string? cursor)
    {
        cursor = null;

        if (!cabecalhos.TryGetValues("Link", out IEnumerable<string>? valores))
        {
            return false;
        }

        foreach (string valor in valores)
        {
            foreach (string parte in valor.Split(','))
            {
                string trecho = parte.Trim();
                if (!trecho.Contains("rel=\"next\"", StringComparison.Ordinal))
                {
                    continue;
                }

                int inicio = trecho.IndexOf('<', StringComparison.Ordinal);
                int fim = trecho.IndexOf('>', StringComparison.Ordinal);
                if (inicio >= 0 && fim > inicio
                    && Uri.TryCreate(trecho[(inicio + 1)..fim], UriKind.Absolute, out Uri? endereco))
                {
                    string? proximo = HttpUtility.ParseQueryString(endereco.Query)["cursor"];
                    cursor = string.IsNullOrEmpty(proximo) ? null : proximo;
                }

                return true;
            }
        }

        return false;
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
