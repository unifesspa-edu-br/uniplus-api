namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Um código que a composição dá a uma concessão. O que vem de versão leva o retrato da permissão nela, base da
/// reclassificação quando a concessão não tem base gravada; o do perfil de governança não tem versão nem base.
/// </summary>
internal sealed class CodigoDaConcessao
{
    public Guid ConcessaoId { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public bool DeVersao { get; init; }

    public ClassificacaoDeProtecao? RetratoDaClassificacao { get; init; }

    public bool RetratoDeUnidade { get; init; }
}
