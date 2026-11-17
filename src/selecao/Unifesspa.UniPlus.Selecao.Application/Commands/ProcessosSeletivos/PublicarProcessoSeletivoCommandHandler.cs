namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// Handler convention-based do <see cref="PublicarProcessoSeletivoCommand"/>
/// (congelamento dos parâmetros na publicação, UNI-REQ-0019, ADR-0005 + ADR-0041): carrega o agregado,
/// congela a versão com o documento confirmado (T3) e a configuração
/// canonicalizada (ADR-0100) e delega a orquestração de negócio a
/// <see cref="ProcessoSeletivo.Publicar"/>. Cascading messages — o
/// <see cref="Domain.Events.ProcessoPublicadoEvent"/> é drenado só depois do
/// <c>SaveChanges</c> bem-sucedido.
/// </summary>
public static class PublicarProcessoSeletivoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Eventos)> Handle(
        PublicarProcessoSeletivoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CongelamentoDaVersao congelamento,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(congelamento);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        return processo is null
            ? (Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado.")), [])
            : await PublicarAsync(processo, command, congelamento, cancellationToken).ConfigureAwait(false);
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> PublicarAsync(
        ProcessoSeletivo processo,
        PublicarProcessoSeletivoCommand command,
        CongelamentoDaVersao congelamento,
        CancellationToken cancellationToken)
    {
        EditalDeclarado edital = new(
            command.Numero, command.PeriodoInscricaoInicio, command.PeriodoInscricaoFim, command.DocumentoEditalId, command.Anexos);

        return congelamento.CongelarAsync(
            processo, edital, command.Ato, new PrimeiraPublicacao(), pedido => Task.FromResult(processo.Publicar(pedido)), cancellationToken);
    }
}
