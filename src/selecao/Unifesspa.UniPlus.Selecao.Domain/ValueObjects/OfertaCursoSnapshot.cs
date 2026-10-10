namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cópia por valor do que nomeia uma oferta de curso para o candidato — o curso com o grau, a
/// unidade ofertante e os turnos —, no momento em que a distribuição de vagas é definida
/// (snapshot-copy, ADR-0061). É o que o candidato vê ao escolher a oferta na inscrição.
/// </summary>
/// <remarks>
/// Renomear o curso ou a unidade depois não alcança o processo; quem quiser o nome novo redefine
/// a distribuição.
/// </remarks>
public sealed record OfertaCursoSnapshot
{
    /// <summary>Tamanho máximo do nome do curso — o mesmo da coluna do cadastro de cursos.</summary>
    public const int CursoNomeMaxLength = 200;

    /// <summary>Tamanho máximo do grau do curso — o mesmo da coluna do cadastro de cursos.</summary>
    public const int CursoGrauMaxLength = 60;

    /// <summary>Tamanho máximo do nome da unidade ofertante — o mesmo do cadastro de ofertas.</summary>
    public const int UnidadeOfertanteNomeMaxLength = 250;

    /// <summary>Tamanho máximo do token de um turno.</summary>
    public const int TurnoMaxLength = 30;

    // EF Core materialization
    private OfertaCursoSnapshot() { }

    private OfertaCursoSnapshot(string cursoNome, string cursoGrau, string unidadeOfertanteNome, IReadOnlyList<string> turnos)
    {
        CursoNome = cursoNome;
        CursoGrau = cursoGrau;
        UnidadeOfertanteNome = unidadeOfertanteNome;
        Turnos = turnos;
    }

    public string CursoNome { get; private set; } = string.Empty;

    public string CursoGrau { get; private set; } = string.Empty;

    public string UnidadeOfertanteNome { get; private set; } = string.Empty;

    /// <summary>Os turnos que a oferta ocupa (tokens), na ordem canônica do cadastro.</summary>
    public IReadOnlyList<string> Turnos { get; private set; } = [];

    public static Result<OfertaCursoSnapshot> Criar(
        string? cursoNome, string? cursoGrau, string? unidadeOfertanteNome, IReadOnlyList<string>? turnos)
    {
        string? nome = Texto(cursoNome, CursoNomeMaxLength);
        string? grau = Texto(cursoGrau, CursoGrauMaxLength);
        string? unidade = Texto(unidadeOfertanteNome, UnidadeOfertanteNomeMaxLength);
        List<string?> turnosNormalizados = [.. (turnos ?? []).Select(static t => Texto(t, TurnoMaxLength))];

        if (nome is null || grau is null || unidade is null
            || turnosNormalizados.Count == 0 || turnosNormalizados.Any(static t => t is null))
        {
            return Result<OfertaCursoSnapshot>.Failure(new DomainError(
                "OfertaCursoSnapshot.Invalida",
                "A oferta de curso precisa de curso, grau, unidade ofertante e ao menos um turno, sem caractere nulo e "
                + $"dentro do tamanho permitido (curso até {CursoNomeMaxLength}, grau até {CursoGrauMaxLength}, "
                + $"unidade até {UnidadeOfertanteNomeMaxLength} e turno até {TurnoMaxLength} caracteres)."));
        }

        return Result<OfertaCursoSnapshot>.Success(
            new OfertaCursoSnapshot(nome, grau, unidade, [.. turnosNormalizados.Select(static t => t!)]));
    }

    public bool Equals(OfertaCursoSnapshot? other) =>
        other is not null
        && string.Equals(CursoNome, other.CursoNome, StringComparison.Ordinal)
        && string.Equals(CursoGrau, other.CursoGrau, StringComparison.Ordinal)
        && string.Equals(UnidadeOfertanteNome, other.UnidadeOfertanteNome, StringComparison.Ordinal)
        && Turnos.SequenceEqual(other.Turnos, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(CursoNome, CursoGrau, UnidadeOfertanteNome, Turnos.Count);

    /// <summary>
    /// O texto em NFC, como entra no envelope canônico; nulo quando vazio, longo demais ou com
    /// caractere nulo — o decodificador também passa por aqui, e um envelope adulterado não pode
    /// injetar U+0000 que só falharia na constraint do Postgres.
    /// </summary>
    private static string? Texto(string? valor, int tamanhoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        string? normalizado = TextoCongelado.Normalizar(valor);
        return normalizado is null || TextoCongelado.ContemCaractereNulo(normalizado) || normalizado.Length > tamanhoMaximo
            ? null
            : normalizado;
    }
}
