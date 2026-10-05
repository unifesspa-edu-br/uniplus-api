namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ChaveNoAcervoPublicoTests
{
    [Fact(DisplayName = "A chave do modelo no acervo é fixa: processo, ato, modelo e hash do arquivo, com a extensão do formato")]
    public void DoModelo_FixaAChaveDoEnderecoPublicado()
    {
        // A chave compõe o endereço divulgado no edital: mudar a fórmula quebra todo link já citado.
        ModeloDaExigencia modelo = new(
            new Guid("0199a000-0000-7000-8000-0000000000aa"),
            "Autodeclaração.odt",
            FormatoDeModelo.Odt,
            new string('d', 64));

        string chave = ChaveNoAcervoPublico.DoModelo(
            new Guid("0199a000-0000-7000-8000-000000000001"),
            new Guid("0199a000-0000-7000-8000-0000000000f0"),
            modelo);

        chave.Should().Be(
            "selecao/processos-seletivos/0199a000-0000-7000-8000-000000000001"
            + "/atos/0199a000-0000-7000-8000-0000000000f0"
            + "/modelos-de-documento/0199a000-0000-7000-8000-0000000000aa"
            + "/dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd.odt");
    }
}
