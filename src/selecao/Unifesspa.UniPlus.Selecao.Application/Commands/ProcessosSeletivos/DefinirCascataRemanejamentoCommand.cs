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
/// <remarks>
/// Códigos <c>string?</c> em vez de <c>string</c> (ADR-0125): sem validator garantindo
/// não-nulo a montante, um JSON com <c>modalidadeOrigemCodigo: null</c> em um item da lista
/// precisa chegar a <see cref="Domain.Entities.DestinoRemanejamento.Criar"/> como violação de
/// campo — com o campo não-anulável, o model binding automático do <c>[ApiController]</c>
/// intercepta o <c>null</c> com um 400 genérico do ASP.NET antes de o Wolverine e o domínio
/// chegarem a rodar.
/// </remarks>
public sealed record DestinoRemanejamentoInput(
    string? ModalidadeOrigemCodigo,
    int Ordem,
    string? ModalidadeDestinoCodigo);

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
