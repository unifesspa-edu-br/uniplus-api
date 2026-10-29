namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Identidade de uma pessoa como o token a traz: emissor e <c>sub</c>, ambos opacos. Nunca
/// carrega CPF nem nome, que não pertencem à concessão.
/// </summary>
public sealed record Identidade
{
    public string Emissor { get; }

    public string Sub { get; }

    public Identidade(string emissor, string sub)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emissor);
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        Emissor = emissor;
        Sub = sub;
    }
}
