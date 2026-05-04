namespace Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Configuração do <see cref="VendorMediaTypeAttribute"/>: identifica o recurso
/// e quais versões são aceitas no header <c>Accept</c>.
/// </summary>
/// <param name="Resource">Identificador curto do recurso na vendor MIME (ex.: <c>edital</c>, <c>inscricao</c>).</param>
/// <param name="Versions">Versões inteiras aceitas; a última posição é a versão "latest" usada como fallback.</param>
public sealed record VendorMediaTypeOptions(string Resource, IReadOnlyList<int> Versions);
