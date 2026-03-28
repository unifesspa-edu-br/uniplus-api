namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.SharedKernel.Domain.Entities;

public sealed class Cota : EntityBase
{
    public Guid EditalId { get; private set; }
    public ModalidadeConcorrencia Modalidade { get; private set; }
    public decimal PercentualVagas { get; private set; }
    public string? Descricao { get; private set; }

    private Cota() { }

    public static Cota Criar(Guid editalId, ModalidadeConcorrencia modalidade, decimal percentualVagas, string? descricao = null)
    {
        return new Cota
        {
            EditalId = editalId,
            Modalidade = modalidade,
            PercentualVagas = percentualVagas,
            Descricao = descricao
        };
    }
}
