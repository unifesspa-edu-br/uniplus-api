namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// Quantas ocorrências o grupo repetível admite: o mínimo, que não é negativo, e o máximo, quando
/// declarado, que é ao menos um e não fica abaixo do mínimo. Sem máximo, o grupo aceita qualquer
/// quantidade de ocorrências.
/// </summary>
public sealed record OcorrenciasDoGrupo(int Minimo, int? Maximo)
{
    private const string Regra = "O mínimo de ocorrências não é negativo, e o máximo, quando declarado, é ao menos um e não fica abaixo do mínimo.";

    /// <summary>Se o mínimo e o máximo respeitam a regra da contagem.</summary>
    public bool Valida => Minimo >= 0 && (Maximo is not { } teto || (teto >= 1 && teto >= Minimo));

    /// <summary>A recusa aponta o mínimo quando ele é negativo e o máximo quando é o máximo que destoa.</summary>
    public IEnumerable<FieldError> Conferir()
    {
        if (Minimo < 0)
        {
            yield return new("minimo", new DomainError(GrupoFormularioErrorCodes.ContagemIncoerente, Regra));
        }
        else if (!Valida)
        {
            yield return new("maximo", new DomainError(GrupoFormularioErrorCodes.ContagemIncoerente, Regra));
        }
    }
}
