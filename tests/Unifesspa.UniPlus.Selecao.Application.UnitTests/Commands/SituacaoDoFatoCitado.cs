namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Como a versão publicada congelou o fato citado pela exigência de documento e como o catálogo vivo o
/// descreve hoje. O padrão é o fato que nada mudou desde a publicação.
/// </summary>
internal sealed record SituacaoDoFatoCitado
{
    /// <summary>O domínio que a versão publicada congelou para o fato.</summary>
    public string DominioCongelado { get; init; } = "BOOLEANO";

    /// <summary>O domínio que o catálogo vivo declara hoje para o fato.</summary>
    public string DominioNoCatalogo { get; init; } = "BOOLEANO";

    /// <summary>A classificação de proteção que o catálogo vivo declara hoje para o fato.</summary>
    public string ClassificacaoNoCatalogo { get; init; } = "PESSOAL";

    /// <summary>Se o registro de codecs consegue reidratar a versão publicada.</summary>
    public bool Reidratavel { get; init; } = true;

    /// <summary>A versão publicada coletou o fato como data, e o processo o coleta como booleano.</summary>
    public bool ItemPublicadoComoData { get; init; }

    /// <summary>O processo coleta, além do fato citado, um fato que nenhuma versão publicada coletou.</summary>
    public bool ComItemNovo { get; init; }

    public TipoRenderizacao TipoDoItemPublicado => ItemPublicadoComoData ? TipoRenderizacao.Data : TipoRenderizacao.Booleano;

    public bool PublicadoDivergeDoProcesso => ItemPublicadoComoData || ComItemNovo;
}
