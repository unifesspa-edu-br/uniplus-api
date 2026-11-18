namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Materializa a revisão vencida de um perfil na transação do handler, com os avisos no outbox da mesma gravação.</summary>
public static class MaterializarRevisaoVencidaCommandHandler
{
    public static async Task<(Result<bool> Resposta, IEnumerable<object> Mensagens)> Handle(
        MaterializarRevisaoVencidaCommand command, ProcessamentoDaRevisao processamento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processamento);

        GravacaoDaOperacao<bool> gravacao = await processamento.ProcessarAsync(command.PerfilId, cancellationToken).ConfigureAwait(false);

        return (gravacao.Resultado, gravacao.Avisos);
    }
}
