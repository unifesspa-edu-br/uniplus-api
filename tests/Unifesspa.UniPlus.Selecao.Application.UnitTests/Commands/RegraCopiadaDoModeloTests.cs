namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// A recusa da gravação aponta a posição da regra na lista que o processo recebeu; a cópia do modelo a leva de volta ao
/// documento ou ao grupo do modelo de que a regra veio.
/// </summary>
public sealed class RegraCopiadaDoModeloTests
{
    private static readonly RegraCopiadaDoModelo[] Copiadas =
    [
        new(new RegraDeComprovacaoInformada("DOCUMENTO"), "conjuntoDeDocumentos.documentos[0]", []),
        new(new RegraDeComprovacaoInformada("QUANTIDADE_MINIMA_DE_ITENS"), "conjuntoDeDocumentos.grupos[0]", ["conjuntoDeDocumentos.grupos[0].documentos[0]", "conjuntoDeDocumentos.grupos[0].documentos[1]"]),
    ];

    private static FieldError Recusa(string? campo) => new(campo, new DomainError("codigo", "mensagem"));

    [Theory(DisplayName = "A recusa na posição de uma regra copiada aponta o documento ou o grupo do modelo")]
    [InlineData("regrasDeComprovacao[0].documento.momento.faseId", "conjuntoDeDocumentos.documentos[0].momento.faseId")]
    [InlineData("regrasDeComprovacao[0].documento.momento.formularioCodigo", "conjuntoDeDocumentos.documentos[0]")]
    [InlineData("regrasDeComprovacao[1].itens[1].documento.momento.formularioCodigo", "conjuntoDeDocumentos.grupos[0].documentos[1]")]
    [InlineData("regrasDeComprovacao[0].documento.indeferimento.abrePrazoParaReenvio", "conjuntoDeDocumentos.documentos[0].indeferimento.abrePrazoParaReenvio")]
    [InlineData("regrasDeComprovacao[1].itens[1].documento.momento.faseId", "conjuntoDeDocumentos.grupos[0].documentos[1].momento.faseId")]
    [InlineData("regrasDeComprovacao[1].cardinalidade.quantidadeMinima", "conjuntoDeDocumentos.grupos[0].quantidadeMinima")]
    public void NoModelo_PosicaoCopiada_ApontaOModelo(string campo, string esperado)
    {
        RegraCopiadaDoModelo.NoModelo(Recusa(campo), Copiadas).Field.Should().Be(esperado);
    }

    [Theory(DisplayName = "A recusa numa posição fora das copiadas perde o campo porque o modelo não a tem")]
    [InlineData("regrasDeComprovacao[2].documento.momento.faseId")]
    [InlineData("regrasDeComprovacao[7].itens[0].documento.momento.formularioCodigo")]
    public void NoModelo_PosicaoForaDasCopiadas_PerdeOCampo(string campo)
    {
        FieldError recusa = RegraCopiadaDoModelo.NoModelo(Recusa(campo), Copiadas);

        recusa.Field.Should().BeNull();
        recusa.Error.Should().Be(Recusa(campo).Error);
    }

    [Theory(DisplayName = "A recusa sem posição na lista segue como veio")]
    [InlineData(null)]
    [InlineData("formularioCodigo")]
    public void NoModelo_SemPosicao_SegueComoVeio(string? campo)
    {
        RegraCopiadaDoModelo.NoModelo(Recusa(campo), Copiadas).Field.Should().Be(campo);
    }
}
