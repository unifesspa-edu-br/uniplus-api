namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Situação do pedido de concessão de acesso.</summary>
public enum SituacaoDoPedido
{
    /// <summary>O pedido é para outra pessoa, que ainda não o aceitou; sem a identidade dela nenhuma decisão acontece.</summary>
    AguardandoBeneficiario = 1,

    /// <summary>Aguarda a aprovação do gestor de acessos, a adequação funcional.</summary>
    AguardandoGestor = 2,

    /// <summary>Aprovado pelo gestor; aguarda a encarregada de dados, a adequação à proteção de dados.</summary>
    AguardandoEncarregada = 3,

    /// <summary>Terminal: a última aprovação exigida concedeu o acesso.</summary>
    Aprovado = 4,

    /// <summary>Terminal: recusado por um aprovador, com motivo, sem concessão.</summary>
    Recusado = 5,

    /// <summary>Terminal: cancelado pelo solicitante antes da primeira decisão.</summary>
    Cancelado = 6,
}

/// <summary>Código textual canônico de <see cref="SituacaoDoPedido"/>, o valor gravado e exposto.</summary>
public static class SituacaoDoPedidoCodigo
{
    public const string AguardandoBeneficiario = "aguardando_beneficiario";
    public const string AguardandoGestor = "aguardando_gestor";
    public const string AguardandoEncarregada = "aguardando_encarregada";
    public const string Aprovado = "aprovado";
    public const string Recusado = "recusado";
    public const string Cancelado = "cancelado";

    public static string ToCodigo(this SituacaoDoPedido situacao) => situacao switch
    {
        SituacaoDoPedido.AguardandoBeneficiario => AguardandoBeneficiario,
        SituacaoDoPedido.AguardandoGestor => AguardandoGestor,
        SituacaoDoPedido.AguardandoEncarregada => AguardandoEncarregada,
        SituacaoDoPedido.Aprovado => Aprovado,
        SituacaoDoPedido.Recusado => Recusado,
        SituacaoDoPedido.Cancelado => Cancelado,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    public static SituacaoDoPedido FromCodigo(string codigo) => codigo switch
    {
        AguardandoBeneficiario => SituacaoDoPedido.AguardandoBeneficiario,
        AguardandoGestor => SituacaoDoPedido.AguardandoGestor,
        AguardandoEncarregada => SituacaoDoPedido.AguardandoEncarregada,
        Aprovado => SituacaoDoPedido.Aprovado,
        Recusado => SituacaoDoPedido.Recusado,
        Cancelado => SituacaoDoPedido.Cancelado,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
