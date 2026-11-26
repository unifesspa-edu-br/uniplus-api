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
    private IReadOnlyList<string> _turnosProtegidos = Array.AsReadOnly(Array.Empty<string>());

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
    public IReadOnlyList<string> Turnos
    {
        get => _turnosProtegidos;
        private set => _turnosProtegidos = Array.AsReadOnly(value.ToArray());
    }

    public CopiaDaOfertaDeCurso ParaNovoDono() =>
        new(Codigo, CursoNome, CursoGrau, UnidadeOfertanteNome, Turnos);

    public static Result<CopiaDaOfertaDeCurso> Criar(
        string? codigo, string? cursoNome, string? cursoGrau, string? unidadeOfertanteNome, IReadOnlyList<string>? turnos)
    {
        string? nome = Texto(cursoNome, TamanhoMaximoDoNomeDoCurso);
        string? grau = Texto(cursoGrau, TamanhoMaximoDoGrauDoCurso);
        string? unidade = Texto(unidadeOfertanteNome, TamanhoMaximoDoNomeDaUnidadeOfertante);
        List<string?> turnosNormalizados = [.. (turnos ?? []).Select(static turno => Texto(turno, TamanhoMaximoDoTurno))];
        List<FieldError> recusas = [];
        ConferirCodigo(codigo, recusas);
        ConferirDadosDoCurso(nome, grau, unidade, recusas);
        ConferirTurnos(turnosNormalizados, recusas);
        return recusas.Count > 0
            ? Result<CopiaDaOfertaDeCurso>.ValidationFailure(recusas)
            : Result<CopiaDaOfertaDeCurso>.Success(new CopiaDaOfertaDeCurso(codigo!, nome!, grau!, unidade!, [.. turnosNormalizados.Select(static turno => turno!)]));
    }

    private static void ConferirDadosDoCurso(string? nome, string? grau, string? unidade, List<FieldError> recusas)
    {
        ConferirTexto(nome, "cursoNome", $"O nome do curso é obrigatório e deve ter até {TamanhoMaximoDoNomeDoCurso} caracteres válidos.", recusas);
        ConferirTexto(grau, "cursoGrau", $"O grau do curso é obrigatório e deve ter até {TamanhoMaximoDoGrauDoCurso} caracteres válidos.", recusas);
        ConferirTexto(unidade, "unidadeOfertanteNome", $"O nome da unidade ofertante é obrigatório e deve ter até {TamanhoMaximoDoNomeDaUnidadeOfertante} caracteres válidos.", recusas);
    }

    private static void ConferirCodigo(string? codigo, List<FieldError> recusas)
    {
        if (codigo is null || codigo.Length > TamanhoMaximoDoCodigo || !FormatoDoCodigo.TemFormatoValido(codigo))
        {
            recusas.Add(new("ofertaCursoCodigo", new DomainError(CopiaDaOfertaDeCursoErrorCodes.CodigoFormatoInvalido,
                "O código da oferta de curso deve começar com letra maiúscula e conter só letras maiúsculas sem acento, algarismos e sublinhado entre as palavras, "
                + $"com até {TamanhoMaximoDoCodigo} caracteres.")));
        }
    }

    private static void ConferirTexto(string? texto, string campo, string mensagem, List<FieldError> recusas)
    {
        if (texto is null)
        {
            recusas.Add(new(campo, new DomainError(CopiaDaOfertaDeCursoErrorCodes.Invalida, mensagem)));
        }
    }

    private static void ConferirTurnos(List<string?> turnos, List<FieldError> recusas)
    {
        if (turnos.Count == 0 || turnos.Any(static turno => turno is null))
        {
            recusas.Add(new("turnos", new DomainError(CopiaDaOfertaDeCursoErrorCodes.Invalida,
                $"Informe ao menos um turno válido, com até {TamanhoMaximoDoTurno} caracteres cada.")));
        }
    }

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
