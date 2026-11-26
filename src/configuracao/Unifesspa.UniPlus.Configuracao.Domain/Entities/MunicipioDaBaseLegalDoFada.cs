namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

public sealed class MunicipioDaBaseLegalDoFada
{
    public string CodigoIbge { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string Uf { get; private set; } = string.Empty;

    private MunicipioDaBaseLegalDoFada() { }

    internal MunicipioDaBaseLegalDoFada(string codigoIbge, string nome, string uf)
    {
        CodigoIbge = codigoIbge.Trim();
        Nome = nome.Trim();
        Uf = uf.Trim().ToUpperInvariant();
    }
}
