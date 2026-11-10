namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Materializa a janela vencida de um perfil na transação do handler, com os avisos no outbox da mesma gravação.</summary>
public static class MaterializarJanelaVencidaCommandHandler
{
    public static async Task<(Result<bool> Resposta, IEnumerable<object> Mensagens)> Handle(
        MaterializarJanelaVencidaCommand command, ProcessamentoDaJanela processamento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processamento);

        GravacaoDaOperacao<bool> gravacao = await processamento.ProcessarAsync(command.PerfilId, cancellationToken).ConfigureAwait(false);

        return (gravacao.Resultado, gravacao.Avisos);
    }
}
