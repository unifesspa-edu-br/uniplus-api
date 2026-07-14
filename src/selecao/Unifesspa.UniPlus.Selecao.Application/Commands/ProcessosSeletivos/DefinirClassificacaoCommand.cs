namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Kernel.Results;
using Domain.ValueObjects;

/// <summary>
/// Item de entrada de uma regra de eliminação, usado por
/// <see cref="DefinirClassificacaoCommand"/>. Apenas o(s) campo(s) relevante(s)
/// ao código da regra é(são) preenchido(s):
/// <list type="bullet">
///   <item><description><c>ELIM-NOTA-MINIMA-ETAPA</c>: <see cref="EtapaRef"/> (deve existir no processo, INV-B4) + <see cref="NotaMinima"/>.</description></item>
///   <item><description><c>ELIM-CORTE-REDACAO</c>: <see cref="Minimo"/>.</description></item>
///   <item><description><c>ELIM-ZERO-EM-AREA</c>: nenhum.</description></item>
/// </list>
/// </summary>
public sealed record RegraEliminacaoInput(
    string RegraCodigo,
    string RegraVersao,
    Guid? EtapaRef,
    decimal? NotaMinima,
    decimal? Minimo);

/// <summary>
/// Define (ou substitui) a configuração de classificação do processo (Story
/// #775, modelagem P-B §2.1) — o 15º bloco canônico, que compõe por
/// referência a fórmula da nota, a precisão, a lista de eliminação e a ordem
/// de alocação. Bônus e desempate não são parâmetros aqui: já são dimensões
/// do próprio agregado (Story #774).
/// </summary>
public sealed record DefinirClassificacaoCommand(
    Guid ProcessoSeletivoId,
    string RegraCalculoCodigo,
    string RegraCalculoVersao,
    string? RegraArredondamentoCodigo,
    string? RegraArredondamentoVersao,
    int? CasasArredondamento,
    string RegraOrdemAlocacaoCodigo,
    string RegraOrdemAlocacaoVersao,
    int NOpcoesAlocacao,
    IReadOnlyList<RegraEliminacaoInput> RegrasEliminacao,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
