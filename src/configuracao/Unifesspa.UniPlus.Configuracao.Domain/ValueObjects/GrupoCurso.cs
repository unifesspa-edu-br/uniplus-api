namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Grupo de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe) — domínio
/// <b>fechado</b> de quatro grupos, cada um com <b>código</b> (sem abreviação e sem
/// acento, identidade estável) e <b>rótulo</b> fixos. O usuário escolhe o grupo pelo
/// código; o rótulo é sempre posto pelo sistema.
/// </summary>
/// <remarks>
/// Não há vocabulário separado nem seed: a lista vive em <see cref="Todos"/>, fonte do
/// CHECK de banco, do vocabulário exposto pela API e da validação. O pareamento
/// <c>curso.grupo_area_enem ↔ peso_area_enem.grupo_curso</c> é pelo código, sem FK.
/// </remarks>
public sealed record GrupoCurso
{
    /// <summary>Código do grupo de área "Tecnológica".</summary>
    public const string Tecnologica = "TECNOLOGICA";

    /// <summary>Código do grupo de área "Humanística I".</summary>
    public const string HumanisticaI = "HUMANISTICA_I";

    /// <summary>Código do grupo de área "Humanística II".</summary>
    public const string HumanisticaII = "HUMANISTICA_II";

    /// <summary>Código do grupo de área "Saúde e Biológicas".</summary>
    public const string SaudeEBiologicas = "SAUDE_E_BIOLOGICAS";

    /// <summary>Os quatro grupos, com código e rótulo, na ordem de exibição.</summary>
    public static IReadOnlyList<GrupoCurso> Todos { get; } =
    [
        new(Tecnologica, "Tecnológica"),
        new(HumanisticaI, "Humanística I"),
        new(HumanisticaII, "Humanística II"),
        new(SaudeEBiologicas, "Saúde e Biológicas"),
    ];

    private static readonly Dictionary<string, GrupoCurso> PorCodigo =
        Todos.ToDictionary(static grupo => grupo.Codigo, StringComparer.Ordinal);

    // Declarado depois de Todos: inicializadores estáticos rodam na ordem do texto.
    private static readonly string GruposAceitos =
        string.Join(", ", Todos.Select(static grupo => $"{grupo.Codigo} ({grupo.Rotulo})"));

    public string Codigo { get; }

    public string Rotulo { get; }

    private GrupoCurso(string codigo, string rotulo)
    {
        Codigo = codigo;
        Rotulo = rotulo;
    }

    /// <summary>
    /// Devolve o grupo do <paramref name="codigo"/> informado, com o rótulo posto pelo
    /// sistema. Código fora dos quatro (ou nulo/em branco) retorna falha de domínio. O
    /// código é normalizado por <c>Trim</c> e comparado de forma ordinal — é ASCII, então
    /// não há forma Unicode a normalizar.
    /// </summary>
    public static Result<GrupoCurso> Criar(string? codigo)
    {
        if (codigo is null || !PorCodigo.TryGetValue(codigo.Trim(), out GrupoCurso? grupo))
        {
            return Result<GrupoCurso>.Failure(new DomainError(
                GrupoCursoErrorCodes.ForaDoDominio,
                $"Grupo de área do ENEM deve ser um de: {GruposAceitos}."));
        }

        return Result<GrupoCurso>.Success(grupo);
    }

    public override string ToString() => Codigo;
}
