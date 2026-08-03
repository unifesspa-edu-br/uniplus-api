namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Uma posição da fila legal de remanejamento, usada por
/// <see cref="DefinirCascataRemanejamentoCommand"/> — espelha
/// <see cref="Domain.Entities.DestinoRemanejamento"/>, mas como entrada de
/// borda (RN-CASCATA-4 é validada pela factory, não aqui).
/// </summary>
public sealed record DestinoRemanejamentoInput(
    string ModalidadeOrigemCodigo,
    int Ordem,
    string ModalidadeDestinoCodigo);

/// <summary>
/// Define (ou remove) a cascata de remanejamento do processo (RN-CASCATA-1..5,
/// Story #575). Os quatro campos são todos nulos (remove) ou todos presentes
/// (define) — presença parcial é recusada com
/// <c>ConfiguracaoCascataRemanejamento.CamposObrigatorios</c>.
/// </summary>
public sealed record DefinirCascataRemanejamentoCommand(
    Guid ProcessoSeletivoId,
    string? RegraCodigo,
    string? RegraVersao,
    string? FallbackCodigo,
    IReadOnlyList<DestinoRemanejamentoInput>? Destinos,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
