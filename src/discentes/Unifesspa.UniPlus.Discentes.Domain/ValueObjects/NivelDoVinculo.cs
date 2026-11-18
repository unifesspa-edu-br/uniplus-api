namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Nível do vínculo (graduação, mestrado…) como o SIGAA o entrega. O vocabulário é aberto:
/// a regra é só ser informado e caber na réplica.
/// </summary>
public sealed record NivelDoVinculo
{
    public string Valor { get; }

    private NivelDoVinculo(string valor) => Valor = valor;

    public static Result<NivelDoVinculo> Criar(string? texto)
    {
        DomainError? erro = TextoDaReplica.RecusarSeVazio(
                texto, DiscentesErrorCodes.VinculoDiscente.NivelVazio, "Nível é obrigatório.")
            ?? TextoDaReplica.RecusarSeExceder(
                texto,
                LimitesDaReplica.Nivel,
                DiscentesErrorCodes.VinculoDiscente.NivelLongo,
                $"Nível excede {LimitesDaReplica.Nivel} caracteres, o máximo que a réplica comporta.");

        return erro is null
            ? Result<NivelDoVinculo>.Success(new NivelDoVinculo(texto!))
            : Result<NivelDoVinculo>.Failure(erro);
    }
}
