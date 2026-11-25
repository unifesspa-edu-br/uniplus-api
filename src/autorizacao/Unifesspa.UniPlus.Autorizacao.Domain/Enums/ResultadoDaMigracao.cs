namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que uma decisão da revisão das concessões fez com a concessão.</summary>
public enum ResultadoDaMigracao
{
    [CodigoDeContrato("MIGRAR", "Migrar")]
    Migrar = 1,
    [CodigoDeContrato("RECUSAR", "Recusar")]
    Recusar = 2,

    /// <summary>O sistema reteve a concessão do próprio gestor que enviou as decisões.</summary>
    [CodigoDeContrato("RETER", "Reter")]
    Reter = 3,

    /// <summary>O sistema encerrou a concessão que ninguém decidiu até o fim da revisão.</summary>
    [CodigoDeContrato("NAO_MIGRADA", "Não migrada")]
    NaoMigrada = 4,

    /// <summary>O sistema tirou da lista a concessão que foi revogada ou venceu antes da decisão.</summary>
    [CodigoDeContrato("FORA_DA_LISTA", "Fora da lista")]
    ForaDaLista = 5,
}
