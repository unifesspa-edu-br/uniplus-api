namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As unidades da Unifesspa que respondem pelas fases do cronograma nos testes. A publicação
/// exige a unidade em toda fase, então o processo publicável de teste a informa; os dois
/// identificadores são fixos para que a comparação seja pelo id, nunca pela sigla.
/// </summary>
internal static class UnidadesDeTeste
{
    /// <summary>O Centro de Processos Seletivos, unidade padrão das fases de teste.</summary>
    public static UnidadeResponsavelSnapshot Ceps { get; } =
        new(Guid.Parse("0197d6f0-0000-7000-8000-00000000ce95", System.Globalization.CultureInfo.InvariantCulture), "CEPS", "Centro de Processos Seletivos");

    /// <summary>O Centro de Gestão Acadêmica, que conduz a habilitação e a matrícula.</summary>
    public static UnidadeResponsavelSnapshot Cga { get; } =
        new(Guid.Parse("0197d6f0-0000-7000-8000-00000000c9a0", System.Globalization.CultureInfo.InvariantCulture), "CGA", "Centro de Gestão Acadêmica");
}
