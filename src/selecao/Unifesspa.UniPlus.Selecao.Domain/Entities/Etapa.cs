namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

public sealed class Etapa : EntityBase
{
    public Guid EditalId { get; private set; }
    public string Nome { get; private set; } = string.Empty;

    /// <summary>
    /// FK preparatória para a futura entidade <c>TipoEtapa</c> (Story #455).
    /// Permanece <c>null</c> nesta Story #454 — a entidade ainda não existe;
    /// será populada quando a promoção do enum
    /// <c>Enums.TipoEtapa</c> for concluída.
    /// FK não-nula entra em migration futura quando dados existirem.
    /// </summary>
    public Guid? TipoEtapaId { get; private set; }
    public decimal Peso { get; private set; }
    public int Ordem { get; private set; }
    public decimal? NotaMinima { get; private set; }
    public bool Eliminatoria { get; private set; }

    private Etapa() { }

    public static Etapa Criar(
        Guid editalId,
        string nome,
        decimal peso,
        int ordem,
        bool eliminatoria = false,
        decimal? notaMinima = null,
        Guid? tipoEtapaId = null)
    {
        // Invariante de factory simétrica a Edital.Criar: opcional (null) é
        // válido, Guid.Empty é estado inválido (informado mas vazio).
        if (tipoEtapaId.HasValue && tipoEtapaId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "TipoEtapaId não pode ser Guid vazio. Omita o argumento para nulo.",
                nameof(tipoEtapaId));
        }

        return new Etapa
        {
            EditalId = editalId,
            Nome = nome,
            Peso = peso,
            Ordem = ordem,
            Eliminatoria = eliminatoria,
            NotaMinima = notaMinima,
            TipoEtapaId = tipoEtapaId,
        };
    }
}
