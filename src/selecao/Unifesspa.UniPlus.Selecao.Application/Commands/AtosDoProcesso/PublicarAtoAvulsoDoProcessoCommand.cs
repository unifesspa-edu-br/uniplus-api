namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Publica um ato avulso do processo — aviso, comunicado, errata e semelhantes, que não são
/// resultado nem congelam configuração — com o documento principal e os anexos, e, quando o ato
/// corrige outro já publicado, a indicação deste e o motivo (ADR-0103).
/// </summary>
/// <param name="ProcessoSeletivoId">O processo que publica o ato.</param>
/// <param name="TipoAtoCodigo">Código do tipo do ato no catálogo de Publicações.</param>
/// <param name="Numero">Número que o documento declara, quando o tipo o tem.</param>
/// <param name="DataPublicacao">Data que o documento declara.</param>
/// <param name="Assinante">Quem assina o ato.</param>
/// <param name="DocumentoPrincipalId">O documento a publicar confirmado que é o principal, sempre PDF.</param>
/// <param name="Anexos">Os anexos, na ordem em que o ato os apresenta; nulo ou vazio quando não há.</param>
/// <param name="AtoCorrigidoId">O ato publicado que este corrige; nulo quando o ato não corrige nenhum.</param>
/// <param name="MotivoDaCorrecao">Motivo da correção; vem junto com <paramref name="AtoCorrigidoId"/>.</param>
public sealed record PublicarAtoAvulsoDoProcessoCommand(
    Guid ProcessoSeletivoId,
    string TipoAtoCodigo,
    string? Numero,
    DateOnly DataPublicacao,
    string Assinante,
    Guid DocumentoPrincipalId,
    IReadOnlyList<AnexoSolicitadoDoAto>? Anexos = null,
    Guid? AtoCorrigidoId = null,
    string? MotivoDaCorrecao = null) : ICommand<Result<AtoPublicadoDto>>, IDadosDoAtoPublicado;

/// <summary>Um anexo que o administrador pede para o ato: o documento confirmado e o nome do download.</summary>
/// <param name="DocumentoId">O documento a publicar confirmado, do mesmo processo.</param>
/// <param name="Nome">Nome do arquivo no download; a extensão do formato é acrescentada quando falta.</param>
public sealed record AnexoSolicitadoDoAto(Guid DocumentoId, string? Nome);
