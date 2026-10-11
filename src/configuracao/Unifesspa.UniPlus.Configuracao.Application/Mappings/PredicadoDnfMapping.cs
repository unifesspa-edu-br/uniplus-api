namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal static class PredicadoDnfMapping
{
    /// <summary>O predicado como OU de cláusulas, cada uma o E das suas condições.</summary>
    public static IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoDto>> ToDto(this PredicadoDnf predicado)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        return [.. predicado.Clausulas.Select(static c => (IReadOnlyList<CondicaoDoPredicadoDto>)
            [.. c.Condicoes.Select(static d => new CondicaoDoPredicadoDto(d.Fato, d.Operador.ToCodigo(), d.Valor))])];
    }
}
