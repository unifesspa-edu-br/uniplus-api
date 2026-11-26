namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o operador (ou o envelope reidratado) declara para a classificação de um processo
/// seletivo, antes de as conferências de <see cref="ConfiguracaoClassificacao.Criar"/> a
/// aceitarem. A fórmula da nota e a ordem de alocação são obrigatórias; o resto tem o valor de
/// quem não declarou nada (nulo, vazio ou falso).
/// </summary>
public sealed record DeclaracaoDaClassificacao
{
    /// <summary>Fórmula da nota final.</summary>
    public required ReferenciaRegra RegraCalculo { get; init; }

    /// <summary>Regra de precisão da nota; ausente na classificação importada.</summary>
    public ReferenciaRegra? RegraArredondamento { get; init; }

    /// <summary>Casas decimais da precisão; ausentes na classificação importada.</summary>
    public int? CasasArredondamento { get; init; }

    /// <summary>Ordem de alocação entre a 1ª e a 2ª opção de curso.</summary>
    public required ReferenciaRegra RegraOrdemAlocacao { get; init; }

    /// <summary>Regras de eliminação; vazia quando não há.</summary>
    public IReadOnlyList<RegraEliminacao> RegrasEliminacao { get; init; } = [];

    /// <summary>A classificação usa a estrutura de pontuação por área do ENEM.</summary>
    public bool BaseadoEmEnem { get; init; }

    /// <summary>Resolução de Pesos por Área declarada, como o operador a digitou.</summary>
    public string? ResolucaoPesoAreaEnem { get; init; }

    /// <summary>Cópia por valor da resolução declarada, um item por grupo de área.</summary>
    public IReadOnlyList<GrupoPesoAreaEnemCongelado> QuadroPesoAreaEnem { get; init; } = [];

    /// <summary>Escala em que a nota final é publicada; ausente na classificação importada.</summary>
    public decimal? EscalaNotaFinal { get; init; }

    /// <summary>A resolução sem as bordas em branco, ou nula quando ela é só branco.</summary>
    internal string? ResolucaoAparada =>
        string.IsNullOrWhiteSpace(ResolucaoPesoAreaEnem) ? null : ResolucaoPesoAreaEnem.Trim();

    /// <summary>
    /// A resolução em NFC, como o cadastro de Pesos por Área a grava e o envelope canônico a
    /// emite: o vínculo com o cadastro é pelo valor, e a forma não muda num ciclo de
    /// retificação. A normalização vem depois da conferência, que recusa o que ela não
    /// conseguiria tratar.
    /// </summary>
    internal string? ResolucaoNormalizada =>
        ResolucaoAparada is { } resolucao ? TextoCongelado.Normalizar(resolucao) : null;
}
