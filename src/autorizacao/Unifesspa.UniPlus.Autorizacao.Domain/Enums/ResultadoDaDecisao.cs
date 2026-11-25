namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que um aprovador decidiu numa etapa do pedido.</summary>
public enum ResultadoDaDecisao
{
    [CodigoDeContrato("APROVADO", "Aprovado")]
    Aprovado = 1,
    [CodigoDeContrato("RECUSADO", "Recusado")]
    Recusado = 2,

    /// <summary>A etapa não decidiu: o perfil mudou depois da aprovação do gestor e o pedido voltou a aguardá-lo.</summary>
    [CodigoDeContrato("PERFIL_ALTERADO", "Perfil alterado")]
    PerfilAlterado = 3,

    /// <summary>A etapa não decidiu: o prazo que o gestor definiu venceu antes dela e o pedido voltou a aguardá-lo.</summary>
    [CodigoDeContrato("PRAZO_VENCIDO", "Prazo vencido")]
    PrazoVencido = 4,

    /// <summary>
    /// A etapa não decidiu: o perfil passou a exigir o termo de responsabilidade depois da criação, e o pedido, sem versão
    /// fixada, foi devolvido.
    /// </summary>
    [CodigoDeContrato("TERMO_EXIGIDO", "Termo exigido")]
    TermoExigido = 5,
}
