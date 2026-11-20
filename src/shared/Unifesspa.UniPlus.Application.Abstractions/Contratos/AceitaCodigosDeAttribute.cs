namespace Unifesspa.UniPlus.Application.Abstractions.Contratos;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Declara que o campo de pedido ou o parâmetro de consulta aceita os códigos de um enum que declara
/// <see cref="CodigoDeContratoAttribute"/>. O contrato publica o campo com o componente <c>{Enum}Codigo</c>, o mesmo do
/// <c>codigo</c> da resposta, em vez de texto livre. O campo é um texto ou uma coleção de textos; em outro tipo a geração
/// do contrato falha.
/// </summary>
/// <typeparam name="TEnum">Enum cujos códigos o campo aceita.</typeparam>
/// <remarks>
/// Só documenta o contrato; não valida a requisição. Quem recusa o código desconhecido é o domínio, no pedido, e a
/// consulta, no filtro. Numa coleção, o contrato publica só os códigos válidos como item: o item nulo que o domínio recebe
/// para recusar não é publicado como aceito. Num registro posicional, vale no parâmetro do construtor e na propriedade
/// (<c>[property: ...]</c>).
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class AceitaCodigosDeAttribute<TEnum> : Attribute, IAceitaCodigosDe
    where TEnum : struct, Enum
{
    Type IAceitaCodigosDe.EnumDeContrato => typeof(TEnum);
}
