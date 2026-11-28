namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Application.Commands.ProcessosSeletivos;

using Controllers;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoOfertaEClassificacaoController.DefinirClassificacao"/> —
/// omite <c>ProcessoSeletivoId</c> (vem da rota).
/// </summary>
/// <remarks>
/// <c>BaseadoEmEnem</c> e <c>RegrasEliminacao</c> são anuláveis, sem valor padrão e sem <c>[JsonRequired]</c>:
/// ausentes ou nulos chegam ao domínio e são recusados (422), acumulados com as demais recusas, em vez de virarem
/// "não é baseado em ENEM" ou o 400 da leitura do corpo. O contrato os publica como obrigatórios e sem
/// <c>null</c>; a lista vazia declara que não há eliminação.
/// <para>
/// <c>ResolucaoPesoAreaEnem</c> é opcional no corpo: ausente vale nulo, e é o domínio que
/// decide se ela era obrigatória (classificação baseada em ENEM com cálculo local) ou
/// indevida (as demais).
/// </para>
/// <para>
/// <c>EscalaNotaFinal</c> é opcional no corpo: o rascunho a tolera ausente, e é o gate de
/// publicação que a cobra na classificação por cálculo local.
/// </para>
/// </remarks>
public sealed record DefinirClassificacaoRequest(
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    string? RegraCalculoCodigo,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    string? RegraCalculoVersao,
    string? RegraArredondamentoCodigo,
    string? RegraArredondamentoVersao,
    int? CasasArredondamento,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    string? RegraOrdemAlocacaoCodigo,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    string? RegraOrdemAlocacaoVersao,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422); envie [] quando não houver nenhuma.")]
    IReadOnlyList<RegraEliminacaoInput>? RegrasEliminacao,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    bool? BaseadoEmEnem,
    string? ResolucaoPesoAreaEnem = null,
    decimal? EscalaNotaFinal = null)
{
    /// <summary>O comando que define a classificação do processo, com o corpo recebido.</summary>
    internal DefinirClassificacaoCommand ParaComando(Guid processoSeletivoId) => new(
        processoSeletivoId,
        RegraCalculoCodigo,
        RegraCalculoVersao,
        RegraArredondamentoCodigo,
        RegraArredondamentoVersao,
        CasasArredondamento,
        RegraOrdemAlocacaoCodigo,
        RegraOrdemAlocacaoVersao,
        RegrasEliminacao,
        BaseadoEmEnem,
        ResolucaoPesoAreaEnem,
        EscalaNotaFinal);
}
