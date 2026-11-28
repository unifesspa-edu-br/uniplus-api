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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;

/// <summary>
/// Handler do <see cref="DefinirFatosColetadosCommand"/> (Story #984): substitui integralmente
/// os itens do formulário de um código, em duas passadas que acumulam no mesmo
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

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao
            .ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await EscreverESalvarAsync(command, obtido.Value!, fatoCandidatoReader, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> EscreverESalvarAsync(
        DefinirFatosColetadosCommand command, ProcessoSeletivo processo, IFatoCandidatoReader fatoCandidatoReader, IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result escrita = await EscreverAsync(command, processo, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
        if (escrita.IsSuccess)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        return escrita;
    }

    private static async Task<Result> EscreverAsync(
        DefinirFatosColetadosCommand command, ProcessoSeletivo processo, IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        if (RecusaDaQuantidade(command) is { } recusa)
        {
            return recusa;
        }

        Result<CodigoDoFormulario> codigo = RegrasDosFormularios.CodigoDoCaminho(command.FormularioCodigo);
        if (codigo.IsFailure)
        {
            return Result.Failure(codigo.Error!);
        }

        ColetaMesclada coleta = SecaoDadosBasicosDoProcesso.Mesclar(processo, codigo.Value!, command);
        ColetaResolvida resolvida = await ResolverNoCatalogoAsync(processo, coleta, fatoCandidatoReader, cancellationToken).ConfigureAwait(false);
        return resolvida.Erros.Count > 0 ? Result.ValidationFailure(resolvida.Erros) : Gravar(codigo.Value!, command, processo, coleta, resolvida);
    }

    /// <summary>
    /// Acima do teto a lista não é lida item a item, e a recusa da quantidade sai antes de resolver o vocabulário
    /// cross-módulo, sem pagar o I/O do reader. A ausência da lista de itens não depende da quantidade e sai junto.
    /// </summary>
    private static Result? RecusaDaQuantidade(DefinirFatosColetadosCommand command) =>
        FormaDoItem.ValidarQuantidade(command.QuantidadeNoTeto) is { Count: > 0 } excesso
            ? Result.ValidationFailure(command.Itens is null ? [LeituraDosItensDoFormulario.RecusaDaListaAusente(), .. excesso] : excesso)
            : null;

    /// <summary>
    /// A leitura da forma dos itens e dos grupos usa só o formato do dado no catálogo, lido depois da recusa por quantidade;
    /// o item recusado na leitura só não segue para a resolução contra o catálogo. As recusas da leitura, da resolução, da
    /// seção dos dados básicos e do vínculo novo acumulam no mesmo <c>errors[]</c> (ADR-0125).
    /// </summary>
    private static async Task<ColetaResolvida> ResolverNoCatalogoAsync(
        ProcessoSeletivo processo, ColetaMesclada coleta, IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> fatosDoCatalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, fatosDoCatalogo);
        ItensLidos itensLidos = LeituraDosItensDoFormulario.Ler(coleta.Itens, contexto);
        GruposLidos gruposLidos = LeituraDosItensDoFormulario.LerGrupos(coleta.Grupos, contexto);
        (List<FatoColetado> fatos, List<FieldError> erros) = EscritaDosItens.Resolver(itensLidos, contexto);
        (List<GrupoColetado> grupos, List<FieldError> errosDosGrupos) = EscritaDosItens.ResolverGrupos(gruposLidos, contexto);
        List<FieldError> vinculoNovo = RecusasDoVinculoNovo(processo, contexto, itensLidos, gruposLidos);
        return new ColetaResolvida(fatos, grupos, [.. erros, .. errosDosGrupos, .. coleta.RecusasDaSecao, .. vinculoNovo]);
    }

    private static List<FieldError> RecusasDoVinculoNovo(ProcessoSeletivo processo, ContextoDoCatalogo contexto, ItensLidos itens, GruposLidos grupos) =>
        [.. ConferenciaDeVinculoNovo.Conferir(
            contexto.Fatos,
            ReferenciasVivas.Vinculos(processo.Declaracao()),
            VinculosDeFatos.De(
                itens.CodigosEnviados.Concat(grupos.CodigosEnviados),
                itens.CondicoesLidas.Concat(grupos.CondicoesLidas).Select(static c => (c.Fato, c.Valor)))).Errors];

    private static Result Gravar(
        CodigoDoFormulario codigo, DefinirFatosColetadosCommand command, ProcessoSeletivo processo, ColetaMesclada coleta, ColetaResolvida resolvida)
    {
        // Grupos omitidos ficam como estão no formulário; a lista vazia os remove.
        Result gravado = processo.DefinirFatosColetados(codigo, resolvida.Fatos, command.Grupos is null && !coleta.GruposOmitidosRegravados ? null : resolvida.Grupos);
        return gravado.IsFailure ? Result.ValidationFailure(gravado.Errors) : Result.Success();
    }

    /// <summary>Os itens e grupos criados contra o catálogo e as recusas de toda a leitura.</summary>
    private sealed record ColetaResolvida(List<FatoColetado> Fatos, List<GrupoColetado> Grupos, List<FieldError> Erros);
}
