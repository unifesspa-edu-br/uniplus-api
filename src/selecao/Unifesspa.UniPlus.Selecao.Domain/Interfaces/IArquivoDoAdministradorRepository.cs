namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Os arquivos do administrador (documento a publicar e modelo de documento), cuja confirmação lê o conteúdo e
/// grava a cópia. O arquivo do candidato não passa por aqui.
/// </summary>
public interface IArquivoDoAdministradorRepository : IArquivoEnviadoRepository
{
    /// <summary>
    /// Reivindica atomicamente a confirmação — <c>UPDATE ... WHERE id = @id AND status =
    /// Pendente</c>, sem passar pelo change tracker. Duas confirmações concorrentes
    /// (Idempotency-Keys diferentes) nunca ganham as duas: a perdedora bloqueia no bloqueio de linha
    /// do Postgres e, quando o bloqueio termina, sua condição não bate mais, então afeta zero linhas. Só depois
    /// de reivindicar com sucesso a confirmação grava a cópia — a perdedora nunca escreve
    /// no armazenamento. A cópia é gravada antes do commit do registro: se a gravação desse registro é
    /// desfeita, a cópia fica sem registro confirmado e sai com o pendente vencido, pela chave que a
    /// entidade calcula (<c>ChaveDaCopiaGravada</c>).
    /// </summary>
    Task<bool> TentarReivindicarConfirmacaoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca para remoção, na gravação da unidade de trabalho, todos os arquivos da identidade do processo e devolve
    /// as chaves de objeto no armazenamento: a de envio e a da cópia gravada na confirmação de cada um, pendente ou confirmado
    /// (a cópia pode existir num pendente cuja confirmação falhou depois de gravá-la). Só identificadores; o nome do
    /// arquivo nunca entra na chave. Quem chama já bloqueia o dono, de modo que nenhuma confirmação corre junto.
    /// </summary>
    Task<IReadOnlyList<string>> RemoverDoProcessoAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default);
}
