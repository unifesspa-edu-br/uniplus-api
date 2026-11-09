namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Processa a janela de um perfil na transação do handler, com os avisos no outbox da mesma gravação.</summary>
public static class ProcessarJanelaDoPerfilCommandHandler
{
    public static async Task<(Result<bool> Resposta, IEnumerable<object> Mensagens)> Handle(
        ProcessarJanelaDoPerfilCommand command, ProcessamentoDaJanela processamento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processamento);

        GravacaoDaOperacao<bool> gravacao = await processamento.ProcessarAsync(command.PerfilId, cancellationToken).ConfigureAwait(false);

        return (gravacao.Resultado, gravacao.Avisos);
    }
}
