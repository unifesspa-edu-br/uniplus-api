namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O registro pendente vencido e a chave do objeto que a URL de envio dele apontava no
/// armazenamento.
/// </summary>
public sealed record ArquivoPendenteVencido(Guid Id, string ObjectKey);
