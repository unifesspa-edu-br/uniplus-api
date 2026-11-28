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
        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao
            .ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
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
        RegrasCitadasNaClassificacao regras = await cadastros.ResolverRegrasAsync(command, cancellationToken).ConfigureAwait(false);
        QuadroDePesosResolvido quadro = await cadastros.ResolverQuadroAsync(regras.Calculo, command, cancellationToken).ConfigureAwait(false);

        return regras.TodasResolvidas
            ? DefinirNoProcesso(processo, ConfiguracaoClassificacao.Criar(regras.Declaracao(command, quadro)), quadro)
            : Result.ValidationFailure(RecusasDoPedido(regras.Campos(command, quadro), regras, quadro));
    }

    // Os campos primeiro, depois o cadastro de Pesos por Área, depois o catálogo; nada é criado nem gravado.
    private static List<FieldError> RecusasDoPedido(CamposDaClassificacao campos, RegrasCitadasNaClassificacao regras, QuadroDePesosResolvido quadro) =>
    [
        .. quadro.RecusasJuntoCom(ConfiguracaoClassificacao.ConferirCampos(campos)),
        .. regras.Recusas,
    ];

    private static Result DefinirNoProcesso(
        ProcessoSeletivo processo, Result<ConfiguracaoClassificacao> configuracao, QuadroDePesosResolvido quadro)
    {
        List<FieldError> erros = quadro.RecusasJuntoCom(configuracao.IsFailure ? configuracao.Errors : []);
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        Result definido = processo.DefinirClassificacao(configuracao.Value!);
        return definido.IsFailure ? Result.ValidationFailure(definido.Errors) : definido;
    }
}
