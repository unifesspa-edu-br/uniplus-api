namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Define as exigências após conferir as declarações e consultar os cadastros necessários.</summary>
public static class DefinirDocumentosExigidosCommandHandler
{
    public static async Task<Result> Handle(
        DefinirDocumentosExigidosCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ITipoDocumentoReader tipoDocumentoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await DefinirBonusRegionalCommandHandler
            .ObterParaEdicaoAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        CadastrosDosDocumentosExigidos cadastros = new(tipoDocumentoReader, fatoCandidatoReader, modeloDeDocumentoRepository);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirAsync(command, obtido.Value!, cadastros, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirAsync(
        DefinirDocumentosExigidosCommand command,
        ProcessoSeletivo processo,
        CadastrosDosDocumentosExigidos cadastros,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result definido = await DefinicaoDosDocumentosExigidos.DefinirAsync(processo, command.RegrasDeComprovacao, [], cadastros, cancellationToken)
            .ConfigureAwait(false);
        if (definido.IsFailure)
        {
            return Result.ValidationFailure(definido.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
