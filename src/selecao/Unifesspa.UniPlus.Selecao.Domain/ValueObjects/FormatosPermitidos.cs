namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Um formato aceito dentro da <see cref="FormatosPermitidos.Lista"/> — sem
/// identidade própria (não deriva de <c>EntityBase</c>): é dado congelado junto do pai, a
/// mesma razão pela qual o VO não recebe entidade filha própria na persistência (jsonb, não
/// tabela 1:N).
/// </summary>
public sealed record FormatoPermitidoEntry
{
    private FormatoPermitidoEntry(FormatoPermitido formato, int? tamanhoMaximoBytesMax)
    {
        Formato = formato;
        TamanhoMaximoBytesMax = tamanhoMaximoBytesMax;
    }

    public FormatoPermitido Formato { get; }

    /// <summary>Teto POR FORMATO — independente e sem relação com o teto GLOBAL da exigência (<c>DocumentoExigido.TamanhoMaximoBytes</c>).</summary>
    public int? TamanhoMaximoBytesMax { get; }

    public static Result<FormatoPermitidoEntry> Criar(FormatoPermitido formato, int? tamanhoMaximoBytesMax)
    {
        if (!Enum.IsDefined(formato))
        {
            return Result<FormatoPermitidoEntry>.Failure(new DomainError("FormatosPermitidos.FormatoInvalido", "Formato não reconhecido."));
        }

        if (tamanhoMaximoBytesMax is <= 0)
        {
            return Result<FormatoPermitidoEntry>.Failure(new DomainError(
                "FormatosPermitidos.TamanhoMaximoBytesMaxInvalido",
                "O tamanho máximo em bytes por formato, quando presente, deve ser maior que zero."));
        }

        return Result<FormatoPermitidoEntry>.Success(new FormatoPermitidoEntry(formato, tamanhoMaximoBytesMax));
    }
}

/// <summary>
/// Formatos aceitos para a apresentação de um <see cref="Entities.DocumentoExigido"/>: uma LISTA de
/// <see cref="FormatoPermitidoEntry"/> (cada um com teto por-formato opcional) OU o token
/// <see cref="Qualquer"/>, mutuamente exclusivos. Campo OBRIGATÓRIO em
/// <see cref="Entities.DocumentoExigido"/> — "sem restrição" é o token explícito
/// <c>QUALQUER</c>, nunca <see langword="null"/>, para não haver ambiguidade.
/// </summary>
/// <remarks>
/// Não há variante "ausente": <see cref="Criar"/> sempre devolve um VO completo (nunca nulo) — a
/// exigência declara sempre um dos dois braços.
/// </remarks>
public sealed record FormatosPermitidos
{
    private FormatosPermitidos(bool qualquer, IReadOnlyList<FormatoPermitidoEntry>? lista)
    {
        Qualquer = qualquer;
        Lista = lista;
    }

    /// <summary><see langword="true"/> ⟺ o token <c>QUALQUER</c> — nesse caso <see cref="Lista"/> é sempre <see langword="null"/>.</summary>
    public bool Qualquer { get; }

    /// <summary>Não-nulo ⟺ <see cref="Qualquer"/> é <see langword="false"/>; nunca vazio quando presente.</summary>
    public IReadOnlyList<FormatoPermitidoEntry>? Lista { get; }

    /// <summary>
    /// <paramref name="qualquer"/> e <paramref name="entradas"/> são mutuamente exclusivos —
    /// nenhuma combinação produz um VO "ausente": o campo é obrigatório, e <see cref="Result{T}"/> aqui
    /// só carrega falha ou sucesso completo.
    /// </summary>
    public static Result<FormatosPermitidos> Criar(
        bool qualquer, IReadOnlyList<(string Formato, int? TamanhoMaximoBytesMax)>? entradas)
    {
        if (qualquer)
        {
            return entradas is { Count: > 0 }
                ? Result<FormatosPermitidos>.Failure(new DomainError(
                    "FormatosPermitidos.QualquerComFormatosEspecificos",
                    "QUALQUER não pode conviver com uma lista de formatos específicos — são mutuamente exclusivos."))
                : Result<FormatosPermitidos>.Success(new FormatosPermitidos(true, null));
        }

        return entradas is null || entradas.Count == 0
            ? Result<FormatosPermitidos>.Failure(new DomainError(
                "FormatosPermitidos.Obrigatorio",
                "FormatosPermitidos é obrigatório: declare QUALQUER ou uma lista com ao menos um formato."))
            : CriarLista(entradas);
    }

    private static Result<FormatosPermitidos> CriarLista(IReadOnlyList<(string Formato, int? TamanhoMaximoBytesMax)> entradas)
    {
        List<FormatoPermitidoEntry> lista = [];
        HashSet<FormatoPermitido> vistos = [];
        foreach ((string codigo, int? tamanho) in entradas)
        {
            Result<FormatoPermitidoEntry> resultado = CriarEntrada(codigo, tamanho, vistos);
            if (resultado.IsFailure)
            {
                return Result<FormatosPermitidos>.Failure(resultado.Error!);
            }

            lista.Add(resultado.Value!);
        }

        return Result<FormatosPermitidos>.Success(new FormatosPermitidos(false, lista));
    }

    private static Result<FormatoPermitidoEntry> CriarEntrada(string codigo, int? tamanho, HashSet<FormatoPermitido> vistos)
    {
        if (!CatalogoDe<FormatoPermitido>.TentarObter(codigo, out FormatoPermitido formato))
        {
            return Result<FormatoPermitidoEntry>.Failure(new DomainError(
                "FormatosPermitidos.FormatoInvalido", "Formato não reconhecido."));
        }

        return vistos.Add(formato)
            ? FormatoPermitidoEntry.Criar(formato, tamanho)
            : Result<FormatoPermitidoEntry>.Failure(new DomainError(
                "FormatosPermitidos.FormatoDuplicado", $"O formato '{codigo}' aparece mais de uma vez na lista."));
    }
}
