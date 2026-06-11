namespace Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Leitor cross-módulo da <c>Instituicao</c> singleton (ADR-0056). Expõe o
/// estado vivo do cabeçalho institucional para consumo por outros bounded
/// contexts (ex.: Seleção, Configuração) sem acesso direto ao banco de
/// Organização (ADR-0054).
/// </summary>
/// <remarks>
/// A <c>Instituicao</c> é singleton (ADR-0055): há no máximo uma viva por
/// instância. A implementação canônica resolve o registro corrente respaldada
/// por cache Redis, sem congelamento — o consumo é sempre do dado vivo. Cada API
/// que precisa do cabeçalho institucional hospeda sua própria instância via
/// <c>AddOrganizacaoInstitucionalInfrastructure()</c>.
/// </remarks>
public interface IInstituicaoReader
{
    /// <summary>
    /// Obtém a Instituição viva (singleton), ou <see langword="null"/> se nenhuma
    /// foi cadastrada ainda.
    /// </summary>
    Task<InstituicaoView?> ObterAsync(CancellationToken cancellationToken = default);
}
