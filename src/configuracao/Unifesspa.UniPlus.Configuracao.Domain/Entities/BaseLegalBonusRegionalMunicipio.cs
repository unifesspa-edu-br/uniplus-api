namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

public sealed class BaseLegalBonusRegionalMunicipio
{
    public string CodigoIbge { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string Uf { get; private set; } = string.Empty;

    private BaseLegalBonusRegionalMunicipio() { }

    internal BaseLegalBonusRegionalMunicipio(string codigoIbge, string nome, string uf)
    {
        CodigoIbge = codigoIbge.Trim();
        Nome = nome.Trim();
        Uf = uf.Trim().ToUpperInvariant();
    }
}
