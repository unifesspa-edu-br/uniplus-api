namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Desempate;
using Domain.ProcessosSeletivos.Referencias;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Handler do <see cref="DefinirCriteriosDesempateCommand"/>. Lê os cadastros (<see cref="CadastrosDoDesempate"/>)
/// antes de bloquear o processo; depois do 404 e do teto, resolve cada critério no catálogo já lido, monta os
/// parâmetros tipados conforme o código da regra e congela a referência — a existência do <c>etapaRef</c> no processo
/// é garantida pela raiz (<see cref="ProcessoSeletivo.DefinirCriteriosDesempate"/>). Quando algum critério referencia
/// <c>DESEMPATE_CONDICAO_SOBRE_DADO</c>, a condição é validada contra o vocabulário fechado de fatos do candidato
/// (ADR-0111). As recusas de todos os critérios se acumulam, cada uma no campo do critério, granularidade nunca coberta
/// pelo FluentValidation.
/// </summary>
public static class DefinirCriteriosDesempateCommandHandler
{
    public static async Task<Result> Handle(
        DefinirCriteriosDesempateCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        CadastrosDoDesempate cadastros = await CadastrosDoDesempate
            .LerAsync(command.Criterios, regraCatalogoReader, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
        Result<ProcessoSeletivo> obtido = await ObterParaEdicaoAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirEGravarAsync(obtido.Value!, command, cadastros, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // Fora do rascunho a recusa vem antes de consultar cadastros.
    private static async Task<Result<ProcessoSeletivo>> ObterParaEdicaoAsync(
        IProcessoSeletivoRepository repositorio, Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await repositorio.ObterParaMutacaoAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (processo is null)
        {
            return Result<ProcessoSeletivo>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(processoSeletivoId));
        }

        return processo.MutacaoBloqueada() is { } bloqueio
            ? Result<ProcessoSeletivo>.Failure(bloqueio)
            : Result<ProcessoSeletivo>.Success(processo);
    }

    private static async Task<Result> DefinirEGravarAsync(
        ProcessoSeletivo processo,
        DefinirCriteriosDesempateCommand command,
        CadastrosDoDesempate cadastros,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result definido = await DefinirAsync(processo, command, cadastros, cancellationToken).ConfigureAwait(false);
        if (definido.IsFailure)
        {
            return definido;
        }

        // Agregado tracked: persistência por change detection (ValueGeneratedNever
        // nos filhos) — não chamar DbSet.Update.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static async Task<Result> DefinirAsync(
        ProcessoSeletivo processo, DefinirCriteriosDesempateCommand command, CadastrosDoDesempate cadastros, CancellationToken cancellationToken)
    {
        // Acima do teto a lista não é lida item a item: o validator não confere os itens dela,
        // e a recusa do agregado é a única resposta.
        List<FieldError> quantidade = RegrasDoDesempate.ConferirQuantidadeDeCriteriosDesempate(command.Criterios.Count);
        if (quantidade.Count > 0)
        {
            return Result.ValidationFailure(quantidade);
        }

        CriteriosResolvidos resolvidos = await cadastros.ResolverAsync(command.Criterios, cancellationToken).ConfigureAwait(false);
        return DefinirNoProcesso(processo, resolvidos, cadastros.Fatos);
    }

    private static Result DefinirNoProcesso(ProcessoSeletivo processo, CriteriosResolvidos resolvidos, FatosDoDesempate? fatos)
    {
        if (resolvidos.Recusas.Count > 0)
        {
            return RecusaDosCriterios(processo, resolvidos);
        }

        Result vinculoNovo = ConferirVinculosNovos(processo, resolvidos.Criados, fatos);
        if (vinculoNovo.IsFailure)
        {
            return vinculoNovo;
        }

        Result definido = processo.DefinirCriteriosDesempate(resolvidos.Criados);
        return definido.IsFailure ? Result.ValidationFailure(definido.Errors) : definido;
    }

    // O que não chega a ser criado também segue para a conferência contra o processo.
    private static Result RecusaDosCriterios(ProcessoSeletivo processo, CriteriosResolvidos resolvidos)
    {
        List<FieldError> erros =
        [
            .. resolvidos.Recusas,
            .. RegrasDoDesempate.ConferirCriteriosDesempate(processo.Declaracao(), resolvidos.Informados),
        ];
        return Result.ValidationFailure(RegrasDoDesempate.AnexarAreasAceitasDoDesempate(processo.Declaracao(), erros));
    }

    private static Result ConferirVinculosNovos(ProcessoSeletivo processo, IReadOnlyList<CriterioDesempate> criterios, FatosDoDesempate? fatos)
    {
        if (fatos is null)
        {
            return Result.Success();
        }

        Result vinculoNovo = ConferenciaDeVinculoNovo.Conferir(
            fatos.Catalogo,
            ReferenciasVivas.Vinculos(processo.Declaracao()),
            VinculosDeFatos.De(
                [],
                criterios.Select(static c => c.Parametros).OfType<ParametrosDesempatePredicadoFato>()
                    .Select(static a => (a.Condicao.Fato, a.Condicao.Valor))));
        return vinculoNovo.IsFailure ? Result.Failure(vinculoNovo.Error!) : vinculoNovo;
    }
}
