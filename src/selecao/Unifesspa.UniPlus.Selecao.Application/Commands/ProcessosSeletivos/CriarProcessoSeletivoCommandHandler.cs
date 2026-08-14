namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
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
        ITipoProcessoReader tipoProcessoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unidadeReader);
        ArgumentNullException.ThrowIfNull(tipoProcessoReader);
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

        // A cidade da Unidade administradora descreve onde ela fica, e é copiada para o
        // snapshot dela — não é a localidade que rege a contagem de prazos, que o processo
        // declara por conta própria (UNI-REQ-0111). A exigência de a Unidade ter cidade
        // permanece como regra do cadastro dela, e é avaliada aqui porque o snapshot a
        // carrega; a justificativa original, que a apoiava na contagem de prazo, deixou de
        // valer quando a localidade passou a ser declarada.
        if (string.IsNullOrWhiteSpace(unidade.CidadeCodigoIbge))
        {
            return Result<Guid>.Failure(new DomainError(
                "ProcessoSeletivo.UnidadeAdministradoraSemCidade",
                $"Unidade administradora {command.UnidadeAdministradoraOrigemId} não tem cidade cadastrada — obrigatória para criar processo seletivo."));
        }

        // Erro nomeado, e não validação de contrato: é este o código que o catálogo
        // público publica para a causa (uniplus.selecao.processo_seletivo.localidade_ausente).
        // Exigir os campos no validator devolveria uniplus.validacao e o consumidor perderia
        // a página que explica a causa.
        if (command.LocalidadeNaoDeclarada)
        {
            return Result<Guid>.Failure(new DomainError(
                "ProcessoSeletivo.LocalidadeAusente",
                "A localidade que rege a contagem dos prazos é obrigatória para criar o processo seletivo."));
        }

        Result<LocalidadeRegente> localidadeResult = LocalidadeRegente.Criar(
            command.LocalidadeCodigoIbge, command.LocalidadeNome, command.LocalidadeUf);
        if (localidadeResult.IsFailure)
        {
            return Result<Guid>.Failure(localidadeResult.Error!);
        }

        TipoProcessoView? tipo = await tipoProcessoReader
            .ObterAtivoPorIdAsync(command.TipoProcessoOrigemId, cancellationToken)
            .ConfigureAwait(false);
        if (tipo is null)
        {
            return Result<Guid>.Failure(new DomainError(
                "ProcessoSeletivo.TipoProcessoNaoEncontradoOuInativo",
                $"Tipo de processo seletivo {command.TipoProcessoOrigemId} não encontrado ou não está ativo."));
        }

        Result<TipoProcessoSnapshot> tipoSnapshotResult = TipoProcessoSnapshot.Criar(tipo.Id, tipo.Codigo, tipo.Nome);
        if (tipoSnapshotResult.IsFailure)
        {
            return Result<Guid>.Failure(tipoSnapshotResult.Error!);
        }

        Result<UnidadeAdministradoraSnapshot> snapshotResult = UnidadeAdministradoraSnapshot.Criar(
            unidade.Sigla, unidade.Slug, unidade.Nome, unidade.Tipo,
            unidade.CidadeCodigoIbge, unidade.CidadeNome, unidade.CidadeUf);
        if (snapshotResult.IsFailure)
        {
            return Result<Guid>.Failure(snapshotResult.Error!);
        }

        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            command.Nome, tipoSnapshotResult.Value!, command.OrigemCandidatos, unidade.Id, snapshotResult.Value!,
            localidadeResult.Value!);

        await processoSeletivoRepository.AdicionarAsync(processo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(processo.Id);
    }
}
