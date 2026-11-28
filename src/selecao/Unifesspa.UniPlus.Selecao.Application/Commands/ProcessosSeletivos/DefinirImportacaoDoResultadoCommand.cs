namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ProcessosSeletivos.ImportacaoDoResultado;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

public sealed record DefinirImportacaoDoResultadoCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<RelatorioInformado>? Relatorios,
    Guid? PrimeiraFaseDepoisDaImportacaoId) : ICommand<Result>;
