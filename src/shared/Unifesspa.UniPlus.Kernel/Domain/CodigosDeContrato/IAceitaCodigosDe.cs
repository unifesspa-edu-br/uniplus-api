namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O que <see cref="AceitaCodigosDeAttribute{TEnum}"/> declara, para quem publica o contrato e só tem o atributo em mãos,
/// sem conhecer o tipo genérico.
/// </summary>
public interface IAceitaCodigosDe
{
    /// <summary>O enum cujos códigos o campo aceita.</summary>
    Type EnumDeContrato { get; }
}
