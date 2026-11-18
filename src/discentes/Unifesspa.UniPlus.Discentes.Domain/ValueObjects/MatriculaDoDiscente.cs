namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Matrícula do discente: obrigatória, só dígitos — o formato que a origem promete — e
/// dentro do que a réplica comporta. Guarda o texto como veio.
/// </summary>
public sealed record MatriculaDoDiscente
{
    public string Valor { get; }

    private MatriculaDoDiscente(string valor) => Valor = valor;

    public static Result<MatriculaDoDiscente> Criar(string? texto)
    {
        DomainError? erro = TextoDaReplica.RecusarSeVazio(
                texto, DiscentesErrorCodes.VinculoDiscente.MatriculaVazia, "Matrícula é obrigatória.")
            ?? RecusarSeNaoNumerica(texto)
            ?? TextoDaReplica.RecusarSeExceder(
                texto,
                LimitesDaReplica.Matricula,
                DiscentesErrorCodes.VinculoDiscente.MatriculaLonga,
                $"Matrícula excede {LimitesDaReplica.Matricula} caracteres, o máximo que a réplica comporta.");

        return erro is null
            ? Result<MatriculaDoDiscente>.Success(new MatriculaDoDiscente(texto!))
            : Result<MatriculaDoDiscente>.Failure(erro);
    }

    /// <summary>
    /// Diz se o texto é composto apenas por dígitos, que é o formato de matrícula que a
    /// origem promete.
    /// </summary>
    public static bool ApenasDigitos(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);

        return valor.Length > 0 && !valor.AsSpan().ContainsAnyExceptInRange('0', '9');
    }

    /// <summary>A matrícula identifica a pessoa: nenhuma interpolação a expõe.</summary>
    public override string ToString() => "[MatriculaDoDiscente]";

    private static DomainError? RecusarSeNaoNumerica(string? texto) =>
        texto is not null && !ApenasDigitos(texto)
            ? new DomainError(
                DiscentesErrorCodes.VinculoDiscente.MatriculaNaoNumerica,
                "Matrícula precisa ser composta apenas por dígitos.")
            : null;
}
