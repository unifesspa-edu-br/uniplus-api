namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Chaves;

/// <summary>Reserva as chaves das bancas das fases e etapas de um processo.</summary>
public sealed class ChavesDasBancasDoProcesso
{
    private readonly ChavesEmUso _chaves = new();

    public void Reservar(IEnumerable<ChaveLocal> chaves)
    {
        ArgumentNullException.ThrowIfNull(chaves);
        foreach (ChaveLocal chave in chaves)
        {
            _chaves.Adicionar(chave);
        }
    }

    public ChaveLocal Formar(string codigoDoTipo)
    {
        ChaveLocal chave = FormacaoDeChaveLocal.Formar("banca", codigoDoTipo, _chaves);
        _chaves.Adicionar(chave);
        return chave;
    }
}
