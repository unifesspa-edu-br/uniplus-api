namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>
/// Declara, na própria propriedade do contrato, o conjunto fechado de valores que ela
/// aceita — para que o schema OpenAPI publique um <c>enum</c> em vez de texto livre, e o
/// cliente gerado tenha o tipo fechado que o domínio já exige.
/// </summary>
/// <remarks>
/// <para>
/// Os valores são passados como argumentos, não lidos de um tipo do módulo. É o que permite
/// a este atributo viver no shared: o vocabulário continua declarado onde a regra mora
/// (uma constante do domínio de cada módulo), e chega aqui como constante de compilação,
/// sem inverter a dependência.
/// </para>
/// <para>
/// Documenta o contrato; não valida a requisição. Quem recusa valor fora do vocabulário
/// continua sendo o domínio, com o erro de negócio que explica a recusa — um cliente que
/// ignore o schema não passa a ser aceito por não ter lido a documentação.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class VocabularioFechadoAttribute : Attribute
{
    public VocabularioFechadoAttribute(params string[] valores)
    {
        ArgumentNullException.ThrowIfNull(valores);
        Valores = valores;
    }

    /// <summary>Os valores aceitos, na ordem em que devem aparecer no schema.</summary>
    public IReadOnlyList<string> Valores { get; }
}
