namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Os registros de arquivo enviado pelo administrador que ficaram pendentes além do prazo — o
/// documento do Edital e o modelo de documento de uma exigência. O pendente nasce junto da URL de
/// envio e só vira dado de negócio na confirmação; vencida a URL sem confirmação, nada mais o
/// promove, e o registro e o objeto enviado são só sobra.
/// </summary>
/// <remarks>
/// Vencido é o registro <c>Pendente</c> cujo <c>ExpiraEm</c> já passou no instante dado. O
/// confirmado nunca entra na conta, qualquer que seja o prazo gravado nele.
/// </remarks>
public interface IArquivoEnviadoRepository
{
    /// <summary>
    /// Até <paramref name="limite"/> pendentes vencidos em <paramref name="agora"/>, do vencimento
    /// mais antigo para o mais recente, sem rastreamento.
    /// </summary>
    Task<IReadOnlyList<ArquivoPendenteVencido>> ListarPendentesVencidosAsync(
        DateTimeOffset agora, int limite, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove o registro só se ele ainda é pendente e vencido em <paramref name="agora"/> —
    /// <c>DELETE ... WHERE id = @id AND status = Pendente AND expira_em &lt;= @agora</c>, sem passar
    /// pelo change tracker. A condição é reavaliada pelo banco na própria remoção: a confirmação
    /// concorrente reivindica o registro com um <c>UPDATE</c> condicionado ao pendente, e as duas
    /// disputam o mesmo bloqueio de linha — quem chega depois encontra a condição falsa e não
    /// afeta nada. Por isso o confirmado entre a listagem e a remoção permanece.
    /// </summary>
    /// <returns><see langword="true"/> quando a linha foi removida.</returns>
    Task<bool> RemoverSePendenteVencidoAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken = default);
}
