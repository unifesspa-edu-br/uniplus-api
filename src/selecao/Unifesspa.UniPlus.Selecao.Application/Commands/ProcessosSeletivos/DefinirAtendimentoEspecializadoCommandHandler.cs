namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirAtendimentoEspecializadoCommand"/> (CA-06 da Story
/// #758): resolve cada dimensão nos cadastros vivos do módulo Configuração
/// (<see cref="ICondicaoAtendimentoReader"/>/<see cref="IRecursoAcessibilidadeReader"/>/<see cref="ITipoDeficienciaReader"/>,
/// ADR-0056), congela por valor (snapshot-copy, ADR-0061) e monta o atendimento especializado —
/// a invariante ADR-0067 (tipo de deficiência só sob condição PcD) é
/// garantida por <see cref="AtendimentoEspecializado.Criar"/>.
/// </summary>
public static class DefinirAtendimentoEspecializadoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirAtendimentoEspecializadoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICondicaoAtendimentoReader condicaoAtendimentoReader,
        IRecursoAcessibilidadeReader recursoAcessibilidadeReader,
        ITipoDeficienciaReader tipoDeficienciaReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(condicaoAtendimentoReader);
        ArgumentNullException.ThrowIfNull(recursoAcessibilidadeReader);
        ArgumentNullException.ThrowIfNull(tipoDeficienciaReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result.Failure(AcessoAoProcesso.NaoEncontrado());
        }

        // Fora do rascunho a recusa vem antes de consultar cadastros.
        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        // Validação sempre precede I/O (ADR-0125, ponto 5): confirma unicidade dos IDs crus
        // antes de consultar os três leitores cross-módulo — um payload com duplicata não
        // precisa de nenhuma chamada de rede para descobrir que vai falhar.
        List<FieldError> idsErros = AtendimentoEspecializado.ValidarIdsUnicos(
            command.CondicaoIds, command.RecursoIds, command.TipoDeficienciaIds);
        if (idsErros.Count > 0)
        {
            return Result.ValidationFailure(idsErros);
        }

        List<CondicaoOfertada> condicoes = [];
        foreach (Guid condicaoId in command.CondicaoIds)
        {
            CondicaoAtendimentoView? condicao = await condicaoAtendimentoReader
                .ObterPorIdAsync(condicaoId, cancellationToken)
                .ConfigureAwait(false);
            if (condicao is null)
            {
                return Result.Failure(new DomainError(
                    "AtendimentoEspecializado.CondicaoNaoEncontrada",
                    $"Condição de atendimento {condicaoId} não encontrada ou não está mais viva."));
            }

            condicoes.Add(CondicaoOfertada.Criar(condicao.Id, condicao.Codigo, condicao.Nome));
        }

        List<RecursoOfertado> recursos = [];
        foreach (Guid recursoId in command.RecursoIds)
        {
            RecursoAcessibilidadeView? recurso = await recursoAcessibilidadeReader
                .ObterPorIdAsync(recursoId, cancellationToken)
                .ConfigureAwait(false);
            if (recurso is null)
            {
                return Result.Failure(new DomainError(
                    "AtendimentoEspecializado.RecursoNaoEncontrado",
                    $"Recurso de acessibilidade {recursoId} não encontrado ou não está mais vivo."));
            }

            recursos.Add(RecursoOfertado.Criar(recurso.Id, recurso.Codigo, recurso.Nome));
        }

        List<DeficienciaAtendida> tiposDeficiencia = [];
        foreach (Guid tipoDeficienciaId in command.TipoDeficienciaIds)
        {
            TipoDeficienciaView? tipo = await tipoDeficienciaReader
                .ObterPorIdAsync(tipoDeficienciaId, cancellationToken)
                .ConfigureAwait(false);
            if (tipo is null)
            {
                return Result.Failure(new DomainError(
                    "AtendimentoEspecializado.TipoDeficienciaNaoEncontrado",
                    $"Tipo de deficiência {tipoDeficienciaId} não encontrado ou não está mais vivo."));
            }

            tiposDeficiencia.Add(DeficienciaAtendida.Criar(tipo.Id, tipo.Codigo, tipo.Nome));
        }

        Result<AtendimentoEspecializado> atendimentoResult =
            AtendimentoEspecializado.Criar(condicoes, recursos, tiposDeficiencia);
        if (atendimentoResult.IsFailure)
        {
            return Result.ValidationFailure(atendimentoResult.Errors);
        }

        Result result = processo.DefinirAtendimentoEspecializado(atendimentoResult.Value!);
        if (result.IsFailure)
        {
            return Result.Failure(result.Error!);
        }

        // Agregado tracked (ObterParaMutacaoAsync): o novo atendimento e suas
        // filhas (Guid v7 já preenchido) são persistidas por change detection.
        // NÃO chamar DbSet.Update — marcaria os filhos novos como Modified,
        // emitindo UPDATE de linhas nunca inseridas.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
