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
            FormatoDeDocumento.Odt,
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

    [Fact(DisplayName = "A chave do edital no acervo é a da ADR-0132: ano, unidade, tipo do ato, processo, ato e hash")]
    public void DoEdital_FixaAChaveDoEnderecoPublicado()
    {
        string chave = ChaveNoAcervoPublico.DoEdital(
            2026,
            "ceps",
            "EDITAL_RETIFICACAO",
            "psr-medicina-2027",
            new Guid("0199a000-0000-7000-8000-0000000000f0"),
            new string('e', 64),
            FormatoDeDocumento.Pdf);

        chave.Should().Be(
            "editais/2026/ceps/edital-retificacao/psr-medicina-2027/0199a000-0000-7000-8000-0000000000f0"
            + "/eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee.pdf");
    }

    [Fact(DisplayName = "A extensão da chave do documento principal segue o formato do arquivo")]
    public void DoEdital_SegueOFormatoNaExtensao()
    {
        string chave = ChaveNoAcervoPublico.DoEdital(
            2026,
            "ceps",
            "AVISO",
            "psr-medicina-2027",
            new Guid("0199a000-0000-7000-8000-0000000000f0"),
            new string('e', 64),
            FormatoDeDocumento.Docx);

        chave.Should().EndWith(".docx");
    }

    [Fact(DisplayName = "A chave do anexo do ato avulso é a do anexo do edital: processo, ato, anexo, hash e extensão do formato")]
    public void DoAnexo_PorIdentificadores_FixaAChave()
    {
        string chave = ChaveNoAcervoPublico.DoAnexo(
            new Guid("0199a000-0000-7000-8000-000000000001"),
            new Guid("0199a000-0000-7000-8000-0000000000f0"),
            new Guid("0199a000-0000-7000-8000-0000000000bb"),
            new string('d', 64),
            FormatoDeDocumento.Odt);

        chave.Should().Be(
            "selecao/processos-seletivos/0199a000-0000-7000-8000-000000000001"
            + "/atos/0199a000-0000-7000-8000-0000000000f0"
            + "/anexos/0199a000-0000-7000-8000-0000000000bb"
            + "/dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd.odt");
    }
}
