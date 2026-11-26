namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ProcessosSeletivos.CadastroDeReserva;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

public sealed record DefinirCadastroDeReservaCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<CadastroDeReservaInformado?> Chamadas) : ICommand<Result>;
