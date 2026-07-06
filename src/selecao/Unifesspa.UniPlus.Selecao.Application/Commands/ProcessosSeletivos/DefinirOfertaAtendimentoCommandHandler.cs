namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Interfaces;
using Kernel.Results;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirOfertaAtendimentoCommand"/> (CA-06 da Story
/// #758): resolve cada dimensão nos cadastros vivos do módulo Configuração
/// (<see cref="ICondicaoAtendimentoReader"/>/<see cref="IRecursoAcessibilidadeReader"/>/<see cref="ITipoDeficienciaReader"/>,
/// ADR-0056), congela por valor (snapshot-copy, ADR-0061) e monta a oferta —
/// a invariante ADR-0067 (tipo de deficiência só sob condição PcD) é
/// garantida por <see cref="OfertaAtendimentoEspecializado.Criar"/>.
/// </summary>
public static class DefinirOfertaAtendimentoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirOfertaAtendimentoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICondicaoAtendimentoReader condicaoAtendimentoReader,
        IRecursoAcessibilidadeReader recursoAcessibilidadeReader,
        ITipoDeficienciaReader tipoDeficienciaReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(condicaoAtendimentoReader);
        ArgumentNullException.ThrowIfNull(recursoAcessibilidadeReader);
        ArgumentNullException.ThrowIfNull(tipoDeficienciaReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        List<OfertaCondicao> condicoes = [];
        foreach (Guid condicaoId in command.CondicaoIds)
        {
            CondicaoAtendimentoView? condicao = await condicaoAtendimentoReader
                .ObterPorIdAsync(condicaoId, cancellationToken)
                .ConfigureAwait(false);
            if (condicao is null)
            {
                return Result.Failure(new DomainError(
                    "OfertaAtendimento.CondicaoNaoEncontrada",
                    $"Condição de atendimento {condicaoId} não encontrada ou não está mais viva."));
            }

            condicoes.Add(OfertaCondicao.Criar(condicao.Id, condicao.Codigo, condicao.Nome));
        }

        List<OfertaRecurso> recursos = [];
        foreach (Guid recursoId in command.RecursoIds)
        {
            RecursoAcessibilidadeView? recurso = await recursoAcessibilidadeReader
                .ObterPorIdAsync(recursoId, cancellationToken)
                .ConfigureAwait(false);
            if (recurso is null)
            {
                return Result.Failure(new DomainError(
                    "OfertaAtendimento.RecursoNaoEncontrado",
                    $"Recurso de acessibilidade {recursoId} não encontrado ou não está mais vivo."));
            }

            recursos.Add(OfertaRecurso.Criar(recurso.Id, recurso.Nome));
        }

        List<OfertaTipoDeficiencia> tiposDeficiencia = [];
        foreach (Guid tipoDeficienciaId in command.TipoDeficienciaIds)
        {
            TipoDeficienciaView? tipo = await tipoDeficienciaReader
                .ObterPorIdAsync(tipoDeficienciaId, cancellationToken)
                .ConfigureAwait(false);
            if (tipo is null)
            {
                return Result.Failure(new DomainError(
                    "OfertaAtendimento.TipoDeficienciaNaoEncontrado",
                    $"Tipo de deficiência {tipoDeficienciaId} não encontrado ou não está mais vivo."));
            }

            tiposDeficiencia.Add(OfertaTipoDeficiencia.Criar(tipo.Id, tipo.Nome));
        }

        Result<OfertaAtendimentoEspecializado> ofertaResult =
            OfertaAtendimentoEspecializado.Criar(condicoes, recursos, tiposDeficiencia);
        if (ofertaResult.IsFailure)
        {
            return Result.Failure(ofertaResult.Error!);
        }

        Result result = processo.DefinirOfertaAtendimento(ofertaResult.Value!);
        if (result.IsFailure)
        {
            return result;
        }

        // Agregado tracked (ObterComConfiguracaoAsync): a nova oferta e suas
        // filhas (Guid v7 já preenchido) são persistidas por change detection.
        // NÃO chamar DbSet.Update — marcaria os filhos novos como Modified,
        // emitindo UPDATE de linhas nunca inseridas.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
