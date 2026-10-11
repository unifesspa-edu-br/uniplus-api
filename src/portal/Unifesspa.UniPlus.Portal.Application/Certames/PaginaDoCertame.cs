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
/// Endereço público do documento do ato, quando o acervo o tem. Hoje só o ato que criou a versão
/// servida tem: é o edital que Seleção publica no acervo.
/// </param>
public sealed record EventoDaLinhaDoTempo(
    Guid Id,
    DateOnly Data,
    string Titulo,
    string? Numero,
    int Ano,
    TipoDoEvento Tipo,
    string? Descricao,
    Uri? DocumentoUrl);

/// <summary>
/// Tipo de um ato da linha do tempo.
/// </summary>
/// <param name="Codigo">Código do tipo do ato.</param>
/// <param name="Nome">Nome do tipo copiado no registro do ato, ou o código quando o ato não o tem.</param>
public sealed record TipoDoEvento(string Codigo, string Nome);
