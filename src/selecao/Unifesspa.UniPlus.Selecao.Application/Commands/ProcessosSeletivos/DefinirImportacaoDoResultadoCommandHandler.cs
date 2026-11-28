namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

public static class DefinirImportacaoDoResultadoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirImportacaoDoResultadoCommand command, IProcessoSeletivoRepository repositorio,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao
            .ObterAsync(repositorio, command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirAsync(obtido.Value!, command, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirAsync(
        ProcessoSeletivo processo, DefinirImportacaoDoResultadoCommand command, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result resultado = processo.DefinirImportacaoDoResultado(command.Relatorios, command.PrimeiraFaseDepoisDaImportacaoId);
        return resultado.IsFailure
            ? Result.ValidationFailure([.. resultado.Errors.Select(NoCampoDoPedido)])
            : await DefinirProvaPresencialCommandHandler.SalvarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // O domínio aponta os campos do bloco "importacao" do documento; o pedido traz os relatórios e a fase na raiz do corpo.
    private static FieldError NoCampoDoPedido(FieldError causa) =>
        causa.Field switch
        {
            "importacao.primeiraFaseDepoisDaImportacao" => causa with { Field = "primeiraFaseDepoisDaImportacaoId" },
            { } campo when campo.StartsWith("importacao.", StringComparison.Ordinal) => causa with { Field = campo["importacao.".Length..] },
            _ => causa,
        };
}
