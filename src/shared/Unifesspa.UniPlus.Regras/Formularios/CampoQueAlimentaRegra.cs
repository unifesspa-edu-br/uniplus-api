namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// Campo opcional sem resposta resolve sem valor, e todo operador sobre ele dá falso — inclusive
/// <c>DIFERENTE</c> e <c>NAO_EM</c> (UNI-REQ-0074). Por isso o campo cujo fato é dependência de uma
/// derivação, ou é citado por negação em qualquer regra, precisa ser obrigatório sempre que exibido:
/// sem resposta, a derivação e a negação dariam resultado que o candidato não declarou. A mesma
/// regra vale no processo e no modelo.
/// </summary>
public static class CampoQueAlimentaRegra
{
    /// <summary>
    /// Os fatos que alimentam regra: toda dependência de derivação — o que já cobre o fato citado por
    /// negação por meio de um derivado — e todo fato citado por negação.
    /// </summary>
    public static HashSet<string> Fatos(IEnumerable<string> dependenciasDasDerivacoes, IEnumerable<(string Fato, Operador Operador)> condicoes)
    {
        ArgumentNullException.ThrowIfNull(dependenciasDasDerivacoes);
        ArgumentNullException.ThrowIfNull(condicoes);

        return new(
            dependenciasDasDerivacoes.Concat(condicoes.Where(static c => c.Operador is Operador.Diferente or Operador.NaoEm).Select(static c => c.Fato)),
            StringComparer.Ordinal);
    }

    /// <summary>O primeiro item, na ordem dada, que alimenta regra sem ser obrigatório sempre.</summary>
    public static DomainError? PrimeiroOpcional(
        IEnumerable<(string FatoCodigo, TipoObrigatoriedade Obrigatoriedade)> itens, IReadOnlySet<string> alimentamRegra)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(alimentamRegra);

        return itens
            .Where(i => i.Obrigatoriedade != TipoObrigatoriedade.Sempre && alimentamRegra.Contains(i.FatoCodigo))
            .Select(static i => new DomainError(
                ItemFormularioErrorCodes.OpcionalQueAlimentaRegra,
                $"O campo '{i.FatoCodigo}' alimenta uma derivação ou uma condição de negação e precisa ser obrigatório sempre que exibido."))
            .FirstOrDefault();
    }
}
