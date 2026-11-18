namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O registro pendente vencido e as chaves que ele deixou no armazenamento: a do objeto que a URL de
/// envio apontava e, no arquivo do administrador, a da cópia gravada na confirmação que talvez não
/// tenha chegado ao banco. Vazia quando o armazenamento do arquivo não é removido por esta rotina.
/// </summary>
public sealed record ArquivoPendenteVencido(Guid Id, IReadOnlyList<string> ChavesDeObjeto);
