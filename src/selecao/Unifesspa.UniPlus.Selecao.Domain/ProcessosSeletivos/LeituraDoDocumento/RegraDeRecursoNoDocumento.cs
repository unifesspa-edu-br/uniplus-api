namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A regra de recurso da fase como o documento a traz: a regra do catálogo, o ato âncora do prazo, os parâmetros do prazo e os
/// procedimentos. O parâmetro que o documento não traz vai no valor de partida do tipo, e quem o confere é o domínio.
/// </summary>
public sealed record RegraDeRecursoNoDocumento(
    ReferenciaDeRegraNoDocumento Regra,
    string? AtoAncoraCodigo,
    ParametrosRegraPrazoRecurso Prazo,
    IReadOnlyList<ProcedimentoNoDocumento> Procedimentos);

/// <summary>Um procedimento de recurso, com a posição dele na lista enviada: o tipo de decisão, o alvo opcional e a regra do catálogo.</summary>
public sealed record ProcedimentoNoDocumento(
    int PosicaoNoDocumento,
    TipoMotivoDeDecisao TipoDeDecisao,
    AlvoNoDocumento? Alvo,
    ReferenciaDeRegraNoDocumento Regra);

/// <summary>O alvo de um procedimento como o documento o traz: o item de avaliação pela chave ou a área do ENEM pelo código.</summary>
public sealed record AlvoNoDocumento(string? ItemChave, string? AreaDoEnem);
