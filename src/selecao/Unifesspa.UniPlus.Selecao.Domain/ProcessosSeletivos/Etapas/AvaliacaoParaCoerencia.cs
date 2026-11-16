namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que a etapa declara sobre como a nota é obtida, no que a coerência da avaliação confere. Itens e
/// escala chegam só como presença: a coerência pergunta se existem, e quem os valida é a conferência
/// da declaração. A regra de consolidação chega só pelo código, para a Application poder rodar a
/// coerência de todas as etapas antes de ler o catálogo.
/// </summary>
public sealed record AvaliacaoParaCoerencia(
    bool NotaDeOrigemNoEnem,
    FormaDeAvaliacao? Forma = null,
    string? RegraConsolidacaoCodigo = null,
    decimal? PassoDoLancamento = null,
    bool DeclaraItens = false,
    bool DeclaraEscala = false);
