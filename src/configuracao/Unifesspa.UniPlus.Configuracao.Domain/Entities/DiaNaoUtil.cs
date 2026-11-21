namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Uma data não útil (feriado ou recesso) dentro de um <see cref="CalendarioDiasUteis"/>.
/// </summary>
/// <remarks>
/// Entidade interna ao agregado <see cref="CalendarioDiasUteis"/>; criada apenas por
/// métodos do agregado (<see cref="CalendarioDiasUteis.Criar"/> ou
/// <see cref="CalendarioDiasUteis.IncluirDiaNaoUtil"/>), nunca por repositório
/// próprio. Não implementa soft-delete: deriva de <see cref="EntityBase"/> — uma vez
/// cadastrada, uma data não útil nunca é editada nem removida; a única mutação do
/// agregado é aditiva (api#1458).
/// </remarks>
public sealed class DiaNaoUtil : EntityBase
{
    public Guid CalendarioDiasUteisId { get; private set; }
    public Abrangencia Abrangencia { get; private set; }
    public string? MunicipioIbge { get; private set; }
    public string? MunicipioNome { get; private set; }
    public string? MunicipioUf { get; private set; }
    public string? Uf { get; private set; }
    public DateOnly Data { get; private set; }
    public string Descricao { get; private set; } = string.Empty;

    internal ChaveDoDiaNaoUtil Chave => new(Data, Abrangencia, MunicipioIbge, Uf);

    // EF Core materialization
    private DiaNaoUtil()
    {
    }

    internal static DiaNaoUtil Abrir(Guid calendarioDiasUteisId, DiaNaoUtilResolvido dia) => new()
    {
        CalendarioDiasUteisId = calendarioDiasUteisId,
        Abrangencia = dia.Abrangencia,
        MunicipioIbge = dia.MunicipioIbge,
        MunicipioNome = dia.MunicipioNome,
        MunicipioUf = dia.MunicipioUf,
        Uf = dia.Uf,
        Data = dia.Data,
        Descricao = dia.Descricao,
    };
}
