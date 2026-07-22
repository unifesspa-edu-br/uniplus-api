namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Bônus regional do <see cref="ProcessoSeletivo"/> (RN05, Story #774,
/// modelagem P-B §2.5): referencia a regra tipada <c>BONUS-MULTIPLICATIVO</c>
/// do <c>rol_de_regras</c> e seus args (<see cref="Fator"/>, <see cref="Teto"/>).
/// </summary>
/// <remarks>
/// <strong>Toggle por presença (RN05, INV-B5):</strong> não existe
/// "BONUS-NENHUM" — a ausência desta entidade no processo já significa sem
/// bônus. A presença, com sua regra tipada, é o que habilita o bônus. O
/// bônus se aplica sobre a nota final, após os pesos (decisão do P.O.:
/// multiplicativo, ex. ×1,20, sem teto).
/// </remarks>
public sealed class ConfiguracaoBonusRegional : EntityBase
{
    public Guid ProcessoSeletivoId { get; private set; }
    public ReferenciaRegra Regra { get; private set; } = null!;
    public decimal Fator { get; private set; }
    public decimal? Teto { get; private set; }
    public string? MunicipioConvenio { get; private set; }
    public string? BaseLegal { get; private set; }

    private ConfiguracaoBonusRegional() { }

    public static Result<ConfiguracaoBonusRegional> Criar(
        ReferenciaRegra regra, decimal fator, decimal? teto, string? municipioConvenio, string? baseLegal)
    {
        ArgumentNullException.ThrowIfNull(regra);

        if (regra.Codigo != RegraBonusCodigo.Multiplicativo)
        {
            return Result<ConfiguracaoBonusRegional>.Failure(new DomainError(
                "ConfiguracaoBonusRegional.RegraInvalida",
                $"A regra {regra.Codigo} não é do código {RegraBonusCodigo.Multiplicativo}."));
        }

        if (fator <= 0)
        {
            return Result<ConfiguracaoBonusRegional>.Failure(new DomainError(
                "ConfiguracaoBonusRegional.FatorInvalido", "O fator do bônus deve ser maior que zero."));
        }

        if (teto is <= 0)
        {
            return Result<ConfiguracaoBonusRegional>.Failure(new DomainError(
                "ConfiguracaoBonusRegional.TetoInvalido", "O teto do bônus, quando informado, deve ser maior que zero."));
        }

        return Result<ConfiguracaoBonusRegional>.Success(new ConfiguracaoBonusRegional
        {
            Regra = regra,
            Fator = fator,
            Teto = teto,
            MunicipioConvenio = municipioConvenio,
            BaseLegal = baseLegal,
        });
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
