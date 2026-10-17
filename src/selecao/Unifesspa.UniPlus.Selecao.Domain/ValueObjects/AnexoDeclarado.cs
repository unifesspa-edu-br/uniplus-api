namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Anexo como o comando de publicação o declara: o documento já confirmado e o nome com que se apresenta.</summary>
/// <param name="DocumentoAPublicarId">O documento a publicar confirmado.</param>
/// <param name="NomeDeExibicao">O nome de exibição do anexo.</param>
public sealed record AnexoDeclarado(Guid DocumentoAPublicarId, string NomeDeExibicao);
