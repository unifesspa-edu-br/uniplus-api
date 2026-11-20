namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using System.Collections.Immutable;

/// <summary>
/// Os códigos declarados em um enum estão inconsistentes. Traz um item por defeito, cada um com o nome do membro
/// responsável, para que o defeito derrube a subida em vez de aparecer em execução.
/// </summary>
public sealed class CodigosDeContratoInvalidosException : InvalidOperationException
{
    /// <summary>Cria a exceção a partir dos defeitos encontrados.</summary>
    public CodigosDeContratoInvalidosException(IReadOnlyList<string> defeitos)
        : base(Montar(defeitos))
    {
        Defeitos = [.. defeitos];
    }

    /// <summary>Defeitos encontrados, um por item.</summary>
    public ImmutableArray<string> Defeitos { get; }

    private static string Montar(IReadOnlyList<string> defeitos)
    {
        ArgumentNullException.ThrowIfNull(defeitos);

        return "Os códigos de contrato dos enums são inválidos:" + string.Concat(defeitos.Select(static d => $"{Environment.NewLine} - {d}"));
    }
}
