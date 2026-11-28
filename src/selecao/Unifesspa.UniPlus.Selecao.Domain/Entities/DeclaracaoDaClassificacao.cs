namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o operador (ou o envelope reidratado) declara para a classificação de um processo
/// seletivo, antes de as conferências de <see cref="ConfiguracaoClassificacao.Criar"/> a
/// aceitarem. A fórmula da nota e a ordem de alocação são obrigatórias; o resto tem o valor de
/// quem não declarou nada (nulo, vazio ou falso), exceto que o pedido que não informa se a
/// classificação é baseada em ENEM, ou a lista de eliminações, passa nulo e é recusado por
/// <see cref="ConfiguracaoClassificacao.Criar"/>.
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

    /// <summary>Regras de eliminação; vazia quando não há, e nula quando o pedido não informou a lista.</summary>
    public IReadOnlyList<RegraEliminacao>? RegrasEliminacao { get; init; } = [];

    /// <summary>A classificação usa a estrutura de pontuação por área do ENEM; nulo quando o pedido não informou.</summary>
    public bool? BaseadoEmEnem { get; init; } = false;

    /// <summary>Resolução de Pesos por Área declarada, como o operador a digitou.</summary>
    public string? ResolucaoPesoAreaEnem { get; init; }

    /// <summary>Cópia por valor da resolução declarada, um item por grupo de área.</summary>
    public IReadOnlyList<GrupoPesoAreaEnemCongelado> QuadroPesoAreaEnem { get; init; } = [];

    /// <summary>Escala em que a nota final é publicada; ausente na classificação importada.</summary>
    public decimal? EscalaNotaFinal { get; init; }

    /// <summary>Os campos declarados, como <see cref="ConfiguracaoClassificacao.ConferirCampos"/> os confere.</summary>
    internal CamposDaClassificacao Campos => new()
    {
        RegraCalculo = RegraCalculo,
        ArredondamentoDeclarado = RegraArredondamento is not null,
        CasasArredondamento = CasasArredondamento,
        Eliminacoes = RegrasEliminacao is { } regras ? EliminacoesDeclaradas.DasRegras(regras) : null,
        BaseadoEmEnem = BaseadoEmEnem,
        ResolucaoPesoAreaEnem = ResolucaoPesoAreaEnem,
        QuadroPesoAreaEnem = QuadroPesoAreaEnem,
        EscalaNotaFinal = EscalaNotaFinal,
    };

    /// <summary>
    /// A resolução em NFC, como o cadastro de Pesos por Área a grava e o envelope canônico a
    /// emite: o vínculo com o cadastro é pelo valor, e a forma não muda num ciclo de
    /// retificação. A normalização vem depois da conferência, que recusa o que ela não
    /// conseguiria tratar.
    /// </summary>
    internal string? ResolucaoNormalizada =>
        ConferenciasDoQuadroDePesosPorArea.Aparar(ResolucaoPesoAreaEnem) is { } resolucao ? TextoCongelado.Normalizar(resolucao) : null;
}
