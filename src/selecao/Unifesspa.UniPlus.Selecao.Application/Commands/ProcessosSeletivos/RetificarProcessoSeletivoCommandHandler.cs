namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Publicacao;

using Kernel.Results;

using Services;

/// <summary>
/// Handler convention-based do <see cref="RetificarProcessoSeletivoCommand"/>
/// (congelamento dos parâmetros na publicação, UNI-REQ-0019, ADR-0101 + ADR-0005): carrega o agregado com a
/// cadeia de Editais, lê a base da retificação, congela a versão com o documento
/// confirmado (T3) e a configuração canonicalizada acrescida do bloco de retificação (ADR-0100/0101) e delega a
/// orquestração de negócio a <see cref="ProcessoSeletivo.Retificar"/>.
/// Cascading messages — o <see cref="Domain.Events.ProcessoPublicadoEvent"/>
/// só é drenado depois do <c>SaveChanges</c> bem-sucedido.
/// </summary>
public static class RetificarProcessoSeletivoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        RetificarProcessoSeletivoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CongelamentoDaVersao congelamento,
        LeituraDaBaseDaRetificacao leituraDaBase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(congelamento);
        ArgumentNullException.ThrowIfNull(leituraDaBase);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        return processo is null
            ? (Result.Failure(AcessoAoProcesso.NaoEncontrado()), [])
            : await RetificarAsync(processo, command, congelamento, leituraDaBase, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(Result Resposta, IEnumerable<object> Eventos)> RetificarAsync(
        ProcessoSeletivo processo,
        RetificarProcessoSeletivoCommand command,
        CongelamentoDaVersao congelamento,
        LeituraDaBaseDaRetificacao leituraDaBase,
        CancellationToken cancellationToken)
    {
        Result<BaseDaRetificacao> baseLida = await leituraDaBase.DaVersaoPublicadaAsync(processo, command.Motivo, cancellationToken).ConfigureAwait(false);
        return baseLida.IsFailure
            ? (Result.ValidationFailure(baseLida.Errors), [])
            : await CongelarAsync(processo, command, baseLida.Value!, congelamento, cancellationToken).ConfigureAwait(false);
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> CongelarAsync(
        ProcessoSeletivo processo,
        RetificarProcessoSeletivoCommand command,
        BaseDaRetificacao baseDaRetificacao,
        CongelamentoDaVersao congelamento,
        CancellationToken cancellationToken)
    {
        EditalDeclarado edital = new(
            command.Numero, command.PeriodoInscricaoInicio, command.PeriodoInscricaoFim, command.DocumentoEditalId, command.Anexos);

        return congelamento.CongelarAsync(
            processo, edital, command.Ato, new RetificacaoDeAtoUnico(baseDaRetificacao),
            pedido => Task.FromResult(processo.Retificar(pedido, baseDaRetificacao.VersaoVigente, baseDaRetificacao.Motivo)),
            cancellationToken);
    }
}
