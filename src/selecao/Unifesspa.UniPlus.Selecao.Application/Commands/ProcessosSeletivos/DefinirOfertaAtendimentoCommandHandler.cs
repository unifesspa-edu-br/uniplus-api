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
    public static async Task<Result<MutacaoAceita>> Handle(
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
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição é conferida AQUI, logo depois do 404 e antes das regras de negócio
        // que este handler avalia (existência de cadastros, coerência de referências): ela
        // as precede na ordem da ADR-0110 D9. Um cliente com If-Match defasado tem de saber
        // disso antes de sair caçando um cadastro que ele não errou.
        //
        // O que ela NÃO precede é a validação de SCHEMA do payload: o FluentValidation roda
        // como middleware do Wolverine, antes deste handler, e um command malformado morre
        // ali com 422 sem que o guard chegue a rodar. É desvio consciente da D9 — corrigi-lo
        // exigiria carregar o agregado no middleware, o que é pior. O custo é uma rodada
        // extra para quem erra as DUAS coisas ao mesmo tempo; nenhum estado é corrompido.
        //
        // O mesmo guard continua dentro do Definir* do domínio: esta antecipação dá a ordem,
        // não a garantia.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        List<OfertaCondicao> condicoes = [];
        foreach (Guid condicaoId in command.CondicaoIds)
        {
            CondicaoAtendimentoView? condicao = await condicaoAtendimentoReader
                .ObterPorIdAsync(condicaoId, cancellationToken)
                .ConfigureAwait(false);
            if (condicao is null)
            {
                return Result<MutacaoAceita>.Failure(new DomainError(
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
                return Result<MutacaoAceita>.Failure(new DomainError(
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
                return Result<MutacaoAceita>.Failure(new DomainError(
                    "OfertaAtendimento.TipoDeficienciaNaoEncontrado",
                    $"Tipo de deficiência {tipoDeficienciaId} não encontrado ou não está mais vivo."));
            }

            tiposDeficiencia.Add(OfertaTipoDeficiencia.Criar(tipo.Id, tipo.Nome));
        }

        Result<OfertaAtendimentoEspecializado> ofertaResult =
            OfertaAtendimentoEspecializado.Criar(condicoes, recursos, tiposDeficiencia);
        if (ofertaResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(ofertaResult.Error!);
        }

        Result result = processo.DefinirOfertaAtendimento(ofertaResult.Value!, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(result.Error!);
        }

        // Agregado tracked (ObterParaMutacaoAsync): a nova oferta e suas
        // filhas (Guid v7 já preenchido) são persistidas por change detection.
        // NÃO chamar DbSet.Update — marcaria os filhos novos como Modified,
        // emitindo UPDATE de linhas nunca inseridas.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
