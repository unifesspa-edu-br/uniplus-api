namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Os operadores que uma condição de regra pode usar sobre um fato do catálogo, com o rótulo no sentido da
/// cardinalidade do fato. É o ponto único dessa lista: a consulta pública e a manutenção a devolvem por aqui.
/// </summary>
public static class OperadoresAdmitidosDoFato
{
    /// <summary>
    /// Os operadores do fato, na ordem em que a tela os oferece; o primeiro é o padrão da condição nova. Vazia
    /// quando o fato não é citável em regra (texto, data, endereço) ou quando não é categórico e tem mais de
    /// um valor.
    /// </summary>
    public static IReadOnlyList<CodigoComRotulo<Operador>> De(FatoCandidato fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        if (VocabularioDoCatalogo.TipoNoVocabulario(fato) is not { } dominio)
        {
            return [];
        }

        CardinalidadeDoFato cardinalidade = VocabularioDoCatalogo.CardinalidadeNoVocabulario(fato);
        return [.. OperadoresDoFato.Admitidos(dominio, cardinalidade)
            .Select(operador => CodigoComRotulo<Operador>.De(operador, OperadoresDoFato.Rotulo(operador, cardinalidade)))];
    }
}
