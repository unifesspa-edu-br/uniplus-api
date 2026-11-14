namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// A cópia ao acervo público (ADR-0132) dos documentos de um ato do processo já publicado: é o efeito posterior que
/// registra o endereço na divulgação do certame. Leva só os identificadores, sem dado pessoal.
/// </summary>
/// <param name="ProcessoSeletivoId">O processo do ato.</param>
/// <param name="AtoId">O ato cujos documentos o acervo recebe.</param>
public sealed record CopiarDocumentosDoAtoAoAcervo(Guid ProcessoSeletivoId, Guid AtoId);
