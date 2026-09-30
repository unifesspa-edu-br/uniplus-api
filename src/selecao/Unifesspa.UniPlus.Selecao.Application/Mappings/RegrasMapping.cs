namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>As regras do formulário — predicado e obrigatoriedade — na forma de leitura, a mesma para item e termo.</summary>
internal static class RegrasMapping
{
    public static ObrigatoriedadeDto ToDto(this Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        return new ObrigatoriedadeDto(
            PredicadoDnfJson.ParaToken(obrigatoriedade.Tipo),
            obrigatoriedade.Predicado?.ToDto());
    }

    public static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>> ToDto(this PredicadoDnf predicado)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        return [.. predicado.Clausulas.Select(static c => (IReadOnlyList<CondicaoPrecondicaoDto>)
            [.. c.Condicoes.Select(static d => new CondicaoPrecondicaoDto(d.Fato, d.Operador.ToCodigo(), d.Valor))])];
    }
}
