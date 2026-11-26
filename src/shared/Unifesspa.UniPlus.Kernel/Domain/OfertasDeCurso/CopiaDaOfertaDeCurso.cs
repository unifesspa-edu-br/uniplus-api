namespace Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cópia por valor do código, nome do curso, grau, unidade ofertante e turnos de uma oferta de curso.
/// Os dados preservam os valores informados no momento da cópia (ADR-0061).
/// </summary>
public sealed record CopiaDaOfertaDeCurso
{
    /// <summary>Tamanho máximo do código da oferta — o mesmo da coluna do cadastro de ofertas.</summary>
    public const int TamanhoMaximoDoCodigo = 50;

    /// <summary>Tamanho máximo do nome do curso — o mesmo da coluna do cadastro de cursos.</summary>
    public const int TamanhoMaximoDoNomeDoCurso = 200;

    /// <summary>Tamanho máximo do grau do curso — o mesmo da coluna do cadastro de cursos.</summary>
    public const int TamanhoMaximoDoGrauDoCurso = 60;

    /// <summary>Tamanho máximo do nome da unidade ofertante — o mesmo do cadastro de ofertas.</summary>
    public const int TamanhoMaximoDoNomeDaUnidadeOfertante = 250;

    /// <summary>Tamanho máximo do token de um turno.</summary>
    public const int TamanhoMaximoDoTurno = 30;

    // EF Core materialization
    private CopiaDaOfertaDeCurso() { }

    private CopiaDaOfertaDeCurso(string codigo, string cursoNome, string cursoGrau, string unidadeOfertanteNome, IReadOnlyList<string> turnos)
    {
        Codigo = codigo;
        CursoNome = cursoNome;
        CursoGrau = cursoGrau;
        UnidadeOfertanteNome = unidadeOfertanteNome;
        Turnos = turnos;
    }

    /// <summary>O código que a gestão deu à oferta, com que o edital a cita; a identidade da oferta é o identificador de origem.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string CursoNome { get; private set; } = string.Empty;

    public string CursoGrau { get; private set; } = string.Empty;

    public string UnidadeOfertanteNome { get; private set; } = string.Empty;

    /// <summary>Os turnos que a oferta ocupa (tokens), na ordem canônica do cadastro.</summary>
    public IReadOnlyList<string> Turnos { get; private set; } = [];

    public static Result<CopiaDaOfertaDeCurso> Criar(
        string? codigo, string? cursoNome, string? cursoGrau, string? unidadeOfertanteNome, IReadOnlyList<string>? turnos)
    {
        string? codigoInformado = CodigoValido(codigo) ? codigo : null;
        string? nome = Texto(cursoNome, TamanhoMaximoDoNomeDoCurso);
        string? grau = Texto(cursoGrau, TamanhoMaximoDoGrauDoCurso);
        string? unidade = Texto(unidadeOfertanteNome, TamanhoMaximoDoNomeDaUnidadeOfertante);
        List<string?> turnosNormalizados = [.. (turnos ?? []).Select(static turno => Texto(turno, TamanhoMaximoDoTurno))];

        return codigoInformado is null || nome is null || grau is null || unidade is null
            || turnosNormalizados.Count == 0 || turnosNormalizados.Any(static turno => turno is null)
            ? Result<CopiaDaOfertaDeCurso>.Failure(new DomainError(CopiaDaOfertaDeCursoErrorCodes.Invalida, MensagemDeInvalidez))
            : Result<CopiaDaOfertaDeCurso>.Success(
                new CopiaDaOfertaDeCurso(codigoInformado, nome, grau, unidade, [.. turnosNormalizados.Select(static turno => turno!)]));
    }

    private static bool CodigoValido(string? codigo) =>
        codigo is not null && codigo.Length <= TamanhoMaximoDoCodigo && FormatoDoCodigo.TemFormatoValido(codigo);

    private static readonly string MensagemDeInvalidez =
        "A oferta de curso precisa de código, curso, grau, unidade ofertante e ao menos um turno, sem caractere nulo e "
        + $"dentro do tamanho permitido (código de letras maiúsculas, dígitos e sublinhado até {TamanhoMaximoDoCodigo}, curso até {TamanhoMaximoDoNomeDoCurso}, grau até {TamanhoMaximoDoGrauDoCurso}, "
        + $"unidade até {TamanhoMaximoDoNomeDaUnidadeOfertante} e turno até {TamanhoMaximoDoTurno} caracteres).";

    public bool Equals(CopiaDaOfertaDeCurso? outra) =>
        outra is not null
        && string.Equals(Codigo, outra.Codigo, StringComparison.Ordinal)
        && string.Equals(CursoNome, outra.CursoNome, StringComparison.Ordinal)
        && string.Equals(CursoGrau, outra.CursoGrau, StringComparison.Ordinal)
        && string.Equals(UnidadeOfertanteNome, outra.UnidadeOfertanteNome, StringComparison.Ordinal)
        && Turnos.SequenceEqual(outra.Turnos, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Codigo, CursoNome, CursoGrau, UnidadeOfertanteNome, Turnos.Count);

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
