namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// As regras de forma do formulário, as mesmas no processo e no modelo (ADR-0137): blocos que a
/// finalidade admite e exige, a revisão e aceite por último, e os itens em seções, na ordem delas.
/// </summary>
public sealed class EstruturaFormularioTests
{
    private static PaginaEstrutura Secao(string codigo, int ordem) => new(codigo, ordem, TipoPaginaFormulario.Secao, null);

    private static PaginaEstrutura Bloco(BlocoSistema bloco, int ordem) => new(bloco.ToString(), ordem, TipoPaginaFormulario.Bloco, bloco);

    [Fact(DisplayName = "Seções seguidas da revisão e aceite formam um formulário válido")]
    public void ValidarPaginas_Valido() =>
        EstruturaFormulario.ValidarPaginas(
            FinalidadeFormulario.Inscricao,
            [Secao("DADOS", 0), Bloco(BlocoSistema.ModalidadesCalculadas, 1), Bloco(BlocoSistema.RevisaoEAceite, 2)])
            .Should().BeEmpty();

    [Theory(DisplayName = "A finalidade decide os blocos: modalidades calculadas só na inscrição, e todo formulário termina na revisão e aceite")]
    [InlineData(FinalidadeFormulario.Habilitacao, EstruturaFormularioErrorCodes.BlocoNaoAdmitido)]
    [InlineData(FinalidadeFormulario.IsencaoTaxa, EstruturaFormularioErrorCodes.BlocoNaoAdmitido)]
    public void ValidarPaginas_BlocoNaoAdmitido(FinalidadeFormulario finalidade, string esperado) =>
        EstruturaFormulario.ValidarPaginas(
            finalidade, [Bloco(BlocoSistema.ModalidadesCalculadas, 0), Bloco(BlocoSistema.RevisaoEAceite, 1)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(esperado);

    [Fact(DisplayName = "A segunda página sem identificação do bloco aponta o caminho da página")]
    public void ValidarPaginas_SegundaPaginaSemBloco_ApontaPagina()
    {
        PaginaEstrutura[] paginas = [Secao("DADOS", 0), new("BLOCO", 1, TipoPaginaFormulario.Bloco, null)];

        IReadOnlyList<FieldError> recusas = EstruturaFormulario.ValidarPaginas(FinalidadeFormulario.Habilitacao, paginas);

        recusas.Should().ContainSingle(e => e.Error.Code == EstruturaFormularioErrorCodes.BlocoNaoAdmitido)
            .Which.Field.Should().Be("paginas[1].bloco");
    }

    [Fact(DisplayName = "Sem a revisão e aceite, o formulário é recusado")]
    public void ValidarPaginas_SemRevisao() =>
        EstruturaFormulario.ValidarPaginas(FinalidadeFormulario.Habilitacao, [Secao("DADOS", 0)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.BlocoExigidoAusente);

    [Fact(DisplayName = "A revisão e aceite fora da última posição é recusada")]
    public void ValidarPaginas_RevisaoForaDoFim() =>
        EstruturaFormulario.ValidarPaginas(FinalidadeFormulario.Habilitacao, [Bloco(BlocoSistema.RevisaoEAceite, 0), Secao("DADOS", 1)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.RevisaoEAceiteForaDoFim);

    [Fact(DisplayName = "Código e ordem de página repetidos acumulam as duas recusas")]
    public void ValidarPaginas_Repeticoes() =>
        EstruturaFormulario.ValidarPaginas(
            FinalidadeFormulario.Habilitacao, [Secao("DADOS", 0), Secao("DADOS", 0), Bloco(BlocoSistema.RevisaoEAceite, 1)])
            .Select(static e => e.Error.Code).Should().BeEquivalentTo(
                [EstruturaFormularioErrorCodes.PaginaCodigoDuplicado, EstruturaFormularioErrorCodes.PaginaOrdemDuplicada]);

    [Fact(DisplayName = "As recusas das páginas saem na ordem: cada página na sua posição, depois as do formulário")]
    public void ValidarPaginas_RecusasEmOrdemComCampoECodigo() =>
        EstruturaFormulario.ValidarPaginas(FinalidadeFormulario.Habilitacao, PaginasComTodasAsRecusas)
            .Select(static erro => (Campo: erro.Field, Codigo: erro.Error.Code)).Should().Equal(RecusasDasPaginasComTodasAsRecusas);

    private static readonly PaginaEstrutura[] PaginasComTodasAsRecusas =
    [
        new("DADOS", 0, TipoPaginaFormulario.Secao, BlocoSistema.ComprovacaoDocumental),
        new("DADOS", 1, TipoPaginaFormulario.Bloco, null),
        Bloco(BlocoSistema.ComprovacaoDocumental, 2),
        Bloco(BlocoSistema.ComprovacaoDocumental, 2),
        Bloco(BlocoSistema.ModalidadesCalculadas, 3),
    ];

    private static readonly (string? Campo, string Codigo)[] RecusasDasPaginasComTodasAsRecusas =
    [
        ("paginas[0].bloco", EstruturaFormularioErrorCodes.SecaoComBloco),
        ("paginas[1].codigo", EstruturaFormularioErrorCodes.PaginaCodigoDuplicado),
        ("paginas[1].bloco", EstruturaFormularioErrorCodes.BlocoNaoAdmitido),
        ("paginas[3].codigo", EstruturaFormularioErrorCodes.PaginaCodigoDuplicado),
        ("paginas[3].ordem", EstruturaFormularioErrorCodes.PaginaOrdemDuplicada),
        ("paginas[3].bloco", EstruturaFormularioErrorCodes.BlocoRepetido),
        ("paginas[4].bloco", EstruturaFormularioErrorCodes.BlocoNaoAdmitido),
        ("paginas", EstruturaFormularioErrorCodes.BlocoExigidoAusente),
    ];

    [Theory(DisplayName = "Item fora de seção — sem seção, em bloco ou em seção inexistente — é recusado")]
    [InlineData(null)]
    [InlineData("RevisaoEAceite")]
    [InlineData("INEXISTENTE")]
    public void ValidarItens_ForaDeSecao(string? pagina) =>
        EstruturaFormulario.ValidarItens(
            [Secao("DADOS", 0), Bloco(BlocoSistema.RevisaoEAceite, 1)], [new ItemEstrutura("COR_RACA", 0, pagina)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDeSecao);

    [Fact(DisplayName = "A ordem dos itens acompanha a das seções: item de seção anterior não vem depois")]
    public void ValidarItens_ForaDaOrdemDasSecoes()
    {
        PaginaEstrutura[] paginas = [Secao("DADOS", 0), Secao("ENDERECO", 1), Bloco(BlocoSistema.RevisaoEAceite, 2)];

        EstruturaFormulario.ValidarItens(paginas, [new ItemEstrutura("CEP", 0, "ENDERECO"), new ItemEstrutura("NOME", 1, "DADOS")])
            .Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDaOrdemDasSecoes);
        EstruturaFormulario.ValidarItens(paginas, [new ItemEstrutura("NOME", 0, "DADOS"), new ItemEstrutura("CEP", 1, "ENDERECO")])
            .Should().BeEmpty();
    }
}
