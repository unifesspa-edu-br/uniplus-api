namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

/// <summary>
/// Peso e corte de uma área de conhecimento dentro de uma linha de
/// <see cref="PesoAreaEnem"/>. Código e rótulo são fixos e postos pelo sistema a partir
/// de <see cref="PesoAreaEnem.Areas"/>; o operador edita só os valores.
/// </summary>
public sealed class PesoAreaEnemArea
{
    public string Codigo { get; private set; } = string.Empty;
    public string Rotulo { get; private set; } = string.Empty;
    public decimal Peso { get; private set; }

    /// <summary>Nota mínima da área (0–1000), ou <see langword="null"/> quando a área não tem corte.</summary>
    public decimal? Corte { get; private set; }

    // EF Core materialization
    private PesoAreaEnemArea()
    {
    }

    private PesoAreaEnemArea(string codigo, string rotulo, decimal peso, decimal? corte)
    {
        Codigo = codigo;
        AplicarValores(rotulo, peso, corte);
    }

    /// <summary>
    /// Cria a área com código e rótulo postos pelo agregado e valores já validados por
    /// <see cref="PesoAreaEnem"/>.
    /// </summary>
    internal static PesoAreaEnemArea Criar(string codigo, string rotulo, decimal peso, decimal? corte) =>
        new(codigo, rotulo, peso, corte);

    internal void AplicarValores(string rotulo, decimal peso, decimal? corte)
    {
        Rotulo = rotulo;
        Peso = peso;
        Corte = corte;
    }
}
