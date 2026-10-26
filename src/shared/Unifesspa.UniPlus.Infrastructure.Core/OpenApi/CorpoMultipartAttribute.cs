namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>
/// Declara no contrato o corpo <c>multipart/form-data</c> de uma action que lê o formulário à mão. O
/// MVC só descreve corpo multipart a partir de parâmetros <c>[FromForm]</c>, e uma action que
/// precisa conferir o tamanho antes de qualquer leitura não pode tê-los — o vínculo leria o
/// formulário antes de a action poder responder 413.
/// </summary>
/// <param name="campoDoArquivo">O nome do campo que carrega o arquivo.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CorpoMultipartAttribute(string campoDoArquivo) : Attribute
{
    public string CampoDoArquivo { get; } = campoDoArquivo;

    /// <summary>Os campos de texto obrigatórios, além do arquivo.</summary>
    public string[] CamposDeTexto { get; init; } = [];
}
