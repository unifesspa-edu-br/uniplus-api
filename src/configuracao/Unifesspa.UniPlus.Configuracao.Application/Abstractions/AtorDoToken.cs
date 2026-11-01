namespace Unifesspa.UniPlus.Configuracao.Application.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Quem age, tirado do token (ADR-0033): o emissor e o <c>sub</c>, e o nome social quando há, senão o civil. O nome
/// de exibição do provedor não entra, porque pode ser o identificador de login. Serve a quem registra um ato e a quem
/// compara a autoria na leitura.
/// </summary>
internal static class AtorDoToken
{
    // Mesmo valor que as colunas "por" do circuito corrente usam quando o token não identifica a pessoa.
    private const string SemIdentificacao = "system";

    public static AtorDoAto De(IUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(userContext);

        return new AtorDoAto(
            Identidade(userContext.Issuer),
            Identidade(userContext.UserId),
            !string.IsNullOrWhiteSpace(userContext.NomeSocial) ? userContext.NomeSocial : userContext.NomeCompleto);
    }

    private static string Identidade(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? SemIdentificacao : valor;
}
