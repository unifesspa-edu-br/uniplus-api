namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;

/// <summary>
/// Handler do <see cref="DefinirFatosColetadosCommand"/> (Story #984): substitui integralmente
/// os itens do formulário de uma finalidade, em duas passadas que acumulam no mesmo
/// <c>errors[]</c> (ADR-0125). A primeira confirma a <b>forma</b> de todos os itens — campos
/// básicos e obrigatoriedade — sem tocar o vocabulário cross-módulo. A segunda resolve, para os
/// itens de forma válida, a <b>coletabilidade</b> (só se coleta fato <c>Origem = DECLARADO</c>
/// com binding de campo de inscrição) e a validação <b>semântica</b> das regras (operador ×
/// domínio × valor do fato citado, contra a oferta do próprio processo para os domínios
/// dinâmicos), com o vocabulário cross-módulo (<see cref="IFatoCandidatoReader"/>, ADR-0056).
/// A validação <b>estrutural</b> do grafo (ordem única, regra cita fato coletado e anterior,
/// aciclicidade) e o guard de rascunho são do agregado
/// (<see cref="ProcessoSeletivo.DefinirFatosColetados"/>). O agregado é tracked: a substituição da
/// coleção (Clear + filhos novos com Guid v7) é persistida por change detection no
/// <c>SaveChanges</c>, sem <c>DbSet.Update</c>.
/// </summary>
public static class DefinirFatosColetadosCommandHandler
{
    public static async Task<Result> Handle(
        DefinirFatosColetadosCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ProcessoSeletivo? processo = await processoSeletivoRepository.ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        Result escrita = processo is null
            ? ProcessoNaoEncontrado(command.ProcessoSeletivoId)
            : await EscreverAsync(command, processo, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
        if (escrita.IsSuccess)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        return escrita;
    }

    private static Result ProcessoNaoEncontrado(Guid processoSeletivoId) =>
        Result.Failure(new DomainError("ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {processoSeletivoId} não encontrado."));

    private static async Task<Result> EscreverAsync(
        DefinirFatosColetadosCommand command, ProcessoSeletivo processo, IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        if (RecusaAntesDoCatalogo(command, processo) is { } recusa)
        {
            return recusa;
        }

        ColetaMesclada coleta = SecaoDadosBasicosDoProcesso.Mesclar(processo, command);
        ColetaResolvida resolvida = await ResolverNoCatalogoAsync(processo, coleta, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
        return resolvida.Erros.Count > 0 ? Result.ValidationFailure(resolvida.Erros) : Gravar(command, processo, coleta, resolvida);
    }

    /// <summary>
    /// Fora do rascunho a recusa vem antes de resolver o vocabulário cross-módulo, sem pagar o I/O do
    /// reader. Acima do teto a lista não é lida item a item: o validator não confere os itens dela, e a
    /// recusa da quantidade é a única resposta.
    /// </summary>
    private static Result? RecusaAntesDoCatalogo(DefinirFatosColetadosCommand command, ProcessoSeletivo processo)
    {
        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        return FormaDoItem.ValidarQuantidade(command.QuantidadeNoTeto) is { Count: > 0 } excesso ? Result.ValidationFailure(excesso) : null;
    }

    /// <summary>
    /// A forma vem antes da leitura do catálogo, sem I/O; o item de forma inválida só não segue para a
    /// conferência contra o catálogo, e as recusas acumulam no mesmo <c>errors[]</c> (ADR-0125).
    /// </summary>
    private static async Task<ColetaResolvida> ResolverNoCatalogoAsync(
        ProcessoSeletivo processo, ColetaMesclada coleta, IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        ItensLidos itensLidos = EscritaDosItens.Ler(coleta.Itens);
        GruposLidos gruposLidos = EscritaDosItens.LerGrupos(coleta.Grupos);
        IReadOnlyList<FatoCandidatoView> fatosDoCatalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, fatosDoCatalogo);
        (List<FatoColetado> fatos, List<FieldError> erros) = EscritaDosItens.Resolver(itensLidos, contexto);
        (List<GrupoColetado> grupos, List<FieldError> errosDosGrupos) = EscritaDosItens.ResolverGrupos(gruposLidos, contexto);
        return new ColetaResolvida(contexto, fatos, grupos, [.. erros, .. errosDosGrupos, .. coleta.RecusasDaSecao]);
    }

    private static Result Gravar(DefinirFatosColetadosCommand command, ProcessoSeletivo processo, ColetaMesclada coleta, ColetaResolvida resolvida)
    {
        Result vinculoNovo = ConferirVinculoNovo(processo, resolvida);
        if (vinculoNovo.IsFailure)
        {
            return Result.Failure(vinculoNovo.Error!);
        }

        // Grupos omitidos ficam como estão no formulário; a lista vazia os remove.
        Result gravado = processo.DefinirFatosColetados(
            command.Finalidade, resolvida.Fatos, command.Grupos is null && !coleta.GruposOmitidosRegravados ? null : resolvida.Grupos);
        return gravado.IsFailure ? Result.ValidationFailure(gravado.Errors) : Result.Success();
    }

    private static Result ConferirVinculoNovo(ProcessoSeletivo processo, ColetaResolvida resolvida) =>
        ConferenciaDeVinculoNovo.Conferir(
            resolvida.Contexto.Fatos,
            ReferenciasVivas.Vinculos(processo.Declaracao()),
            VinculosDeFatos.De(
                resolvida.Fatos.Concat(resolvida.Grupos.SelectMany(static g => g.Subitens)).Select(static f => f.FatoCodigo),
                resolvida.Fatos.SelectMany(static f => f.Condicoes).Concat(resolvida.Grupos.SelectMany(static g => g.Condicoes))
                    .Select(static c => (c.Fato, c.Valor))));

    /// <summary>Os itens e grupos criados contra o catálogo, o contexto que o conferiu e as recusas de toda a leitura.</summary>
    private sealed record ColetaResolvida(
        ContextoDoCatalogo Contexto, List<FatoColetado> Fatos, List<GrupoColetado> Grupos, List<FieldError> Erros);
}
