namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Os registros de arquivo enviado por URL pré-assinada que ficaram pendentes além do prazo — o
/// documento a publicar e o modelo de documento do administrador e o arquivo do candidato. O pendente nasce junto da URL de
/// envio e só vira dado de negócio na confirmação; vencida a URL sem confirmação, nada mais o
/// promove, e o registro e o objeto enviado são só sobra.
/// </summary>
/// <remarks>
/// Vencido é o registro <c>PENDENTE</c> cujo <c>ExpiraEm</c> já passou no instante dado. O
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
    /// Remove o registro só se ele ainda é pendente e vencido em <paramref name="agora"/> e, na mesma
    /// transação, executa <paramref name="removerObjetos"/>: abre a transação, apaga o registro —
    /// <c>DELETE ... WHERE id = @id AND status = Pendente AND expira_em &lt;= @agora</c>, sem passar
    /// pelo change tracker —, remove os objetos e só então confirma. A condição é reavaliada pelo
    /// banco na própria remoção: a confirmação concorrente reivindica o registro com um <c>UPDATE</c>
    /// condicionado ao pendente, e as duas disputam o mesmo bloqueio de linha — quem chega depois
    /// encontra a condição falsa e não afeta nada. O DELETE segura a linha até o commit, então uma
    /// confirmação que chega durante a remoção dos objetos espera e depois não encontra mais o
    /// registro; e o confirmado entre a listagem e a remoção permanece, com a cópia gravada dele.
    /// Se <paramref name="removerObjetos"/> lança, a transação é desfeita e o registro fica.
    /// </summary>
    /// <remarks>
    /// Só a rotina de limpeza chama este método: a transação é própria e não aninha numa transação
    /// ambiente, como a do pipeline de mensagens. <paramref name="removerObjetos"/> só executa quando
    /// a linha foi apagada.
    /// </remarks>
    /// <returns><see langword="true"/> quando a linha foi removida.</returns>
    Task<bool> RemoverSePendenteVencidoAsync(
        Guid id,
        DateTimeOffset agora,
        Func<CancellationToken, Task> removerObjetos,
        CancellationToken cancellationToken = default);
}
