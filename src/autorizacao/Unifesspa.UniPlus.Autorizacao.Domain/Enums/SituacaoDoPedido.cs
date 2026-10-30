namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Situação do pedido de concessão de acesso.</summary>
public enum SituacaoDoPedido
{
    /// <summary>O pedido é para outra pessoa, que ainda não o aceitou; sem a identidade dela nenhuma decisão acontece.</summary>
    AguardandoBeneficiario = 1,

    /// <summary>Aguarda a aprovação do gestor de acessos, a adequação funcional.</summary>
    AguardandoGestor = 2,

    /// <summary>Aprovado pelo gestor; aguarda a proteção de dados, a adequação à proteção de dados.</summary>
    AguardandoProtecaoDeDados = 3,

    /// <summary>Terminal: a última aprovação exigida concedeu o acesso.</summary>
    Aprovado = 4,

    /// <summary>Terminal: recusado por um aprovador, com motivo, sem concessão.</summary>
    Recusado = 5,

    /// <summary>Terminal: cancelado pelo solicitante antes da primeira decisão.</summary>
    Cancelado = 6,

    /// <summary>
    /// O perfil exige o documento de designação e o pedido ainda não tem um anexo verificado: nenhuma etapa
    /// de decisão nem o aceite do beneficiário acontece antes dele.
    /// </summary>
    AguardandoDocumento = 7,
}

/// <summary>Código textual canônico de <see cref="SituacaoDoPedido"/>, o valor gravado e exposto.</summary>
public static class SituacaoDoPedidoCodigo
{
    public const string AguardandoBeneficiario = "aguardando_beneficiario";
    public const string AguardandoGestor = "aguardando_gestor";
    public const string AguardandoProtecaoDeDados = "aguardando_protecao_de_dados";
    public const string Aprovado = "aprovado";
    public const string Recusado = "recusado";
    public const string Cancelado = "cancelado";
    public const string AguardandoDocumento = "aguardando_documento";

    public static string ToCodigo(this SituacaoDoPedido situacao) => situacao switch
    {
        SituacaoDoPedido.AguardandoBeneficiario => AguardandoBeneficiario,
        SituacaoDoPedido.AguardandoGestor => AguardandoGestor,
        SituacaoDoPedido.AguardandoProtecaoDeDados => AguardandoProtecaoDeDados,
        SituacaoDoPedido.Aprovado => Aprovado,
        SituacaoDoPedido.Recusado => Recusado,
        SituacaoDoPedido.Cancelado => Cancelado,
        SituacaoDoPedido.AguardandoDocumento => AguardandoDocumento,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    public static SituacaoDoPedido FromCodigo(string codigo) => codigo switch
    {
        AguardandoBeneficiario => SituacaoDoPedido.AguardandoBeneficiario,
        AguardandoGestor => SituacaoDoPedido.AguardandoGestor,
        AguardandoProtecaoDeDados => SituacaoDoPedido.AguardandoProtecaoDeDados,
        Aprovado => SituacaoDoPedido.Aprovado,
        Recusado => SituacaoDoPedido.Recusado,
        Cancelado => SituacaoDoPedido.Cancelado,
        AguardandoDocumento => SituacaoDoPedido.AguardandoDocumento,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
