namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

public sealed class Etapa : EntityBase
{
    public Guid EditalId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public TipoEtapa Tipo { get; private set; }
    public decimal Peso { get; private set; }
    public int Ordem { get; private set; }
    public decimal? NotaMinima { get; private set; }
    public bool Eliminatoria { get; private set; }

    private Etapa() { }

    public static Etapa Criar(Guid editalId, string nome, TipoEtapa tipo, decimal peso, int ordem, bool eliminatoria = false, decimal? notaMinima = null)
    {
        return new Etapa
        {
            EditalId = editalId,
            Nome = nome,
            Tipo = tipo,
            Peso = peso,
            Ordem = ordem,
            Eliminatoria = eliminatoria,
            NotaMinima = notaMinima
        };
    }
}
