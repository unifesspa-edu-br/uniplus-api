namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Um formato aceito dentro da <see cref="FormatosPermitidos.Lista"/> (Story #918) — sem
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
/// Formatos aceitos para a apresentação de um <see cref="Entities.DocumentoExigido"/> (Story
/// #918) — substitui o campo singular <c>FormatoPermitido?</c> (PR #900): agora uma LISTA de
/// <see cref="FormatoPermitidoEntry"/> (cada um com teto por-formato opcional) OU o token
/// <see cref="Qualquer"/>, mutuamente exclusivos. Campo agora OBRIGATÓRIO em
/// <see cref="Entities.DocumentoExigido"/> — o <see langword="null"/> antigo ("sem
/// restrição") é substituído pelo token explícito <c>QUALQUER</c>, eliminando a ambiguidade.
/// </summary>
/// <remarks>
/// Diferente de <see cref="IdadeMaximaEmissao"/> (regra 0..1, <see langword="null"/> é
/// sucesso "ausente"), aqui não há variante "ausente": <see cref="Criar"/> sempre devolve um
/// VO completo (nunca nulo) — a exigência declara sempre um dos dois braços.
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
    /// nenhuma combinação produz um VO "ausente" (ao contrário de <see cref="IdadeMaximaEmissao.Criar"/>):
    /// o campo é obrigatório, e <see cref="Result{T}"/> aqui só carrega falha ou sucesso completo.
    /// </summary>
    public static Result<FormatosPermitidos> Criar(
        bool qualquer, IReadOnlyList<(string Formato, int? TamanhoMaximoBytesMax)>? entradas)
    {
        if (qualquer)
        {
            if (entradas is { Count: > 0 })
            {
                return Result<FormatosPermitidos>.Failure(new DomainError(
                    "FormatosPermitidos.QualquerComFormatosEspecificos",
                    "QUALQUER não pode conviver com uma lista de formatos específicos — são mutuamente exclusivos."));
            }

            return Result<FormatosPermitidos>.Success(new FormatosPermitidos(true, null));
        }

        if (entradas is null || entradas.Count == 0)
        {
            return Result<FormatosPermitidos>.Failure(new DomainError(
                "FormatosPermitidos.Obrigatorio",
                "FormatosPermitidos é obrigatório: declare QUALQUER ou uma lista com ao menos um formato."));
        }

        List<FormatoPermitidoEntry> lista = [];
        HashSet<FormatoPermitido> formatosVistos = [];
        foreach ((string formatoCodigo, int? tamanhoMaximoBytesMax) in entradas)
        {
            if (FormatoPermitidoCodigo.FromCodigo(formatoCodigo) is not { } formato)
            {
                return Result<FormatosPermitidos>.Failure(new DomainError(
                    "FormatosPermitidos.FormatoInvalido",
                    $"Formato '{formatoCodigo}' não reconhecido."));
            }

            if (!formatosVistos.Add(formato))
            {
                return Result<FormatosPermitidos>.Failure(new DomainError(
                    "FormatosPermitidos.FormatoDuplicado",
                    $"O formato '{formatoCodigo}' aparece mais de uma vez na lista."));
            }

            Result<FormatoPermitidoEntry> entradaResult = FormatoPermitidoEntry.Criar(formato, tamanhoMaximoBytesMax);
            if (entradaResult.IsFailure)
            {
                return Result<FormatosPermitidos>.Failure(entradaResult.Error!);
            }

            lista.Add(entradaResult.Value!);
        }

        return Result<FormatosPermitidos>.Success(new FormatosPermitidos(false, lista));
    }
}
