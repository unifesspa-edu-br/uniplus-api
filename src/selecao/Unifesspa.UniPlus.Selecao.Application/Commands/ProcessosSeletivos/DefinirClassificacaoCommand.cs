namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Item de entrada de uma regra de eliminação, usado por
/// <see cref="DefinirClassificacaoCommand"/>. Apenas o(s) campo(s) relevante(s)
/// ao código da regra é(são) preenchido(s):
/// <list type="bullet">
///   <item><description><c>ELIM-NOTA-MINIMA-ETAPA</c>: <see cref="EtapaRef"/> (deve existir no processo, INV-B4) + <see cref="NotaMinima"/>.</description></item>
///   <item><description><c>ELIM-CORTE-EM-AREA</c>: <see cref="AreaCodigo"/> (área do quadro de pesos por área) + <see cref="Minimo"/>.</description></item>
///   <item><description><c>ELIM-ZERO-EM-AREA</c>: nenhum.</description></item>
///   <item><description><c>ELIM-FALTA-EM-DIA-DE-PROVA-ENEM</c>: nenhum.</description></item>
/// </list>
/// </summary>
public sealed record RegraEliminacaoInput(
    string RegraCodigo,
    string RegraVersao,
    Guid? EtapaRef,
    decimal? NotaMinima,
    decimal? Minimo,
    string? AreaCodigo);

/// <summary>
/// Define (ou substitui) a configuração de classificação do processo (Story
/// #775) — o 15º bloco canônico, que compõe por
/// referência a fórmula da nota, a precisão, a lista de eliminação e a ordem
/// de alocação. Bônus e desempate não são parâmetros aqui: já são dimensões
/// do próprio agregado (Story #774).
/// </summary>
/// <param name="ResolucaoPesoAreaEnem">
/// Resolução de Pesos por Área que a classificação usa — obrigatória na classificação
/// baseada em ENEM com cálculo local, recusada nas demais. O handler a resolve no cadastro
/// e congela o quadro por cópia.
/// </param>
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
    bool BaseadoEmEnem,
    string? ResolucaoPesoAreaEnem,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
