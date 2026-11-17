namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// Fecha a sessão editorial (ADR-0110): congela a versão N+1 <b>com a
/// configuração editada</b>, registra o ato em Publicações e encerra a sessão — tudo
/// na mesma transação.
/// </summary>
/// <remarks>
/// <para>
/// <b>É aqui que a Feature entrega o que ela existe para entregar.</b> Abrir e descartar
/// deixam o certame como estava; só o fechamento faz a configuração alterada virar
/// documento.
/// </para>
/// <para>
/// As conferências são <b>as mesmas</b> do atalho atômico, e pela mesma razão: o que o catálogo de
/// Publicações recusaria tem de ser recusado <b>aqui</b>, antes de qualquer escrita. A recusa do
/// registro central, como a vaga do objeto ocupada ou o ato já retificado, vem da porta, na mesma
/// transação, e desfaz tudo.
/// </para>
/// <para>
/// <b>Uma recusa não destrói a sessão.</b> Se o congelamento for negado — conformidade
/// insuficiente, ato já retificado, documento não confirmado —, o rascunho <b>permanece
/// aberto</b>, com a configuração editada intacta: o administrador corrige e tenta de novo.
/// </para>
/// </remarks>
public static class FecharRetificacaoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        FecharRetificacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CongelamentoDaVersao congelamento,
        LeituraDaBaseDaRetificacao leituraDaBase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(congelamento);
        ArgumentNullException.ThrowIfNull(leituraDaBase);

        Result<ProcessoSeletivo> obtido = await ObterParaFecharAsync(command, processoSeletivoRepository, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? (Result.Failure(obtido.Error!), [])
            : await FecharAsync(obtido.Value!, command, congelamento, leituraDaBase, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ProcessoSeletivo>> ObterParaFecharAsync(
        FecharRetificacaoCommand command, IProcessoSeletivoRepository repositorio, CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await repositorio
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<ProcessoSeletivo>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        return PrecondicoesDoFechamento.Recusa(command.Precondicao, processo) is { } recusa
            ? Result<ProcessoSeletivo>.Failure(recusa)
            : Result<ProcessoSeletivo>.Success(processo);
    }

    private static async Task<(Result Resposta, IEnumerable<object> Eventos)> FecharAsync(
        ProcessoSeletivo processo,
        FecharRetificacaoCommand command,
        CongelamentoDaVersao congelamento,
        LeituraDaBaseDaRetificacao leituraDaBase,
        CancellationToken cancellationToken)
    {
        Result<BaseDaRetificacao> baseLida = await leituraDaBase.DaSessaoAbertaAsync(processo, cancellationToken).ConfigureAwait(false);
        return baseLida.IsFailure
            ? (Result.ValidationFailure(baseLida.Errors), [])
            : await CongelarAsync(processo, command, baseLida.Value!, congelamento, cancellationToken).ConfigureAwait(false);
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> CongelarAsync(
        ProcessoSeletivo processo,
        FecharRetificacaoCommand command,
        BaseDaRetificacao baseDaRetificacao,
        CongelamentoDaVersao congelamento,
        CancellationToken cancellationToken)
    {
        EditalDeclarado edital = new(
            command.Numero, command.PeriodoInscricaoInicio, command.PeriodoInscricaoFim, command.DocumentoEditalId, command.Anexos);
        FechamentoDaSessao origem = new(baseDaRetificacao, processo.Rascunho!.FatosDasVersoesPublicadas);

        return congelamento.CongelarAsync(
            processo, edital, command.Ato, origem,
            pedido => Task.FromResult(processo.FecharRetificacao(pedido, baseDaRetificacao.VersaoVigente, command.Precondicao)),
            cancellationToken);
    }
}
