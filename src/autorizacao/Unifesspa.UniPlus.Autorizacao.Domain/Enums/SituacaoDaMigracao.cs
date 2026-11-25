namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação de uma concessão dentro da revisão das concessões, desde que as decisões da gestão foram enviadas.</summary>
public enum SituacaoDaMigracao
{
    /// <summary>A concessão é do próprio gestor que enviou as decisões: outro gestor a decide.</summary>
    [CodigoDeContrato("RETIDA", "Retida")]
    Retida = 1,

    /// <summary>A gestão migrou, e a proteção de dados ainda decide.</summary>
    [CodigoDeContrato("AGUARDANDO_PROTECAO_DE_DADOS", "Aguardando proteção de dados")]
    AguardandoProtecaoDeDados = 2,

    /// <summary>Final: a concessão passou para a versão-alvo.</summary>
    [CodigoDeContrato("MIGRADA", "Migrada")]
    Migrada = 3,

    /// <summary>Final: a concessão foi recusada e revogada.</summary>
    [CodigoDeContrato("RECUSADA", "Recusada")]
    Recusada = 4,

    /// <summary>Final: ninguém decidiu até o fim da revisão, e a concessão foi revogada.</summary>
    [CodigoDeContrato("NAO_MIGRADA", "Não migrada")]
    NaoMigrada = 5,

    /// <summary>Final: a concessão saiu da lista antes da decisão, por revogação ou vencimento.</summary>
    [CodigoDeContrato("FORA_DA_LISTA", "Fora da lista")]
    ForaDaLista = 6,
}
