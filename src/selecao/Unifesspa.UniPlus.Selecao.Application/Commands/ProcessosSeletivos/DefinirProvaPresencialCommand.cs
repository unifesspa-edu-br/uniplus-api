namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ProcessosSeletivos.ProvaPresencial;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Declara se o processo tem prova presencial e os municípios de prova. Os dois campos são obrigatórios: ausentes ou
/// nulos chegam ao domínio e são recusados (422); a lista vazia declara "nenhum município".
/// </summary>
public sealed record DefinirProvaPresencialCommand(
    Guid ProcessoSeletivoId,
    bool? TemProvaPresencial,
    IReadOnlyList<MunicipioDeProvaInformado>? Municipios) : ICommand<Result>;
