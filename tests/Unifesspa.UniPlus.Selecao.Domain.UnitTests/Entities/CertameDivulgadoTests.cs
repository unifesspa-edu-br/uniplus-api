namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O edital de cada ato divulgado fica registrado na linha do certame, sem endereço até a cópia ao acervo.
/// </summary>
public sealed class CertameDivulgadoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly FacetasDoCertameDivulgado Facetas = new("ps-2027", "PS 2027", null, ["AC"], Agora, Agora.AddDays(10));

    [Fact(DisplayName = "A entrada do ato nasce sem endereço")]
    public void AbrirEntradaDoAto_NasceSemEndereco()
    {
        CertameDivulgado divulgado = Novo();
        Guid ato = Guid.CreateVersion7();

        divulgado.AbrirEntradaDoAto(ato, 1);

        divulgado.AtosDivulgados.Should().Equal(AtoDivulgado.SemEndereco(ato, 1));
        divulgado.AtosDivulgados[0].UrlDownload.Should().BeNull();
        divulgado.CopiouOsDocumentosDoAto(ato).Should().BeFalse();
    }

    [Fact(DisplayName = "Registrar duas vezes a entrada do mesmo ato é erro de programação")]
    public void AbrirEntradaDoAto_DoMesmoAto_Lanca()
    {
        CertameDivulgado divulgado = Novo();
        Guid ato = Guid.CreateVersion7();
        divulgado.AbrirEntradaDoAto(ato, 1);

        Action repetir = () => divulgado.AbrirEntradaDoAto(ato, 1);

        repetir.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Os endereços preenchem só a entrada do ato copiado")]
    public void RegistrarEnderecos_PreencheSoAEntradaDoAto()
    {
        (CertameDivulgado divulgado, Guid abertura, Guid retificacao) = ComAberturaERetificacaoPendentes();
        DocumentoDivulgado principal = new(PapelDoDocumentoDivulgado.Principal, "a.pdf", "application/pdf", 10, new string('b', 64), "https://acervo.teste/a.pdf");

        divulgado.RegistrarEnderecos(abertura, "https://acervo.teste/a.pdf", [principal]).Should().BeTrue();

        divulgado.AtosDivulgados.Should().BeEquivalentTo(
            [new AtoDivulgado(abertura, 1, "https://acervo.teste/a.pdf", [principal]), AtoDivulgado.SemEndereco(retificacao, 2)],
            opcoes => opcoes.WithStrictOrdering());
    }

    [Fact(DisplayName = "Só o ato que teve os endereços registrados conta como copiado")]
    public void CopiouOsDocumentosDoAto_SoDoAtoComEnderecos()
    {
        (CertameDivulgado divulgado, Guid abertura, Guid retificacao) = ComAberturaERetificacaoPendentes();

        divulgado.RegistrarEnderecos(abertura, "https://acervo.teste/a.pdf", []);

        divulgado.CopiouOsDocumentosDoAto(abertura).Should().BeTrue();
        divulgado.CopiouOsDocumentosDoAto(retificacao).Should().BeFalse();
    }

    [Fact(DisplayName = "Entrada que já tem endereço não muda na reentrega da cópia")]
    public void RegistrarEnderecos_NaEntradaPreenchida_NaoMuda()
    {
        CertameDivulgado divulgado = Novo();
        Guid ato = Guid.CreateVersion7();
        divulgado.AbrirEntradaDoAto(ato, 1);
        divulgado.RegistrarEnderecos(ato, "https://acervo.teste/a.pdf", []);

        bool mudou = divulgado.RegistrarEnderecos(ato, "https://acervo.teste/outro.pdf", []);

        mudou.Should().BeFalse();
        divulgado.AtosDivulgados[0].UrlDownload.Should().Be("https://acervo.teste/a.pdf", "o endereço do ato é imutável");
    }

    [Fact(DisplayName = "Registrar endereços de ato sem entrada é erro de programação")]
    public void RegistrarEnderecos_SemEntrada_Lanca()
    {
        CertameDivulgado divulgado = Novo();

        Action registrar = () => divulgado.RegistrarEnderecos(Guid.CreateVersion7(), "https://acervo.teste/a.pdf", []);

        registrar.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Avançar para versão mais nova troca a versão projetada")]
    public void Avancar_ParaVersaoMaisNova_TrocaAVersao()
    {
        CertameDivulgado divulgado = Novo();
        Guid ato = Guid.CreateVersion7();
        DateTimeOffset depois = Agora.AddDays(1);

        divulgado.Avancar(2, ato, new string('c', 64), "9", Facetas, "{\"v\":2}", depois);

        divulgado.NumeroVersao.Should().Be(2);
        divulgado.AtoCriadorId.Should().Be(ato);
        divulgado.DocumentoPublico.Should().Be("{\"v\":2}");
        divulgado.DivulgadoEm.Should().Be(depois);
    }

    [Theory(DisplayName = "Avançar para a mesma versão ou para uma anterior lança")]
    [InlineData(1)]
    [InlineData(0)]
    public void Avancar_SemSerMaisNova_Lanca(int numeroVersao)
    {
        CertameDivulgado divulgado = Novo();

        Action avancar = () => divulgado.Avancar(numeroVersao, Guid.CreateVersion7(), new string('c', 64), "9", Facetas, "{}", Agora);

        avancar.Should().Throw<InvalidOperationException>();
    }

    private static (CertameDivulgado Divulgado, Guid Abertura, Guid Retificacao) ComAberturaERetificacaoPendentes()
    {
        CertameDivulgado divulgado = Novo();
        Guid abertura = Guid.CreateVersion7();
        Guid retificacao = Guid.CreateVersion7();
        divulgado.AbrirEntradaDoAto(abertura, 1);
        divulgado.AbrirEntradaDoAto(retificacao, 2);

        return (divulgado, abertura, retificacao);
    }

    private static CertameDivulgado Novo() => CertameDivulgado.Criar(
        Guid.CreateVersion7(),
        numeroVersao: 1,
        Guid.CreateVersion7(),
        new string('a', 64),
        "7",
        Facetas,
        "{}",
        Agora);
}
