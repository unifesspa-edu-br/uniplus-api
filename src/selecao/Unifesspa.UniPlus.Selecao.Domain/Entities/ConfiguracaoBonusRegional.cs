namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Bônus regional do <see cref="ProcessoSeletivo"/> (RN05, Story #774):
/// referencia a regra tipada <c>BONUS-MULTIPLICATIVO</c>
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
    /// <summary>Alinhado a <c>ConfiguracaoBonusRegionalConfiguration</c> (varchar(200)).</summary>
    public const int MunicipioConvenioMaxLength = 200;

    /// <summary>Alinhado a <c>ConfiguracaoBonusRegionalConfiguration</c> (varchar(500)).</summary>
    public const int BaseLegalMaxLength = 500;

    public Guid ProcessoSeletivoId { get; private set; }
    public ReferenciaRegra Regra { get; private set; } = null!;
    public decimal Fator { get; private set; }
    public decimal? Teto { get; private set; }
    public string? MunicipioConvenio { get; private set; }
    public string? BaseLegal { get; private set; }

    private ConfiguracaoBonusRegional() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — o array
    /// <c>errors[]</c> do contrato público (ADR-0023) precisa de todas as regras violadas no
    /// mesmo lote. Os limites de tamanho de <see cref="MunicipioConvenio"/>/<see cref="BaseLegal"/>
    /// não existiam aqui antes — só no validator — deixando o domínio aceitar um valor que só
    /// falhava em <c>SaveChanges</c> com erro de banco em vez de 422.
    /// </summary>
    public static Result<ConfiguracaoBonusRegional> Criar(
        ReferenciaRegra regra, decimal fator, decimal? teto, string? municipioConvenio, string? baseLegal)
    {
        ArgumentNullException.ThrowIfNull(regra);

        List<FieldError> erros = [];

        if (regra.Codigo != RegraBonusCodigo.Multiplicativo)
        {
            erros.Add(new("regraCodigo", new DomainError(
                "ConfiguracaoBonusRegional.RegraInvalida",
                $"A regra do bônus precisa ser do código {RegraBonusCodigo.Multiplicativo}.")));
        }

        if (fator <= 0)
        {
            erros.Add(new("fator", new DomainError(
                "ConfiguracaoBonusRegional.FatorInvalido", "O fator do bônus deve ser maior que zero.")));
        }

        if (teto is <= 0)
        {
            erros.Add(new("teto", new DomainError(
                "ConfiguracaoBonusRegional.TetoInvalido", "O teto do bônus, quando informado, deve ser maior que zero.")));
        }

        if (municipioConvenio is { Length: > MunicipioConvenioMaxLength })
        {
            erros.Add(new("municipioConvenio", new DomainError(
                "ConfiguracaoBonusRegional.MunicipioConvenioTamanho",
                $"Município do convênio deve ter no máximo {MunicipioConvenioMaxLength} caracteres.")));
        }

        if (baseLegal is { Length: > BaseLegalMaxLength })
        {
            erros.Add(new("baseLegal", new DomainError(
                "ConfiguracaoBonusRegional.BaseLegalTamanho",
                $"Base legal deve ter no máximo {BaseLegalMaxLength} caracteres.")));
        }

        if (erros.Count > 0)
        {
            return Result<ConfiguracaoBonusRegional>.ValidationFailure(erros);
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
