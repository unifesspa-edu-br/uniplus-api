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

    /// <summary>
    /// Terminal: a versão do perfil que o pedido seguia foi substituída por outra que acrescenta permissão, e o
    /// solicitante refaz o pedido depois que o perfil for atualizado.
    /// </summary>
    Devolvido = 8,
}

/// <summary>Código textual canônico de <see cref="SituacaoDoPedido"/>, o valor gravado e exposto.</summary>
public static class SituacaoDoPedidoCodigo
{
    public const string AguardandoBeneficiario = "AGUARDANDO_BENEFICIARIO";
    public const string AguardandoGestor = "AGUARDANDO_GESTOR";
    public const string AguardandoProtecaoDeDados = "AGUARDANDO_PROTECAO_DE_DADOS";
    public const string Aprovado = "APROVADO";
    public const string Recusado = "RECUSADO";
    public const string Cancelado = "CANCELADO";
    public const string AguardandoDocumento = "AGUARDANDO_DOCUMENTO";
    public const string Devolvido = "DEVOLVIDO";

    public static string ToCodigo(this SituacaoDoPedido situacao) => situacao switch
    {
        SituacaoDoPedido.AguardandoBeneficiario => AguardandoBeneficiario,
        SituacaoDoPedido.AguardandoGestor => AguardandoGestor,
        SituacaoDoPedido.AguardandoProtecaoDeDados => AguardandoProtecaoDeDados,
        SituacaoDoPedido.Aprovado => Aprovado,
        SituacaoDoPedido.Recusado => Recusado,
        SituacaoDoPedido.Cancelado => Cancelado,
        SituacaoDoPedido.AguardandoDocumento => AguardandoDocumento,
        SituacaoDoPedido.Devolvido => Devolvido,
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
        Devolvido => SituacaoDoPedido.Devolvido,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
