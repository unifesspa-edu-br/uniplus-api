namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O prazo máximo de emissão de uma exigência de documento visto pela conferência (UNI-REQ-0016): o que a
/// exigência declara, se declara, e se a regra do processo obriga a declarar, como no comprovante de
/// residência do conjunto básico da habilitação (UNI-REQ-0151).
/// </summary>
public sealed record PrazoDeEmissaoDaExigencia(bool PrazoObrigatorio, IdadeMaximaEmissao? Idade);
