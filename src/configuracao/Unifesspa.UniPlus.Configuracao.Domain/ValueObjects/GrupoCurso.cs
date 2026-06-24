namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Grupo de área do ENEM (Resolução INEP/ENEM 805/2024, Anexo I) — texto de
/// domínio <b>fechado</b> de quatro valores canônicos. É vocabulário de
/// referência (com acento e espaço), não um enumerado compilado: a lista vem da
/// resolução, e o pareamento <c>curso.grupo_area_enem ↔ peso_area_enem.grupo_curso</c>
/// é por valor sobre esse vocabulário compartilhado (sem FK). Persistido como
/// <c>varchar</c>.
/// </summary>
public sealed record GrupoCurso
{
    /// <summary>Grupo de área "Tecnológica".</summary>
    public const string Tecnologica = "Tecnológica";

    /// <summary>Grupo de área "Humanística I".</summary>
    public const string HumanisticaI = "Humanística I";

    /// <summary>Grupo de área "Humanística II".</summary>
    public const string HumanisticaII = "Humanística II";

    /// <summary>Grupo de área "Saúde e Biológicas".</summary>
    public const string SaudeEBiologicas = "Saúde e Biológicas";

    /// <summary>Conjunto canônico dos quatro grupos de área da Res. 805/2024.</summary>
    public static readonly IReadOnlySet<string> Valores =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Tecnologica,
            HumanisticaI,
            HumanisticaII,
            SaudeEBiologicas,
        };

    public string Valor { get; }

    private GrupoCurso(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="GrupoCurso"/> validando o valor contra o conjunto
    /// canônico. Valor fora do domínio (ou nulo/em branco) retorna falha de
    /// domínio. O valor é normalizado por <c>Trim</c> antes da comparação.
    /// </summary>
    public static Result<GrupoCurso> Criar(string valor)
    {
        if (!EhValido(valor))
        {
            return Result<GrupoCurso>.Failure(new DomainError(
                "GrupoCurso.ForaDoDominio",
                $"Grupo de curso deve ser um de: {string.Join(", ", Valores)}."));
        }

        return Result<GrupoCurso>.Success(new GrupoCurso(valor.Trim()));
    }

    /// <summary>Indica se <paramref name="valor"/> pertence ao domínio fechado, sem alocar.</summary>
    public static bool EhValido(string valor) =>
        !string.IsNullOrWhiteSpace(valor) && Valores.Contains(valor.Trim());

    public override string ToString() => Valor;
}
