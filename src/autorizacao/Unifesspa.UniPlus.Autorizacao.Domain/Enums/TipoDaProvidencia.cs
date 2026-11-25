namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que o Gestor de proteção de dados pede sobre uma concessão (UNI-REQ-0375).</summary>
public enum TipoDaProvidencia
{
    /// <summary>A retirada do acesso.</summary>
    [CodigoDeContrato("RETIRADA", "Retirada")]
    Retirada = 1,

    /// <summary>A revisão do acesso.</summary>
    [CodigoDeContrato("REVISAO", "Revisão")]
    Revisao = 2,
}
