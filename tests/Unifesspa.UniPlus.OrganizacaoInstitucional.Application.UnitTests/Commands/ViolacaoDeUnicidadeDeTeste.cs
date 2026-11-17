namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

/// <summary>
/// Imita a exceção do provedor na violação de unicidade, que expõe o SQLSTATE e o nome da restrição
/// violada, no formato que a Application lê por reflexão.
/// </summary>
internal sealed class ViolacaoDeUnicidadeDeTeste(string restricao) : Exception("Violação de unicidade de teste.")
{
    public string SqlState { get; } = "23505";

    public string ConstraintName { get; } = restricao;
}
