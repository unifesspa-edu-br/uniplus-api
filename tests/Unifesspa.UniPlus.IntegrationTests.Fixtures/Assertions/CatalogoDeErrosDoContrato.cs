namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

/// <summary>
/// O endereço do catálogo público de erros, tal como o contrato o publica: o <c>type</c> de toda
/// recusa é esta base mais o <c>code</c>, e a página de cada <c>code</c> mora sob ela no portal.
/// É o CONTRATO, escrito por extenso de propósito, e não uma cópia da configuração do host:
/// ler a configuração faria o teste comparar o host com ele mesmo. A concordância entre as duas
/// pontas é conferida por um teste em <c>Host.IntegrationTests</c>.
/// </summary>
public static class CatalogoDeErrosDoContrato
{
    public const string Base = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";
}
