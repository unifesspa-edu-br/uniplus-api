namespace Unifesspa.UniPlus.Infrastructure.Core.Storage;

/// <summary>
/// O acervo público (ADR-0132): o bucket dedicado ao documento de ato publicado, de leitura anônima
/// de objeto, e o endereço pelo qual a borda o serve. Ligadas via
/// <see cref="DependencyInjection.AcervoPublicoServiceCollectionExtensions.AddUniPlusAcervoPublico"/>.
/// </summary>
public sealed class AcervoPublicoOptions
{
    public const string SectionName = "AcervoPublico";

    /// <summary>
    /// Nome do bucket público. A aplicação nunca o cria nem lhe aplica política: ele é provisionado
    /// pela infraestrutura, já com a leitura anônima de objeto.
    /// </summary>
    public string Bucket { get; init; } = string.Empty;

    /// <summary>
    /// Endereço absoluto pelo qual o acervo é lido, ao qual a chave do objeto é acrescentada para
    /// formar o link divulgado. É o nome que a borda publica, e não a porta de dados do
    /// armazenamento — o link nunca é pré-assinado. Fora de Development é obrigatório e
    /// <c>https</c>: um valor ausente ou errado produziria links quebrados em documento publicado.
    /// </summary>
    public string? EnderecoBase { get; init; }
}
