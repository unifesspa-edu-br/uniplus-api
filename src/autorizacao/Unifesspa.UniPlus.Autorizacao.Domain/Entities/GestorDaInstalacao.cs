namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

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
}
