namespace Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Para que o chamador decidiu o acesso, e portanto o que a política de registro grava: a negativa de um pedido é
/// tentativa de alguém, a de uma projeção é pergunta do sistema.
/// </summary>
public enum IntencaoDoRegistro
{
    /// <summary>
    /// Pedido que pode ser recusado. Grava a decisão que o autorizou (a primeira, havendo alternativas) ou, se nenhuma
    /// autorizou, as negativas, desde que o dado alcançado seja pessoal ou mais restrito.
    /// </summary>
    Pedido = 0,

    /// <summary>
    /// A etapa de um fluxo de aprovação. Como o <see cref="Pedido"/>, mas grava todas as permissões que habilitaram a
    /// aprovação, e não só a primeira.
    /// </summary>
    Aprovador = 1,

    /// <summary>
    /// A projeção de classes de dado e de arquivo. Grava toda classe permitida exercida, de qualquer sensibilidade, e
    /// nunca a negada: quem perguntou foi o sistema.
    /// </summary>
    Projecao = 2,

    /// <summary>Uma entrada por resource exibido numa listagem. Grava a decisão permitida, de qualquer sensibilidade.</summary>
    PorResourceExibido = 3,
}
