namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Monta as etapas com as recusas acumuladas por etapa (ADR-0125) e entrega ao processo, que confere
/// a estrutura do formulário, a fase, os itens já definidos e os fatos que a exibição de cada seção
/// cita. A semântica da exibição (fato do catálogo, operador e valor do domínio) é conferida aqui,
/// contra o catálogo.
/// </summary>
public static class DefinirFormularioCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirFormularioCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        // A precondição de concorrência precede a validação do payload (ADR-0110 D9).
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        IReadOnlyList<EtapaFormularioInput> entradas = command.Etapas ?? [];
        EtapasLidas lidas = EscritaDasEtapas.Ler(entradas);

        // A semântica das exibições vem do catálogo, lido só quando alguma seção tem exibição.
        IReadOnlyList<FatoCandidatoView> catalogo = lidas.Exibicoes.Any(static e => e is not null)
            ? await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false)
            : [];
        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, catalogo);
        List<FieldError> erros = [.. lidas.Erros, .. EscritaDasEtapas.ConferirExibicoes(lidas, contexto)];
        List<EtapaFormulario> etapas = [.. lidas.Etapas];

        if (erros.Count > 0)
        {
            // As recusas do formulário somam-se às das etapas (ADR-0125). A estrutura só é conferida
            // com todas as etapas montadas, e a exibição já recusada pela semântica não é recusada
            // de novo pela posição.
            HashSet<string> recusados = [.. erros.Select(static e => e.Field ?? string.Empty)];
            erros.AddRange(processo
                .ConferirFormulario(command.Finalidade, command.FaseId, command.Titulo, etapas, etapasCompletas: etapas.Count == entradas.Count)
                .Where(recusa => recusa.Field is not { } campo
                    || !campo.EndsWith(".exibicao", StringComparison.Ordinal)
                    || !recusados.Contains(campo)));
            return Result<MutacaoAceita>.ValidationFailure(erros);
        }

        Result vinculoNovo = ConferenciaDeVinculoNovo.Conferir(
            contexto.Fatos,
            processo.Vinculos(),
            VinculosDeFatos.De([], etapas.SelectMany(static e => e.Condicoes).Select(static c => (c.Fato, c.Valor))));
        if (vinculoNovo.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(vinculoNovo.Error!);
        }

        Result definir = processo.DefinirFormulario(command.Finalidade, command.FaseId, command.Titulo, etapas, command.Precondicao);
        if (definir.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(definir.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    internal static DomainError ProcessoNaoEncontrado(Guid id) =>
        new("ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {id} não encontrado.");
}

/// <summary>Remove o formulário da finalidade; a regra de só em rascunho é do processo.</summary>
public static class RemoverFormularioCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        RemoverFormularioCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        Result remover = processo.RemoverFormulario(command.Finalidade, command.Precondicao);
        if (remover.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(remover.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
