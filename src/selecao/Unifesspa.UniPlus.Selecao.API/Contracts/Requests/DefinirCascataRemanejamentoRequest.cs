namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Controllers;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Corpo de <see cref="ProcessoSeletivoController.DefinirCascataRemanejamento"/>
/// — omite <c>ProcessoSeletivoId</c> (vem da rota). Os quatro campos são todos
/// nulos (remove a cascata) ou todos presentes (define) — presença parcial é
/// recusada com <c>ConfiguracaoCascataRemanejamento.CamposObrigatorios</c>.
/// </summary>
public sealed record DefinirCascataRemanejamentoRequest(
    string? RegraCodigo,
    string? RegraVersao,
    string? FallbackCodigo,
    IReadOnlyList<DestinoRemanejamentoInput>? Destinos);
