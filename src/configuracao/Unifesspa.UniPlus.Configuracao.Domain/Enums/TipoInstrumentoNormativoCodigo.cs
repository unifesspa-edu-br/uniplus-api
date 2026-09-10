namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Mapeamento único entre <see cref="TipoInstrumentoNormativo"/> e o código textual canônico
/// UPPER_SNAKE do wire — fonte de verdade única do wire format, evitando que o front-end
/// mantenha cópia local do vocabulário.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Descritos"/> é derivado do enum, nunca escrito à mão: um tipo novo entra no
/// vocabulário ao ser declarado em <see cref="TipoInstrumentoNormativo"/>, e a ausência do
/// rótulo correspondente em <see cref="NomeDe"/> ou <see cref="DescricaoDe"/> explode na
/// primeira leitura — em vez de vender um vocabulário silenciosamente incompleto.
/// </para>
/// <para>
/// <see cref="TipoInstrumentoNormativo.Nenhum"/> é explicitamente excluído de
/// <see cref="Descritos"/>: é sentinela de ausência, não um tipo referenciável por
/// uma Base Legal.
/// </para>
/// </remarks>
public static class TipoInstrumentoNormativoCodigo
{
    public const string Lei = "LEI";
    public const string Decreto = "DECRETO";
    public const string Portaria = "PORTARIA";
    public const string Resolucao = "RESOLUCAO";
    public const string InstrucaoNormativa = "INSTRUCAO_NORMATIVA";
    public const string Parecer = "PARECER";

    /// <summary>
    /// Os seis tipos referenciáveis, na ordem de declaração do enum. A lista é derivada de
    /// <see cref="TipoInstrumentoNormativo"/>, não escrita à mão: um tipo novo entra aqui ao
    /// ser declarado no enum, e a ausência do rótulo correspondente aparece como exceção na
    /// primeira leitura, em vez de virar um tipo que a API deixa de anunciar.
    /// </summary>
    public static IReadOnlyList<TipoInstrumentoNormativoDescrito> Descritos { get; } =
    [
        .. Enum.GetValues<TipoInstrumentoNormativo>()
            .Where(static t => t != TipoInstrumentoNormativo.Nenhum)
            .Select(static t => new TipoInstrumentoNormativoDescrito(t.ToCodigo(), NomeDe(t), DescricaoDe(t))),
    ];

    /// <summary>
    /// Os seis códigos canônicos, derivados de <see cref="Descritos"/> — uma segunda lista
    /// escrita à mão envelheceria sem avisar quando um código novo entrasse só num dos dois
    /// lugares.
    /// </summary>
    public static IReadOnlyList<string> Codigos { get; } = [.. Descritos.Select(static d => d.Codigo)];

    /// <summary>Os códigos aceitos em texto corrido, para compor mensagens de validação.</summary>
    public static string CodigosEmTexto { get; } = string.Join(", ", Codigos);

    private static string NomeDe(TipoInstrumentoNormativo tipo) => tipo switch
    {
        TipoInstrumentoNormativo.Lei => "Lei",
        TipoInstrumentoNormativo.Decreto => "Decreto",
        TipoInstrumentoNormativo.Portaria => "Portaria",
        TipoInstrumentoNormativo.Resolucao => "Resolução",
        TipoInstrumentoNormativo.InstrucaoNormativa => "Instrução Normativa",
        TipoInstrumentoNormativo.Parecer => "Parecer",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoInstrumentoNormativo sem rótulo declarado."),
    };

    private static string DescricaoDe(TipoInstrumentoNormativo tipo) => tipo switch
    {
        TipoInstrumentoNormativo.Lei =>
            "Ato normativo de maior hierarquia no ordenamento infraconstitucional, emanado do Poder Legislativo.",
        TipoInstrumentoNormativo.Decreto =>
            "Ato normativo do Poder Executivo que regulamenta leis ou dispõe sobre matéria de sua competência.",
        TipoInstrumentoNormativo.Portaria =>
            "Ato administrativo de autoridade pública que disciplina procedimentos internos ou matéria de competência específica.",
        TipoInstrumentoNormativo.Resolucao =>
            "Ato normativo de órgão colegiado (conselho, câmara, comitê) com efeitos internos ou externos conforme a norma instituidora.",
        TipoInstrumentoNormativo.InstrucaoNormativa =>
            "Ato administrativo de caráter técnico-operacional que detalha a execução de normas superiores.",
        TipoInstrumentoNormativo.Parecer =>
            "Manifestação técnica ou jurídica de órgão competente que orienta a aplicação de normas — vinculante quando aprovado pela autoridade competente.",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoInstrumentoNormativo sem descrição declarada."),
    };

    /// <summary>
    /// Converte <paramref name="tipo"/> para o código canônico UPPER_SNAKE.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Lançado para <see cref="TipoInstrumentoNormativo.Nenhum"/>, que é sentinela de ausência
    /// e não possui código canônico.
    /// </exception>
    public static string ToCodigo(this TipoInstrumentoNormativo tipo) => tipo switch
    {
        TipoInstrumentoNormativo.Lei => Lei,
        TipoInstrumentoNormativo.Decreto => Decreto,
        TipoInstrumentoNormativo.Portaria => Portaria,
        TipoInstrumentoNormativo.Resolucao => Resolucao,
        TipoInstrumentoNormativo.InstrucaoNormativa => InstrucaoNormativa,
        TipoInstrumentoNormativo.Parecer => Parecer,
        TipoInstrumentoNormativo.Nenhum => throw new ArgumentOutOfRangeException(
            nameof(tipo), tipo, "TipoInstrumentoNormativo.Nenhum é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoInstrumentoNormativo desconhecido."),
    };

    /// <summary>
    /// Converte um código canônico para <see cref="TipoInstrumentoNormativo"/>.
    /// Devolve <see cref="TipoInstrumentoNormativo.Nenhum"/> para código desconhecido ou nulo.
    /// </summary>
    public static TipoInstrumentoNormativo FromCodigo(string? codigo) => codigo switch
    {
        Lei => TipoInstrumentoNormativo.Lei,
        Decreto => TipoInstrumentoNormativo.Decreto,
        Portaria => TipoInstrumentoNormativo.Portaria,
        Resolucao => TipoInstrumentoNormativo.Resolucao,
        InstrucaoNormativa => TipoInstrumentoNormativo.InstrucaoNormativa,
        Parecer => TipoInstrumentoNormativo.Parecer,
        _ => TipoInstrumentoNormativo.Nenhum,
    };
}
