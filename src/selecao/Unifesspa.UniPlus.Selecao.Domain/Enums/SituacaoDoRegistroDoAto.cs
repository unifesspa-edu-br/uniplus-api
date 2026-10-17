namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Situação do registro de um ato do processo na Seleção. Só há duas: o módulo Publicações responde
/// apenas o sucesso do registro do ato, e a recusa de mérito vai para a fila de mensagens que falharam,
/// sem resposta — por isso não existe "recusado", e o que ficar <see cref="Pendente"/> é resolvido
/// à mão.
/// </summary>
public enum SituacaoDoRegistroDoAto
{
    /// <summary>O ato foi enviado ao registro central e a confirmação ainda não chegou.</summary>
    Pendente = 0,

    /// <summary>O registro central confirmou o ato.</summary>
    Registrado = 1,
}
