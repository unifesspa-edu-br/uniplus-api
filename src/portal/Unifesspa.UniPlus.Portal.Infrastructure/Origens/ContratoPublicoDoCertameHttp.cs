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

    /// <summary>
    /// Com a resposta pedida como <c>IApiResponse</c>, a falha de envio — rede, tempo esgotado,
    /// interrupção por falhas — chega dentro da resposta, sem exceção, e cai no "sem resposta": é por
    /// isso que a interrupção por falhas não escapa até o tratador global como erro inesperado. Só o
    /// cancelamento pedido por quem chama continua saindo como exceção.
    /// </summary>
    private async Task<ConsultaDoCertame> ConsultarAsync(
        Func<Task<IApiResponse<CertamePublicadoPayload>>> consultar,
        CancellationToken cancellationToken)
    {
        IApiResponse<CertamePublicadoPayload> resposta = await consultar().ConfigureAwait(false);

        using (resposta)
        {
            return FalhaDaConsulta(resposta) ?? DoConteudo(resposta.Content);
        }
    }

    private ConsultaDoCertame? FalhaDaConsulta(IApiResponse<CertamePublicadoPayload> resposta) => resposta.StatusCode switch
    {
        HttpStatusCode.NotFound => new ConsultaDoCertame.NaoEncontrado(),
        null => IndisponivelSemResposta(resposta.Error),
        _ when !resposta.IsSuccessStatusCode => IndisponivelPorStatus((int)resposta.StatusCode!.Value),
        _ => null,
    };

    private ConsultaDoCertame.Indisponivel IndisponivelSemResposta(Exception? erro)
    {
        LogOrigemSemResposta(_logger, erro);

        return new ConsultaDoCertame.Indisponivel();
    }

    private ConsultaDoCertame.Indisponivel IndisponivelPorStatus(int status)
    {
        LogRespostaInesperada(_logger, status);

        return new ConsultaDoCertame.Indisponivel();
    }

    /// <summary>
    /// Um 200 sem os campos que a página exige não é certame: é contrato quebrado, e servi-lo
    /// mostraria ao candidato uma página sem título ou sem linha do tempo verificável.
    /// </summary>
    private ConsultaDoCertame DoConteudo(CertamePublicadoPayload? conteudo)
    {
        if (CertameDaOrigemDe(conteudo) is { } certame)
        {
            return new ConsultaDoCertame.Encontrado(certame);
        }

        LogCorpoForaDoContrato(_logger);

        return new ConsultaDoCertame.Indisponivel();
    }

    private static CertameDaOrigem? CertameDaOrigemDe(CertamePublicadoPayload? certame) =>
        certame is { Nome: { } nome, Situacao: { Valor: { } situacao } }
        && certame.ProcessoSeletivoId != Guid.Empty
        && certame.AtoCriadorId != Guid.Empty
        && !string.IsNullOrWhiteSpace(nome)
        && DocumentosPorAto(certame.EditaisPublicados) is { } documentosPorAto
            ? new CertameDaOrigem(
                certame.ProcessoSeletivoId,
                certame.AtoCriadorId,
                nome,
                NumeroDoEdital(certame.Periodo),
                DocumentoPublico(certame.DocumentoEdital?.UrlDownload),
                documentosPorAto,
                situacao)
            : null;

    private static string? NumeroDoEdital(PeriodoDoCertamePayload? periodo) =>
        string.IsNullOrWhiteSpace(periodo?.Numero) ? null : periodo.Numero;

    /// <summary>
    /// Os documentos de cada ato divulgado do processo, pelo ato: o edital de cada versão e os atos que
    /// não criam versão. Edital sem a lista de documentos, que o contrato exige, deixa o certame fora do
    /// contrato (resultado nulo); a lista de editais ausente é válida e não tem documento algum. O edital sem identificador do ato
    /// também tem a lista conferida, mas não entra no resultado: não há a quem associar os documentos.
    /// </summary>
    private static Dictionary<Guid, IReadOnlyList<DocumentoDoEvento>>? DocumentosPorAto(
        IReadOnlyList<EditalPublicadoDoCertamePayload?>? editais)
    {
        Dictionary<Guid, IReadOnlyList<DocumentoDoEvento>> porAto = [];
        foreach (EditalPublicadoDoCertamePayload edital in (editais ?? []).OfType<EditalPublicadoDoCertamePayload>())
        {
            if (DocumentosDoEdital(edital) is not { } documentos)
            {
                return null;
            }

            porAto[edital.AtoId] = documentos;
        }

        porAto.Remove(Guid.Empty);

        return porAto;
    }

    private static IReadOnlyList<DocumentoDoEvento>? DocumentosDoEdital(EditalPublicadoDoCertamePayload edital) =>
        edital.Documentos is { } documentos ? [.. documentos.Select(ComoDocumento).OfType<DocumentoDoEvento>()] : null;

    /// <summary>
    /// Documento sem nome, sem tipo de mídia, com tamanho negativo, papel desconhecido ou endereço
    /// fora do padrão aceito é ignorado, como o link do edital: o resto do evento segue servido.
    /// </summary>
    private static DocumentoDoEvento? ComoDocumento(DocumentoDoAtoPayload? documento) =>
        documento is { Nome: { Length: > 0 } nome, TipoDeMidia: { Length: > 0 } tipoDeMidia, TamanhoBytes: >= 0 and { } tamanho }
        && PapelDe(documento.Papel) is { } papel
        && DocumentoPublico(documento.UrlDownload) is { } url
            ? new DocumentoDoEvento(papel, nome, tipoDeMidia, tamanho, url)
            : null;

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
