namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A fase como a mensagem de recusa a cita: pelo nome que a unidade vê, entre aspas simples, nunca pelo
/// código do tipo de fase.
/// </summary>
public sealed record CitacaoDaFase
{
    public CitacaoDaFase(string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        Nome = nome;
    }

    public string Nome { get; }

    public override string ToString() => $"'{Nome}'";

    internal static string ComArtigo(CitacaoDaFase? fase) => fase is null ? "a fase" : $"a fase {fase}";
}
