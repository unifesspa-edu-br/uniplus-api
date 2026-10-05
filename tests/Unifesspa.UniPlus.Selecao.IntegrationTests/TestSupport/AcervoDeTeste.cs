namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

/// <summary>
/// O endereço do acervo público para os testes que leem o contrato público sem baixar o arquivo: a
/// implementação de produção sobre um endereço base fixo.
/// </summary>
internal static class AcervoDeTeste
{
    public static IEnderecoNoAcervoPublico Endereco { get; } = new EnderecoNoAcervoPublico(
        Options.Create(new AcervoPublicoOptions { EnderecoBase = "https://acervo.teste" }));
}
