namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O edital de cada versão divulgada fica registrado na linha do certame, por ato.
/// </summary>
public sealed class CertameDivulgadoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Registrar o edital do mesmo ato duas vezes mantém uma entrada só")]
    public void RegistrarEdital_DoMesmoAto_EIdempotente()
    {
        CertameDivulgado divulgado = Novo();
        Guid ato = Guid.CreateVersion7();
        EditalDivulgado edital = new(ato, 1, "https://acervo.teste/a.pdf");

        divulgado.RegistrarEdital(edital).Should().BeTrue();
        divulgado.RegistrarEdital(edital with { UrlDownload = "https://acervo.teste/outro.pdf" }).Should().BeFalse();

        divulgado.EditaisDivulgados.Should().Equal([edital], "o endereço do ato é imutável: a reentrega não o substitui");
    }

    [Fact(DisplayName = "Atos diferentes acumulam o edital de cada um")]
    public void RegistrarEdital_DeAtosDiferentes_Acumula()
    {
        CertameDivulgado divulgado = Novo();
        EditalDivulgado abertura = new(Guid.CreateVersion7(), 1, "https://acervo.teste/a.pdf");
        EditalDivulgado retificacao = new(Guid.CreateVersion7(), 2, "https://acervo.teste/b.pdf");

        divulgado.RegistrarEdital(abertura);
        divulgado.RegistrarEdital(retificacao);

        divulgado.EditaisDivulgados.Should().Equal(abertura, retificacao);
    }

    private static CertameDivulgado Novo() => CertameDivulgado.Criar(
        Guid.CreateVersion7(),
        numeroVersao: 1,
        Guid.CreateVersion7(),
        new string('a', 64),
        "7",
        new FacetasDoCertameDivulgado("ps-2027", "PS 2027", null, ["AC"], Agora, Agora.AddDays(10)),
        "{}",
        Agora);
}
