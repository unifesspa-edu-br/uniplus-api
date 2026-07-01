namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Catálogo do domínio fechado das <b>quatro bancas</b> da seleção (UNI-REQ-0064).
/// Fonte única dos códigos aceitos na guarda de domínio (<c>TipoBanca.Criar</c>),
/// no validator e no CHECK de banco <c>ck_tipo_banca_codigo_canonico</c>.
/// </summary>
public static class TipoBancaCatalogo
{
    /// <summary>Os quatro códigos canônicos dos tipos de banca da seleção.</summary>
    public static readonly IReadOnlyList<string> Codigos =
    [
        "BANCA_ANALISE_DOCUMENTAL",
        "BANCA_ENTREVISTA",
        "BANCA_CORRECAO_REDACOES",
        "BANCA_ANALISE_RECURSOS",
    ];

    private static readonly HashSet<string> CodigosSet = new(Codigos, StringComparer.Ordinal);

    /// <summary>Indica se <paramref name="codigo"/> pertence ao conjunto canônico das quatro bancas.</summary>
    public static bool EhCanonico(string? codigo) =>
        codigo is not null && CodigosSet.Contains(codigo);
}
