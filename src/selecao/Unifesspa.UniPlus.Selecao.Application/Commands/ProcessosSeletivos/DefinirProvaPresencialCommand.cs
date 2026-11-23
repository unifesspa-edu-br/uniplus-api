namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ProcessosSeletivos.ProvaPresencial;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

public sealed record DefinirProvaPresencialCommand(
    Guid ProcessoSeletivoId,
    bool TemProvaPresencial,
    IReadOnlyList<MunicipioDeProvaInformado> Municipios) : ICommand<Result>;
