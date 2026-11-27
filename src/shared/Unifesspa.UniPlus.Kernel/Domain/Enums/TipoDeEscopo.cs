namespace Unifesspa.UniPlus.Kernel.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Alcance de uma concessão: uma unidade organizacional ou a instituição inteira.</summary>
public enum TipoDeEscopo
{
    /// <summary>A concessão vale numa unidade; não alcança as unidades abaixo dela (ADR-0079).</summary>
    [CodigoDeContrato("UNIDADE", "Unidade")]
    Unidade = 1,

    /// <summary>A concessão vale para recursos da instituição.</summary>
    [CodigoDeContrato("INSTITUICAO", "Instituição")]
    Instituicao = 2,
}
