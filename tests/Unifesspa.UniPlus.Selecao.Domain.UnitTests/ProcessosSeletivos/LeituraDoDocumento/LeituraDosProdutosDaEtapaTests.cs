namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.EtapasLidasDeTeste;

/// <summary>Os atos que a etapa publica entram quando têm versão vigente; o que o catálogo não tem fica de fora e suspende o que se conclui da publicação.</summary>
public sealed class LeituraDosProdutosDaEtapaTests
{
    [Fact(DisplayName = "Os atos vigentes entram na etapa com o papel que o documento declara")]
    public void Produtos_Vigentes_EntramNaEtapa()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").Publicando(Preliminar(0, AtoDoResultado), Definitivo(1, AtoDoDefinitivo)));

        lidas.Etapas.Single().Produtos.Select(static produto => (produto.AtoCodigo, produto.Papel))
            .Should().BeEquivalentTo([(AtoDoResultado, PapelDaPublicacaoPrevista.Preliminar), (AtoDoDefinitivo, PapelDaPublicacaoPrevista.Definitivo)]);
    }

    [Fact(DisplayName = "O ato sem versão vigente fica de fora, aponta o campo do ato pela posição no documento e não repete o texto digitado")]
    public void Produto_SemVersaoVigente_ApontaOCampo()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").Publicando(Preliminar(0, AtoDoResultado), Preliminar(1, "ATO-DIGITADO")));

        lidas.Etapas.Single().Produtos.Should().ContainSingle();
        PartesRecusadas(lidas).Should().Equal(("/etapas/0/publicacoesPrevistas/1/atoCodigo", EtapaProcessoErrorCodes.AtoDoProdutoNaoEncontrado));
        Mensagens(lidas).Should().NotContain(mensagem => mensagem.Contains("DIGITADO", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A lista de produtos com parte recusada fica marcada para a etapa, que não julga o que se conclui dela")]
    public void Produto_Recusado_SuspendeAListaDeProdutos()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").Publicando(Preliminar(0, "ATO-DIGITADO")));

        Origem(lidas).PartesRecusadasDe("etapas[0]").Should().BeEquivalentTo(EtapaDeclarada.ListaDosProdutos);
        lidas.Etapas.Single().Declaracao(Origem(lidas).PartesRecusadasDe("etapas[0]")).ParteRecusadaEm(EtapaDeclarada.ListaDosProdutos).Should().BeTrue();
    }

    [Fact(DisplayName = "Dois atos inexistentes na mesma etapa acumulam, cada um na sua posição")]
    public void Produtos_Inexistentes_Acumulam()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").Publicando(Preliminar(0, "ATO-UM"), Definitivo(1, "ATO-DOIS")));

        PartesRecusadas(lidas).Select(static parte => parte.Ponteiro).Should().Equal(
            "/etapas/0/publicacoesPrevistas/0/atoCodigo", "/etapas/0/publicacoesPrevistas/1/atoCodigo");
    }
}
