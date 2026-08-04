namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Governance.Contracts;

using Wolverine.Attributes;

/// <summary>
/// Handler convention-based do <see cref="CriarProcessoSeletivoCommand"/>:
/// resolve a Unidade administradora viva via <see cref="IUnidadeReader"/> (ADR-0056),
/// congela por snapshot-copy (<see cref="UnidadeAdministradoraSnapshot"/>, ADR-0061),
/// cria o agregado-raiz em rascunho, persiste via
/// <see cref="IProcessoSeletivoRepository"/> e retorna o id.
/// </summary>
public static class CriarProcessoSeletivoCommandHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c> necessário porque este handler injeta <see cref="IUnidadeReader"/>
    /// (Organização Institucional, dependente de <c>OrganizacaoInstitucionalDbContext</c>) junto de
    /// <see cref="ISelecaoUnitOfWork"/> (Seleção, dependente de <c>SelecaoDbContext</c>) — o mesmo
    /// problema que <c>CriarOfertaCursoCommandHandler</c> (Configuração), primeiro consumidor
    /// cross-módulo de <see cref="IUnidadeReader"/>, já resolveu: o detector de transação do
    /// Wolverine.EntityFrameworkCore (<c>AutoApplyTransactions</c>) enumera as dependências
    /// transitivas dos parâmetros do handler em busca de um único <c>DbContext</c> a enrolar na
    /// transação do outbox — encontraria dois e falharia por ambiguidade no boot do host. A
    /// persistência continua correta sem o enrolamento automático: o handler chama
    /// <see cref="ISelecaoUnitOfWork.SalvarAlteracoesAsync"/> explicitamente, e
    /// <see cref="ProcessoSeletivo.Criar"/> não levanta domain event — não há atomicidade
    /// write+evento de outbox a perder (ADR-0004 não se aplica aqui). O reader é só leitura, sem
    /// participação na transação de escrita.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<Guid>> Handle(
        CriarProcessoSeletivoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IUnidadeReader unidadeReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unidadeReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        UnidadeView? unidade = await unidadeReader
            .ObterPorIdAsync(command.UnidadeAdministradoraOrigemId, cancellationToken)
            .ConfigureAwait(false);
        if (unidade is null)
        {
            return Result<Guid>.Failure(new DomainError(
                "ProcessoSeletivo.UnidadeAdministradoraNaoEncontrada",
                $"Unidade administradora {command.UnidadeAdministradoraOrigemId} não encontrada ou não está mais viva."));
        }

        Result<UnidadeAdministradoraSnapshot> snapshotResult = UnidadeAdministradoraSnapshot.Criar(
            unidade.Sigla, unidade.Slug, unidade.Nome, unidade.Tipo);
        if (snapshotResult.IsFailure)
        {
            return Result<Guid>.Failure(snapshotResult.Error!);
        }

        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            command.Nome, command.Tipo, command.OrigemCandidatos, unidade.Id, snapshotResult.Value!);

        await processoSeletivoRepository.AdicionarAsync(processo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(processo.Id);
    }
}
