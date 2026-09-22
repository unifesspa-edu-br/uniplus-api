namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.HealthChecks;

/// <summary>
/// Declara se o ambiente deveria carregar o catálogo de tipos de ato que este repositório
/// publica — e, portanto, se faz sentido conferir o cadastro contra ele.
/// </summary>
/// <remarks>
/// <para>
/// Desligada por padrão porque catálogo divergente é <b>legítimo</b> em ambiente que carrega só o
/// que usa: as suítes de integração do módulo Seleção criam tipos de ato com os atributos que
/// cada cenário precisa, sem compromisso com o catálogo declarado. Ligar a conferência ali
/// degradaria permanentemente um ambiente correto para o próprio propósito, e sinal que aparece
/// sempre treina quem opera a ignorá-lo.
/// </para>
/// <para>
/// Homologação e produção ligam pelo <c>uniplus-infra</c>, que é onde a afirmação "este ambiente
/// recebe o catálogo pelo bootstrap" pertence — ela é fato de implantação, não de código.
/// </para>
/// </remarks>
internal sealed class CatalogoDeTiposAtoOptions
{
    internal const string SectionName = "Publicacoes:CatalogoDeTiposAto";

    /// <summary>Se o cadastro deve ser conferido contra o catálogo declarado.</summary>
    public bool Conferir { get; init; }
}
