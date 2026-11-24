namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Define a classificação de um processo a partir das regras e dos pesos cadastrados.</summary>
public static class DefinirClassificacaoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirClassificacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IPesoAreaEnemReader pesoAreaEnemReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(pesoAreaEnemReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        Result<ProcessoSeletivo> obtido = await DefinirBonusRegionalCommandHandler
            .ObterParaEdicaoAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirEGravarAsync(obtido.Value!, command, new CadastrosDaClassificacao(regraCatalogoReader, pesoAreaEnemReader),
                unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirEGravarAsync(
        ProcessoSeletivo processo, DefinirClassificacaoCommand command, CadastrosDaClassificacao cadastros,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result definido = await DefinirAsync(processo, command, cadastros, cancellationToken).ConfigureAwait(false);
        if (definido.IsFailure)
        {
            return definido;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static async Task<Result> DefinirAsync(
        ProcessoSeletivo processo, DefinirClassificacaoCommand command, CadastrosDaClassificacao cadastros, CancellationToken cancellationToken)
    {
        Result<DeclaracaoDaClassificacao> regras = await cadastros.ResolverRegrasAsync(command, cancellationToken).ConfigureAwait(false);
        if (regras.IsFailure)
        {
            return Result.Failure(regras.Error!);
        }

        QuadroDePesosResolvido quadro = await cadastros.ResolverQuadroAsync(regras.Value!.RegraCalculo, command, cancellationToken).ConfigureAwait(false);
        Result<ConfiguracaoClassificacao> configuracao = ConfiguracaoClassificacao.Criar(quadro.DeclaracaoCompleta(regras.Value));
        return DefinirNoProcesso(processo, configuracao, quadro);
    }

    private static Result DefinirNoProcesso(
        ProcessoSeletivo processo, Result<ConfiguracaoClassificacao> configuracao, QuadroDePesosResolvido quadro)
    {
        List<FieldError> erros = quadro.RecusasJuntoCom(configuracao);
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        Result definido = processo.DefinirClassificacao(configuracao.Value!);
        return definido.IsFailure ? Result.ValidationFailure(definido.Errors) : definido;
    }
}
