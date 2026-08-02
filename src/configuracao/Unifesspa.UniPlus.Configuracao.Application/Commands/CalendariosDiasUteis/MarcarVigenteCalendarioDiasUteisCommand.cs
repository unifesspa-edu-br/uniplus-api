namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Torna o dataset <paramref name="Id"/> o vigente, desmarcando o vigente anterior
/// (se houver) na mesma transação — no máximo um dataset é vigente por vez.
/// </summary>
public sealed record MarcarVigenteCalendarioDiasUteisCommand(Guid Id) : ICommand<Result>;
