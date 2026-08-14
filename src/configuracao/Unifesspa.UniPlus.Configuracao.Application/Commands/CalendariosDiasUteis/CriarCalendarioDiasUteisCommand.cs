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
public sealed record CriarCalendarioDiasUteisCommand(
    string VersaoDataset,
    IReadOnlyList<DiaNaoUtilCommandItem> DiasNaoUteis) : ICommand<Result<Guid>>;
