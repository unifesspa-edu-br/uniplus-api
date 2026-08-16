namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um dataset de calendário de dias úteis (UNI-REQ-0116): a versão do dataset
/// e a lista completa de dias não úteis. Nasce sempre não vigente — tornar-se o
/// dataset corrente é o comando <c>MarcarVigenteCalendarioDiasUteisCommand</c>,
/// separado. O ator de auditoria (<c>created_by</c>) é carimbado server-side via
/// <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>VersaoDataset</c> e os itens de <c>DiasNaoUteis</c> são nuláveis (ADR-0125):
/// sem validator FluentValidation garantindo não-nulo a montante, um payload com
/// campo ausente ou item <c>null</c> na lista (ex.: <c>"diasNaoUteis":[null]</c>)
/// precisa chegar ao domínio para virar 422, não travar em 400 de model binding
/// nem lançar <see cref="NullReferenceException"/> ao montar o agregado.
/// </remarks>
public sealed record CriarCalendarioDiasUteisCommand(
    string? VersaoDataset,
    IReadOnlyList<DiaNaoUtilCommandItem?>? DiasNaoUteis) : ICommand<Result<Guid>>;
