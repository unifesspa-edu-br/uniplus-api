namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// O registro dos avisos já emitidos, que impede o mesmo aviso, para o mesmo fato e a mesma pessoa, de sair duas vezes.
/// A gravação acontece na conexão e na transação do <c>DbContext</c> de quem chama: o registro e o envelope no outbox
/// confirmam ou desfazem juntos.
/// </summary>
public interface IRegistroDeAvisos
{
    /// <summary>
    /// Grava de uma vez os identificadores ainda não registrados e devolve só os que acabaram de entrar. Quem recebe um
    /// identificador de volta é quem deve emitir a mensagem: a segunda gravação concorrente espera a primeira e não
    /// recebe nada.
    /// </summary>
    /// <param name="tipo">O tipo do aviso, em texto estável.</param>
    /// <param name="fatoId">O fato que gerou o aviso.</param>
    /// <param name="mensagensIds">Os identificadores, já ordenados: toda transação trava as entradas na mesma ordem.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<IReadOnlyCollection<Guid>> RegistrarAsync(
        string tipo, Guid fatoId, IReadOnlyList<Guid> mensagensIds, CancellationToken cancellationToken = default);
}
