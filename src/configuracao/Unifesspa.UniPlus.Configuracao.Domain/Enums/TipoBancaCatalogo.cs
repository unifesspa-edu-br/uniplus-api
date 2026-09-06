namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Catálogo do domínio fechado das <b>seis bancas</b> da seleção (UNI-REQ-0139).
/// Fonte única dos códigos aceitos na guarda de domínio (<c>TipoBanca.Criar</c>),
/// no validator e no CHECK de banco <c>ck_tipo_banca_codigo_canonico</c>.
/// </summary>
public static class TipoBancaCatalogo
{
    /// <summary>
    /// As seis bancas, com o rótulo canônico do código — fonte do endpoint de
    /// vocabulário (<c>GET /api/configuracao/vocabularios/tipos-banca</c>). O rótulo
    /// aqui é fixo por código; não confundir com <c>TipoBanca.Nome</c>, campo editável
    /// por instância que prevalece na listagem do cadastro.
    /// </summary>
    public static IReadOnlyList<TipoBancaDescrito> Descritos { get; } =
    [
        new("BANCA_ANALISE_DOCUMENTAL", "Banca de análise documental"),
        new("BANCA_ENTREVISTA", "Banca de entrevista"),
        new("BANCA_CORRECAO_REDACOES", "Banca de correção de redações"),
        new("BANCA_ANALISE_RECURSOS", "Banca de análise de recursos"),
        new("BANCA_HETEROIDENTIFICACAO", "Banca de heteroidentificação"),
        new("BANCA_BIOPSICOSSOCIAL", "Banca de avaliação biopsicossocial"),
    ];

    /// <summary>
    /// Os seis códigos canônicos dos tipos de banca da seleção, derivados de
    /// <see cref="Descritos"/> — uma segunda lista escrita à mão envelheceria sem avisar
    /// quando um código novo entrasse só num dos dois lugares.
    /// </summary>
    public static IReadOnlyList<string> Codigos { get; } = [.. Descritos.Select(static d => d.Codigo)];

    private static readonly HashSet<string> CodigosSet = new(Codigos, StringComparer.Ordinal);

    /// <summary>Indica se <paramref name="codigo"/> pertence ao conjunto canônico das seis bancas.</summary>
    public static bool EhCanonico(string? codigo) =>
        codigo is not null && CodigosSet.Contains(codigo);
}
