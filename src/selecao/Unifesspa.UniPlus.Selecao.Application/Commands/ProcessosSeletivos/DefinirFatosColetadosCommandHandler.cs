namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

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
/// (<see cref="ProcessoSeletivo.DefinirFatosColetados"/>).
/// </summary>
public static class DefinirFatosColetadosCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirFatosColetadosCommand command,
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
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição (e o bloqueio de mutação pós-publicação sem sessão) é conferida AQUI, ANTES
        // da resolução do vocabulário cross-módulo: um processo publicado sem sessão, ou um cliente
        // com If-Match defasado, é recusado sem pagar o I/O do reader nem caçar um fato que ele não
        // errou. O mesmo guard continua dentro de DefinirFatosColetados.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        // Acima do teto a lista não é lida item a item: o validator não confere os itens dela, e a
        // recusa da quantidade é a única resposta.
        IReadOnlyList<GrupoColetadoInput> grupos = command.Grupos ?? [];
        if (FormaDoItem.ValidarQuantidade(command.QuantidadeNoTeto) is { Count: > 0 } excesso)
        {
            return Result<MutacaoAceita>.ValidationFailure(excesso);
        }

        // O formulário de inscrição coleta o conjunto básico na seção reservada: o item básico omitido
        // entra como está gravado, e o enviado tem de ser igual a ele. Os grupos omitidos ficam como
        // estão gravados, na ordem do formulário, e não sobem com os itens enviados.
        IReadOnlyList<FatoColetadoInput> itens = command.Itens;
        List<FieldError> daSecao = [];
        if (command.Finalidade == FinalidadeFormulario.Inscricao)
        {
            (itens, grupos, daSecao) = ConjuntoBasicoDaInscricao.MesclarItens(command.Itens, grupos, SecaoDadosBasicosDoProcesso.Itens(processo));
        }

        // A forma vem antes da leitura do catálogo, sem I/O; o item de forma inválida só não segue
        // para a conferência contra o catálogo, e as recusas acumulam no mesmo errors[] (ADR-0125).
        ItensLidos lidos = EscritaDosItens.Ler(itens);
        GruposLidos gruposLidos = EscritaDosItens.LerGrupos(grupos);
        IReadOnlyList<FatoCandidatoView> fatosDoCatalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, fatosDoCatalogo);
        (List<FatoColetado> fatos, List<FieldError> erros) = EscritaDosItens.Resolver(lidos, contexto);
        (List<GrupoColetado> gruposResolvidos, List<FieldError> errosDosGrupos) = EscritaDosItens.ResolverGrupos(gruposLidos, contexto);
        erros.AddRange(errosDosGrupos);
        erros.AddRange(daSecao);

        if (erros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(erros);
        }

        Result vinculoNovo = ConferenciaDeVinculoNovo.Conferir(
            contexto.Fatos,
            processo.Vinculos(),
            VinculosDeFatos.De(
                fatos.Concat(gruposResolvidos.SelectMany(static g => g.Subitens)).Select(static f => f.FatoCodigo),
                fatos.SelectMany(static f => f.Condicoes).Concat(gruposResolvidos.SelectMany(static g => g.Condicoes))
                    .Select(static c => (c.Fato, c.Valor))));
        if (vinculoNovo.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(vinculoNovo.Error!);
        }

        // Grupos omitidos ficam como estão no formulário; a lista vazia os remove.
        Result result = processo.DefinirFatosColetados(
            command.Finalidade, fatos, command.Precondicao, command.Grupos is null ? null : gruposResolvidos);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(result.Errors);
        }

        // Agregado tracked: a substituição da coleção (Clear + filhos novos com Guid v7) é
        // persistida por change detection no SaveChanges — não chamar DbSet.Update.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        // Em rascunho puro não há sessão: ETagDaSessaoEditorial é nulo e a resposta é 204 sem ETag.
        // Sob sessão de retificação, a revisão avançou e o ETag novo é devolvido.
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
