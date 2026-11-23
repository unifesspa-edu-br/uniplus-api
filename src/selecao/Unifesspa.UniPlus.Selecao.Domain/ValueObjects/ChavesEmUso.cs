namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ChavesEmUso
{
    private readonly HashSet<ChaveLocal> _chaves = [];

    public bool Adicionar(ChaveLocal chave) => _chaves.Add(chave);

    public bool Contem(ChaveLocal chave) => _chaves.Contains(chave);
}
