namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Um dos dois gestores que a instalação designa: o par emissor e <c>contaId</c> do token, o e-mail institucional que a
/// revogação da concessão pede de volta e o nome que a trilha mostra. Nunca guarda CPF.
/// </summary>
public sealed class GestorDaInstalacao
{
    public string Emissor { get; private set; }

    public string ContaId { get; private set; }

    /// <summary>E-mail institucional, em minúsculas; nulo só nas linhas anteriores a este dado.</summary>
    public string? Email { get; private set; }

    public string? Nome { get; private set; }

    internal GestorDaInstalacao(string emissor, string contaId, string? email, string? nome)
    {
        Emissor = emissor;
        ContaId = contaId;
        Email = email;
        Nome = nome;
    }

    internal Identidade Identidade => new(Emissor, ContaId, Nome);

    /// <summary>O gestor conferido, ou nulo quando a identidade informada não vale: de outro emissor, sem <c>contaId</c> UUID ou sem e-mail válido.</summary>
    internal static GestorDaInstalacao? Conferir(IdentidadeInformada? informado, string emissorAceito) =>
        informado is { Emissor: { } emissor, ContaId: { } contaId }
        && string.Equals(emissor, emissorAceito, StringComparison.Ordinal)
        && Guid.TryParseExact(contaId, "D", out Guid guid)
        && guid != Guid.Empty
        && EmailDaPessoa.Normalizar(informado.Email) is { } email
            ? new GestorDaInstalacao(emissor, guid.ToString("D"), email, NomeDeExibicao.Normalizar(informado.Nome))
            : null;
}
