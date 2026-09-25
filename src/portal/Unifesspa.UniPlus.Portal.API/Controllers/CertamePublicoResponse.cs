namespace Unifesspa.UniPlus.Portal.API.Controllers;

using Unifesspa.UniPlus.Portal.Application.Certames;

/// <summary>
/// A página pública do certame: os dados publicados por Seleção e a linha do tempo de atos de
/// Publicações, prontos para a tela do Portal do Candidato.
/// </summary>
/// <param name="Identificador">Identificador legível do certame, o mesmo do endereço da página.</param>
/// <param name="Nome">Título do certame congelado na publicação.</param>
/// <param name="NumeroEdital">Número do edital como declarado na publicação, quando declarado.</param>
/// <param name="DocumentoEditalUrl">
/// Endereço público do edital no acervo. Ausente enquanto o documento não estiver disponível no
/// acervo — é pendência de processamento, nunca erro. O documento não trafega pela Portal.
/// </param>
/// <param name="LinhaDoTempo">Atos do certame, do mais recente para o mais antigo.</param>
/// <param name="Aviso">
/// Texto neutro presente quando a linha do tempo está temporariamente vazia ou incompleta; ausente
/// quando ela está completa.
/// </param>
internal sealed record CertamePublicoResponse(
    string Identificador,
    string Nome,
    string? NumeroEdital,
    string? DocumentoEditalUrl,
    IReadOnlyList<EventoDaLinhaDoTempoResponse> LinhaDoTempo,
    string? Aviso)
{
    /// <summary>
    /// Texto do aviso de linha do tempo degradada. Um só texto para todos os motivos: a página não
    /// explica ao público o estado interno das origens.
    /// </summary>
    internal const string AvisoDeLinhaDoTempoIncompleta =
        "A lista de publicações deste processo seletivo pode estar incompleta no momento. "
        + "Tente novamente em alguns minutos.";

    internal static CertamePublicoResponse De(string identificador, PaginaDoCertame pagina) =>
        new(
            identificador,
            pagina.Nome,
            pagina.NumeroEdital,
            DocumentoEditalUrl: null,
            [.. pagina.LinhaDoTempo.Select(EventoDaLinhaDoTempoResponse.De)],
            pagina.Degradacao is null ? null : AvisoDeLinhaDoTempoIncompleta);
}

/// <summary>
/// Um ato na linha do tempo da página pública.
/// </summary>
/// <param name="Id">Identificador do ato.</param>
/// <param name="Data">Data de publicação declarada no documento do ato.</param>
/// <param name="Titulo">Nome do tipo do ato seguido do número, quando declarado, sem compor o ano.</param>
/// <param name="Numero">Número do ato como declarado pelo órgão. Ausente quando o ato não declara número.</param>
/// <param name="Ano">Ano da numeração declarada pelo órgão (não é o ano da data de publicação).</param>
/// <param name="Tipo">Tipo do ato.</param>
/// <param name="Descricao">Motivo da retificação, quando o ato retifica outro.</param>
/// <param name="DocumentoUrl">
/// Endereço público do documento do ato no acervo. Ausente enquanto o acervo não tiver o documento.
/// </param>
internal sealed record EventoDaLinhaDoTempoResponse(
    Guid Id,
    DateOnly Data,
    string Titulo,
    string? Numero,
    int Ano,
    TipoDoEventoResponse Tipo,
    string? Descricao,
    string? DocumentoUrl)
{
    internal static EventoDaLinhaDoTempoResponse De(EventoDaLinhaDoTempo evento) =>
        new(
            evento.Id,
            evento.Data,
            evento.Titulo,
            evento.Numero,
            evento.Ano,
            new TipoDoEventoResponse(evento.Tipo.Codigo, evento.Tipo.Nome),
            evento.Descricao,
            DocumentoUrl: null);
}

/// <summary>
/// Tipo de um ato da linha do tempo.
/// </summary>
/// <param name="Codigo">Código do tipo do ato.</param>
/// <param name="Nome">Nome do tipo copiado no registro do ato, ou o código quando o ato não o tem.</param>
internal sealed record TipoDoEventoResponse(string Codigo, string Nome);
