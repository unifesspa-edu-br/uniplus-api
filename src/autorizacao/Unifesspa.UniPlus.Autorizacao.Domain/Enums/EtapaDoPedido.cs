namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Etapa de aprovação do pedido. A primeira é sempre a do gestor.</summary>
public enum EtapaDoPedido
{
    /// <summary>Adequação funcional, a cargo do gestor de acessos.</summary>
    [CodigoDeContrato("GESTOR", "Gestor")]
    Gestor = 1,

    /// <summary>Adequação à proteção de dados, a cargo da proteção de dados.</summary>
    [CodigoDeContrato("PROTECAO_DE_DADOS", "Proteção de dados")]
    ProtecaoDeDados = 2,
}
