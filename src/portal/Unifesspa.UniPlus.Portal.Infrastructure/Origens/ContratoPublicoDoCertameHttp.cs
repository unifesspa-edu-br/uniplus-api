namespace Unifesspa.UniPlus.Portal.Infrastructure.Origens;

using System.Net;

using Microsoft.Extensions.Logging;

using Refit;

using Unifesspa.UniPlus.Portal.Application.Certames;
using Unifesspa.UniPlus.Portal.Application.Certames.Origens;

/// <summary>
/// Lê o detalhe público do certame em Seleção e traduz a resposta nos três desfechos que a
/// composição distingue. Nenhuma falha de transporte escapa como exceção.
/// </summary>
internal sealed partial class ContratoPublicoDoCertameHttp : IContratoPublicoDoCertame
{
    private readonly ISelecaoCertamePublicoApi _api;
    private readonly ILogger<ContratoPublicoDoCertameHttp> _logger;

    public ContratoPublicoDoCertameHttp(ISelecaoCertamePublicoApi api, ILogger<ContratoPublicoDoCertameHttp> logger)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(logger);

        _api = api;
        _logger = logger;
    }

    public Task<ConsultaDoCertame> ObterPorIdentificadorAsync(string identificador, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identificador);

        return ConsultarAsync(() => _api.ObterPorIdentificadorAsync(identificador, cancellationToken), cancellationToken);
    }

    private async Task<ConsultaDoCertame> ConsultarAsync(
        Func<Task<IApiResponse<CertamePublicadoPayload>>> consultar,
        CancellationToken cancellationToken)
    {
        // Com a resposta pedida como IApiResponse, a falha de envio — rede, tempo esgotado,
        // circuito aberto — chega dentro da resposta, sem exceção, e cai no "sem resposta" abaixo:
        // é por isso que o circuito aberto não escapa até o tratador global como erro inesperado.
        // Só o cancelamento pedido por quem chama continua saindo como exceção.
        IApiResponse<CertamePublicadoPayload> resposta = await consultar().ConfigureAwait(false);

        using (resposta)
        {
            if (resposta.StatusCode == HttpStatusCode.NotFound)
            {
                return new ConsultaDoCertame.NaoEncontrado();
            }

            if (resposta.StatusCode is null)
            {
                LogOrigemSemResposta(_logger, resposta.Error);
                return new ConsultaDoCertame.Indisponivel();
            }

            if (!resposta.IsSuccessStatusCode)
            {
                LogRespostaInesperada(_logger, (int)resposta.StatusCode);
                return new ConsultaDoCertame.Indisponivel();
            }

            if (resposta.Content is not { } certame
                || certame.ProcessoSeletivoId == Guid.Empty
                || certame.AtoCriadorId == Guid.Empty
                || string.IsNullOrWhiteSpace(certame.Nome)
                || certame.Situacao is not { } situacao)
            {
                // Um 200 sem os campos que a página exige não é certame: é contrato quebrado, e
                // servi-lo mostraria ao candidato uma página sem título ou sem linha do tempo
                // verificável.
                LogCorpoForaDoContrato(_logger);
                return new ConsultaDoCertame.Indisponivel();
            }

            return new ConsultaDoCertame.Encontrado(new CertameDaOrigem(
                certame.ProcessoSeletivoId,
                certame.AtoCriadorId,
                certame.Nome,
                string.IsNullOrWhiteSpace(certame.Periodo?.Numero) ? null : certame.Periodo.Numero,
                DocumentoPublico(certame.DocumentoEdital?.UrlDownload),
                DocumentosPorAto(certame.EditaisPublicados),
                situacao));
        }
    }

    /// <summary>
    /// Os documentos de cada ato divulgado do processo, pelo ato: o edital de cada versão e os atos que
    /// não criam versão. Documento sem nome, sem tipo de
    /// mídia, com tamanho negativo, papel desconhecido ou endereço fora do padrão aceito é
    /// ignorado, como o link do edital: o resto do evento segue servido.
    /// </summary>
    private static Dictionary<Guid, IReadOnlyList<DocumentoDoEvento>> DocumentosPorAto(
        IReadOnlyList<EditalPublicadoDoCertamePayload?>? editais)
    {
        Dictionary<Guid, IReadOnlyList<DocumentoDoEvento>> porAto = [];
        foreach (EditalPublicadoDoCertamePayload? edital in editais ?? [])
        {
            if (edital is null || edital.AtoId == Guid.Empty)
            {
                continue;
            }

            List<DocumentoDoEvento> documentos = [];
            foreach (DocumentoDoAtoPayload? documento in edital.Documentos ?? [])
            {
                if (documento is { Nome: { Length: > 0 } nome, TipoDeMidia: { Length: > 0 } tipoDeMidia, TamanhoBytes: >= 0 and { } tamanho }
                    && PapelDe(documento.Papel) is { } papel
                    && DocumentoPublico(documento.UrlDownload) is { } url)
                {
                    documentos.Add(new DocumentoDoEvento(papel, nome, tipoDeMidia, tamanho, url));
                }
            }

            porAto[edital.AtoId] = documentos;
        }

        return porAto;
    }

    private static PapelDoDocumento? PapelDe(string? papel) => papel switch
    {
        "principal" => PapelDoDocumento.Principal,
        "anexo" => PapelDoDocumento.Anexo,
        _ => null,
    };

    /// <summary>
    /// Só endereço absoluto de leitura pública chega à página: qualquer outra forma é tratada como
    /// documento ainda indisponível, e não vira link.
    /// </summary>
    private static Uri? DocumentoPublico(Uri? endereco) =>
        endereco is { IsAbsoluteUri: true } && (endereco.Scheme == Uri.UriSchemeHttps || endereco.Scheme == Uri.UriSchemeHttp)
            ? endereco
            : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seleção não respondeu à consulta do detalhe público do certame.")]
    private static partial void LogOrigemSemResposta(ILogger logger, Exception? excecao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seleção respondeu {Status} à consulta do detalhe público do certame.")]
    private static partial void LogRespostaInesperada(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Error, Message = "Seleção respondeu o detalhe público do certame sem os campos obrigatórios do contrato.")]
    private static partial void LogCorpoForaDoContrato(ILogger logger);
}
