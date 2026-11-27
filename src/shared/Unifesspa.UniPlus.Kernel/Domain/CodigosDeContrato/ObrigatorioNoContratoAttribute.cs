namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Declara que o contrato publica o campo do pedido como obrigatório e sem aceitar nulo, mas a leitura
/// do pedido não o exige. Quem recusa o campo ausente ou nulo é a regra de domínio, com a recusa de validação e o código
/// de erro dela, e não a leitura do corpo, que responderia 400.
/// </summary>
/// <remarks>
/// Num registro posicional, vale na propriedade: <c>[property: ObrigatorioNoContrato]</c>. O tipo publicado é o do campo,
/// sem a opção de nulo.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ObrigatorioNoContratoAttribute : Attribute;
