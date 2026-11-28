namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ProcessosSeletivos.CadastroDeReserva;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Substitui o cadastro de reserva por chamada. A lista é obrigatória: ausente ou nula é recusada (422) pelo domínio, e a
/// lista vazia declara "nenhuma chamada com reserva".
/// </summary>
public sealed record DefinirCadastroDeReservaCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<CadastroDeReservaInformado?>? Chamadas) : ICommand<Result>;
