namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Uma data não útil (feriado ou recesso) dentro de um <see cref="CalendarioDiasUteis"/>.
/// </summary>
/// <remarks>
/// Entidade interna ao agregado <see cref="CalendarioDiasUteis"/>; criada apenas por
/// métodos do agregado, nunca por repositório próprio. Não implementa soft-delete:
/// deriva de <see cref="EntityBase"/> — corrigir o dataset é publicar uma nova versão
/// (novo <see cref="CalendarioDiasUteis"/>), não editar linhas de uma versão existente.
/// </remarks>
public sealed class DiaNaoUtil : EntityBase
{
    public Guid CalendarioDiasUteisId { get; private set; }
    public Abrangencia Abrangencia { get; private set; }
    public string? MunicipioIbge { get; private set; }
    public DateOnly Data { get; private set; }
    public string Descricao { get; private set; } = string.Empty;

    // EF Core materialization
    private DiaNaoUtil()
    {
    }

    internal static DiaNaoUtil Abrir(
        Guid calendarioDiasUteisId,
        Abrangencia abrangencia,
        string? municipioIbge,
        DateOnly data,
        string descricao)
    {
        return new DiaNaoUtil
        {
            CalendarioDiasUteisId = calendarioDiasUteisId,
            Abrangencia = abrangencia,
            MunicipioIbge = municipioIbge,
            Data = data,
            Descricao = descricao,
        };
    }
}
