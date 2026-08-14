namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirAlgoritmoContagemPrazoCommand"/> (UNI-REQ-0112): resolve o
/// par declarado contra o rol de regras e delega ao domínio a identidade completa.
/// </summary>
/// <remarks>
/// A referência é montada a partir dos valores <b>resolvidos</b> do catálogo — nunca
/// ecoados do payload —, o mesmo caminho que as demais dimensões usam ao aplicar uma
/// regra. Assim o hash congelado é, por construção, o da definição efetivamente aplicada.
/// </remarks>
public static class DefinirAlgoritmoContagemPrazoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirAlgoritmoContagemPrazoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        if (command.AlgoritmoNaoDeclarado)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.AlgoritmoContagemPrazoNaoDeclarado",
                "A convenção de contagem dos prazos é obrigatória — informe código e versão."));
        }

        RegraCatalogo? regraCatalogo = await regraCatalogoReader
            .ObterAsync(command.Codigo!, command.Versao!, cancellationToken)
            .ConfigureAwait(false);
        if (regraCatalogo is null || regraCatalogo.Tipo != TipoRegra.AlgoritmoContagemPrazo)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.AlgoritmoContagemPrazoNaoEncontrado",
                $"A regra {command.Codigo}/{command.Versao} não é uma entrada de algoritmo de contagem do rol_de_regras."));
        }

        Result<ReferenciaRegra> referenciaResult = ReferenciaRegra.Criar(
            regraCatalogo.Codigo, regraCatalogo.Versao, regraCatalogo.Hash);
        if (referenciaResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(referenciaResult.Error!);
        }

        Result definirResult = processo.DefinirAlgoritmoContagemPrazo(referenciaResult.Value!, command.Precondicao);
        if (definirResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
