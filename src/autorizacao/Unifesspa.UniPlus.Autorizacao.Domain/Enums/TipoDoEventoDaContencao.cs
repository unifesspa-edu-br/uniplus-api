namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que a trilha da contenção registra do perfil.</summary>
public enum TipoDoEventoDaContencao
{
    /// <summary>Uma pessoa suspendeu o perfil.</summary>
    [CodigoDeContrato("SUSPENSAO", "Suspensão")]
    Suspensao = 1,

    /// <summary>A suspensão se fechou: as etapas exigidas foram decididas.</summary>
    [CodigoDeContrato("RETOMADA", "Retomada")]
    Retomada = 2,

    /// <summary>Uma pessoa desativou o perfil.</summary>
    [CodigoDeContrato("DESATIVACAO", "Desativação")]
    Desativacao = 3,

    /// <summary>Outra pessoa reativou o perfil desativado.</summary>
    [CodigoDeContrato("REATIVACAO", "Reativação")]
    Reativacao = 4,
}
