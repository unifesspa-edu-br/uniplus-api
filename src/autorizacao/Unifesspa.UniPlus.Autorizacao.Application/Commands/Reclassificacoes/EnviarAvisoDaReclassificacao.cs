namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Avisa um fato da reclassificação: grava a linha de aviso e deixa o e-mail no outbox da mesma transação.</summary>
public sealed record EnviarAvisoDaReclassificacaoCommand(OrigemDoAviso Origem, Guid FatoId) : ICommand<Result<bool>>;

/// <summary>
/// O aviso de um fato, numa transação: a linha de <c>avisos_da_reclassificacao</c> e os e-mails entram juntos ou nenhum
/// entra. A unicidade da linha por origem e fato barra o segundo aviso; a execução concorrente que perde a corrida falha
/// e a rotina a tenta de novo no ciclo seguinte.
/// </summary>
public static class EnviarAvisoDaReclassificacaoCommandHandler
{
    public static async Task<(Result<bool> Resposta, IEnumerable<object> Mensagens)> Handle(
        EnviarAvisoDaReclassificacaoCommand command,
        AvisosDaReclassificacao avisos,
        IReclassificacaoRepository reclassificacoes,
        IAutorizacaoUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(reclassificacoes);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        IReadOnlyList<EnviarEmail>? emails = await avisos
            .MontarAsync(new FatoSemAviso(command.Origem, command.FatoId), cancellationToken)
            .ConfigureAwait(false);
        if (emails is null)
        {
            return (Result<bool>.Success(false), []);
        }

        reclassificacoes.Adicionar(AvisoDaReclassificacao.Registrar(command.Origem, command.FatoId, clock.GetUtcNow()));
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return (Result<bool>.Success(true), emails);
    }
}
