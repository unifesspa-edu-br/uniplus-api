namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação do pedido de concessão de acesso.</summary>
public enum SituacaoDoPedido
{
    /// <summary>O pedido é para outra pessoa, que ainda não o aceitou; sem a identidade dela nenhuma decisão acontece.</summary>
    [CodigoDeContrato("AGUARDANDO_BENEFICIARIO", "Aguardando beneficiário")]
    AguardandoBeneficiario = 1,

    /// <summary>Aguarda a aprovação do gestor de acessos, a adequação funcional.</summary>
    [CodigoDeContrato("AGUARDANDO_GESTOR", "Aguardando gestor")]
    AguardandoGestor = 2,

    /// <summary>Aprovado pelo gestor; aguarda a proteção de dados, a adequação à proteção de dados.</summary>
    [CodigoDeContrato("AGUARDANDO_PROTECAO_DE_DADOS", "Aguardando proteção de dados")]
    AguardandoProtecaoDeDados = 3,

    /// <summary>Terminal: a última aprovação exigida concedeu o acesso.</summary>
    [CodigoDeContrato("APROVADO", "Aprovado")]
    Aprovado = 4,

    /// <summary>Terminal: recusado por um aprovador, com motivo, sem concessão.</summary>
    [CodigoDeContrato("RECUSADO", "Recusado")]
    Recusado = 5,

    /// <summary>Terminal: cancelado pelo solicitante antes da primeira decisão.</summary>
    [CodigoDeContrato("CANCELADO", "Cancelado")]
    Cancelado = 6,

    /// <summary>
    /// O perfil exige o documento de designação e o pedido ainda não tem um anexo verificado: nenhuma etapa
    /// de decisão nem o aceite do beneficiário acontece antes dele.
    /// </summary>
    [CodigoDeContrato("AGUARDANDO_DOCUMENTO", "Aguardando documento")]
    AguardandoDocumento = 7,

    /// <summary>
    /// Terminal: a versão do perfil que o pedido seguia foi substituída por outra que acrescenta permissão, e o
    /// solicitante refaz o pedido depois que o perfil for atualizado.
    /// </summary>
    [CodigoDeContrato("DEVOLVIDO", "Devolvido")]
    Devolvido = 8,
}
