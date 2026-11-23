namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>Os atos que a fase publica entram quando têm versão vigente; o que o catálogo não tem fica de fora e suspende o que se conclui da publicação.</summary>
public sealed class LeituraDosProdutosDaFaseTests
{
    [Fact(DisplayName = "Os atos vigentes entram na fase com o papel que o documento declara")]
    public void Produtos_Vigentes_EntramNaFase()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "resultado", "RESULTADO").Publicando(Preliminar(0, AtoDoResultado), Definitivo(1, AtoDoDefinitivo))));

        lidos.Cronograma.CronogramaFases.Single().Produtos.Select(static produto => (produto.AtoCodigo, produto.Papel))
            .Should().Equal((AtoDoResultado, PapelProdutoFase.Preliminar), (AtoDoDefinitivo, PapelProdutoFase.Definitivo));
    }

    [Fact(DisplayName = "O ato sem versão vigente fica de fora, aponta o campo do ato pela posição no documento e não repete o texto digitado")]
    public void Produto_SemVersaoVigente_ApontaOCampo()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "resultado", "RESULTADO").Publicando(Preliminar(0, AtoDoResultado), Preliminar(1, "ATO-DIGITADO"))));

        lidos.Cronograma.CronogramaFases.Single().Produtos.Should().ContainSingle();
        PartesRecusadas(lidos).Should().Equal(("/cronograma/fases/0/publicacoesPrevistas/1/atoCodigo", FaseCronogramaErrorCodes.AtoDoProdutoNaoEncontrado));
        Mensagens(lidos).Should().NotContain(mensagem => mensagem.Contains("DIGITADO", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A lista de produtos com parte recusada suspende o que se conclui da publicação de resultado das fases")]
    public void Produto_Recusado_SuspendeAPublicacaoDeResultado()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "resultado", "RESULTADO").Publicando(Preliminar(0, "ATO-DIGITADO"))));

        Cronograma(lidos).ProdutosComParteRecusadaEmAlgumaFase.Should().BeTrue();
        Cronograma(lidos).Fases.Single().ParteRecusadaEm(FaseDeclarada.ListaDosProdutos).Should().BeTrue();
    }

    [Fact(DisplayName = "Dois atos inexistentes na mesma fase acumulam, cada um na sua posição")]
    public void Produtos_Inexistentes_Acumulam()
    {
        IdentificacaoECronogramaLidos lidos = Ler(Documento(Fase(0, "resultado", "RESULTADO").Publicando(Preliminar(0, "ATO-UM"), Definitivo(1, "ATO-DOIS"))));

        PartesRecusadas(lidos).Select(static parte => parte.Ponteiro).Should().Equal(
            "/cronograma/fases/0/publicacoesPrevistas/0/atoCodigo", "/cronograma/fases/0/publicacoesPrevistas/1/atoCodigo");
    }
}
