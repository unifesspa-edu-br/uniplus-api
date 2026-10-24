namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Os arquivos do administrador (documento a publicar e modelo de documento), cuja confirmação lê o conteúdo e
/// grava a cópia selada. O arquivo do candidato não passa por aqui.
/// </summary>
public interface IArquivoDoAdministradorRepository : IArquivoEnviadoRepository
{
    /// <summary>
    /// Reivindica atomicamente a confirmação — <c>UPDATE ... WHERE id = @id AND status =
    /// Pendente</c>, sem passar pelo change tracker. Duas confirmações concorrentes
    /// (Idempotency-Keys diferentes) nunca ganham as duas: a perdedora bloqueia no lock de linha
    /// do Postgres e, ao destravar, sua condição não bate mais, então afeta zero linhas. Só depois
    /// de reivindicar com sucesso a confirmação grava a cópia selada — a perdedora nunca escreve
    /// no armazenamento.
    /// </summary>
    Task<bool> TentarReivindicarConfirmacaoAsync(Guid id, CancellationToken cancellationToken = default);
}
