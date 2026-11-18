namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

using Unifesspa.UniPlus.Discentes.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Nome do discente como o SIGAA o entrega: obrigatório e dentro do que a réplica comporta,
/// sem exigir sobrenome nem restringir caracteres. Nunca aparece inteiro em texto de
/// diagnóstico.
/// </summary>
public sealed record NomeDoDiscente
{
    public string Valor { get; }

    private NomeDoDiscente(string valor) => Valor = valor;

    /// <summary>A inicial de cada parte do nome, como em <c>A*** E*** d* S***</c>.</summary>
    public string Mascarado =>
        string.Join(' ', Valor.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Mascarar));

    public static Result<NomeDoDiscente> Criar(string? texto)
    {
        DomainError? erro = TextoDaReplica.RecusarSeVazio(
                texto, DiscentesErrorCodes.VinculoDiscente.NomeVazio, "Nome é obrigatório.")
            ?? TextoDaReplica.RecusarSeExceder(
                texto,
                LimitesDaReplica.Nome,
                DiscentesErrorCodes.VinculoDiscente.NomeLongo,
                $"Nome excede {LimitesDaReplica.Nome} caracteres, o máximo que a réplica comporta.");

        return erro is null
            ? Result<NomeDoDiscente>.Success(new NomeDoDiscente(texto!))
            : Result<NomeDoDiscente>.Failure(erro);
    }

    public override string ToString() => Mascarado;

    private static string Mascarar(string parte) => $"{parte[0]}{new string('*', Math.Min(parte.Length - 1, 3))}";
}
