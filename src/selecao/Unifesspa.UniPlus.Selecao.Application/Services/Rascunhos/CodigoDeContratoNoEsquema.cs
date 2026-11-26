namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Um campo do esquema da configuração que guarda o código de um enum e tem o rótulo declarado ao lado, só para leitura.
/// </summary>
/// <param name="Localizacao">
/// Onde o esquema declara o campo de código (<c>arquivo.schema.json#/ponteiro</c>, o mesmo formato de
/// <see cref="Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao.FormaDoNo.Localizacoes"/>). O mesmo trecho
/// vale para todos os caminhos do documento em que o campo aparece.
/// </param>
/// <param name="RotuloDe">O rótulo do catálogo para o código, ou <see langword="null"/> quando o catálogo não conhece o código.</param>
public sealed record CodigoDeContratoNoEsquema(string Localizacao, Func<string, string?> RotuloDe)
{
    public static CodigoDeContratoNoEsquema De<TEnum>(string localizacao)
        where TEnum : struct, Enum =>
        new(localizacao, static codigo => CatalogoDe<TEnum>.TentarObter(codigo, out TEnum valor) ? valor.Rotulo() : null);
}
