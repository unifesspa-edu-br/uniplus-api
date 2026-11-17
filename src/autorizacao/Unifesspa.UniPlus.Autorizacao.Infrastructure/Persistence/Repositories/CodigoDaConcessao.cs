namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Um código que a composição dá a uma concessão. O que vem de versão leva a classificação e o escopo que a versão guarda da permissão, base da
/// reclassificação quando a concessão não tem base gravada; o do perfil de governança não tem versão nem base.
/// </summary>
internal sealed class CodigoDaConcessao
{
    public Guid ConcessaoId { get; init; }

    public CodigoDePermissao Codigo { get; init; } = null!;

    public bool DeVersao { get; init; }

    public ClassificacaoDeProtecao? ClassificacaoNaVersao { get; init; }

    public bool DeUnidadeNaVersao { get; init; }
}
