namespace Unifesspa.UniPlus.Portal.Application.Certames;

/// <summary>
/// A página pública do certame composta pela Portal: os dados publicados por Seleção e a linha
/// do tempo de atos de Publicações.
/// </summary>
/// <param name="ProcessoSeletivoId">Identificador do processo seletivo.</param>
/// <param name="Nome">Título do certame congelado na publicação.</param>
/// <param name="NumeroEdital">Número do edital, quando declarado na publicação.</param>
/// <param name="Situacao">Situação da janela de inscrição, como Seleção a resolveu na leitura.</param>
/// <param name="DocumentoEditalUrl">Endereço público do edital vigente, quando publicado.</param>
/// <param name="DataPublicacao">
/// Data de publicação do edital vigente — a do ato que criou a versão servida. Nula quando a linha
/// do tempo não alcança esse ato.
/// </param>
/// <param name="LinhaDoTempo">Atos do certame, do mais recente para o mais antigo.</param>
/// <param name="Degradacao">
/// Por que a linha do tempo está vazia ou incompleta; nulo quando ela está completa e consistente
/// com o detalhe servido.
/// </param>
public sealed record PaginaDoCertame(
    Guid ProcessoSeletivoId,
    string Nome,
    string? NumeroEdital,
    SituacaoDaInscricao Situacao,
    Uri? DocumentoEditalUrl,
    DateOnly? DataPublicacao,
    IReadOnlyList<EventoDaLinhaDoTempo> LinhaDoTempo,
    MotivoDaDegradacao? Degradacao);

/// <summary>
/// Um ato na linha do tempo da página pública.
/// </summary>
/// <param name="Id">Identificador do ato.</param>
/// <param name="Data">Data de publicação declarada no documento do ato.</param>
/// <param name="Titulo">Nome do tipo do ato seguido do número, quando declarado, sem compor o ano.</param>
/// <param name="Numero">Número do ato como declarado pelo órgão, quando declarado.</param>
/// <param name="Ano">Ano da numeração declarada pelo órgão.</param>
/// <param name="Tipo">Tipo do ato.</param>
/// <param name="Descricao">Motivo da retificação, quando o ato retifica outro.</param>
/// <param name="DocumentoUrl">
/// Endereço público do documento principal do ato, quando o acervo o tem. Hoje só o ato que criou a
/// versão servida tem: é o edital que Seleção publica no acervo. É o mesmo endereço do documento de
/// papel principal em <paramref name="Documentos"/>.
/// </param>
/// <param name="Documentos">
/// Os documentos do ato no acervo — o principal primeiro e os anexos em seguida. Vazia quando o
/// acervo não tem documento do ato.
/// </param>
public sealed record EventoDaLinhaDoTempo(
    Guid Id,
    DateOnly Data,
    string Titulo,
    string? Numero,
    int Ano,
    TipoDoEvento Tipo,
    string? Descricao,
    Uri? DocumentoUrl,
    IReadOnlyList<DocumentoDoEvento> Documentos);

/// <summary>Papel de um documento no ato que o publica.</summary>
public enum PapelDoDocumento
{
    /// <summary>O documento que é o próprio ato — o edital.</summary>
    Principal,

    /// <summary>O documento que acompanha o ato.</summary>
    Anexo,
}

/// <summary>
/// Um documento de um ato no acervo público. É o formato único de documento de ato da página,
/// do edital e dos anexos dele aos atos avulsos.
/// </summary>
/// <param name="Papel">Se o documento é o próprio ato ou o acompanha.</param>
/// <param name="Nome">Nome com que o arquivo é salvo por quem o baixa.</param>
/// <param name="TipoDeMidia">Tipo de mídia do arquivo.</param>
/// <param name="TamanhoBytes">Tamanho do arquivo, em bytes.</param>
/// <param name="Url">Endereço público do arquivo no acervo.</param>
public sealed record DocumentoDoEvento(
    PapelDoDocumento Papel,
    string Nome,
    string TipoDeMidia,
    long TamanhoBytes,
    Uri Url);

/// <summary>
/// Tipo de um ato da linha do tempo.
/// </summary>
/// <param name="Codigo">Código do tipo do ato.</param>
/// <param name="Nome">Nome do tipo copiado no registro do ato, ou o código quando o ato não o tem.</param>
public sealed record TipoDoEvento(string Codigo, string Nome);
