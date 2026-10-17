namespace Unifesspa.UniPlus.Kernel.Results;

public sealed record DomainError(string Code, string Message);

/// <summary>
/// Um <see cref="DomainError"/> associado ao campo de origem — usado quando um
/// <see cref="Result"/> agrega mais de uma violação (ex.: várias regras de
/// <c>Entidade.ValidarCampos</c> falhando no mesmo lote). <see cref="Field"/> é
/// <see langword="null"/> para erros que não são de campo específico (ex.:
/// "referenciado por registro vivo").
/// <para>
/// <see cref="Origem"/> identifica de dentro o que causou a recusa (a regra e o fato citado, por
/// exemplo) quando o campo e o código não bastam para distinguir duas recusas; não vai para o wire.
/// </para>
/// </summary>
public sealed record FieldError(string? Field, DomainError Error, string? Origem = null);
