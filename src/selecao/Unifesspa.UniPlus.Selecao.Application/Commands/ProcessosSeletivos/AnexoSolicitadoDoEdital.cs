namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Um anexo que o administrador pede para o edital: o documento a publicar já confirmado e o nome
/// com que o arquivo é baixado.
/// </summary>
/// <param name="DocumentoId">O documento a publicar confirmado, do mesmo processo.</param>
/// <param name="Nome">Nome do arquivo no download; a extensão do formato é acrescentada quando falta.</param>
public sealed record AnexoSolicitadoDoEdital(Guid DocumentoId, string? Nome);
