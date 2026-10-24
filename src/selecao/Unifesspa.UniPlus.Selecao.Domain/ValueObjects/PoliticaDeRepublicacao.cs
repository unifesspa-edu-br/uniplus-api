namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quanto o pedido de verificação espera, depois de cada publicação, antes de ser republicado. O prazo dobra a cada
/// publicação, até o teto: com o verificador fora do ar, a fila não acumula um duplicado a cada prazo base.
/// </summary>
/// <param name="Base">O prazo depois da primeira publicação; precisa passar do tempo de fila no pico.</param>
/// <param name="Maximo">O maior prazo entre duas publicações.</param>
public sealed record PoliticaDeRepublicacao(TimeSpan Base, TimeSpan Maximo)
{
    public static PoliticaDeRepublicacao Padrao { get; } = new(TimeSpan.FromMinutes(10), TimeSpan.FromHours(6));

    /// <summary>O prazo até a próxima publicação, dado quantas já foram feitas: <c>Base × 2^(publicações − 1)</c>, limitado ao teto.</summary>
    public TimeSpan PrazoApos(int publicacoes)
    {
        int dobras = Math.Max(publicacoes - 1, 0);
        double prazo = Base.TotalSeconds * Math.Pow(2, Math.Min(dobras, 30));
        return prazo >= Maximo.TotalSeconds ? Maximo : TimeSpan.FromSeconds(prazo);
    }
}
