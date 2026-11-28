namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// As regras de forma do formulário, as mesmas no processo e no modelo (ADR-0137): blocos que a
/// declaração admite e exige, a revisão e aceite por último, e os itens em seções, na ordem delas.
/// </summary>
public sealed class EstruturaFormularioTests
{
    private static PaginaEstrutura Secao(string codigo, int ordem) => new(codigo, ordem, TipoPaginaFormulario.Secao, null);

    private static PaginaEstrutura Bloco(BlocoSistema bloco, int ordem) => new(bloco.ToString(), ordem, TipoPaginaFormulario.Bloco, bloco);

    private static readonly DeclaracaoDoFormulario ServeAInscricao = new(ServeAFaseDeInscricao: true, Obrigatorio: true);

    private static readonly DeclaracaoDoFormulario ObrigatorioForaDaInscricao = new(ServeAFaseDeInscricao: false, Obrigatorio: true);

    [Theory(DisplayName = "As modalidades calculadas são admitidas só quando o formulário serve à fase de inscrição, seja ele obrigatório ou não")]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void BlocosAdmitidos_ModalidadesSoQuandoServeAInscricao(bool serveAFaseDeInscricao, bool obrigatorio, bool admiteModalidades)
    {
        IReadOnlySet<BlocoSistema> admitidos = EstruturaFormulario.BlocosAdmitidos(new DeclaracaoDoFormulario(serveAFaseDeInscricao, obrigatorio));

        admitidos.Contains(BlocoSistema.ModalidadesCalculadas).Should().Be(admiteModalidades);
        admitidos.Should().Contain([BlocoSistema.ComprovacaoDocumental, BlocoSistema.RevisaoEAceite]);
    }

    [Fact(DisplayName = "Todo formulário exige a revisão e aceite, qualquer que seja a declaração")]
    public void BlocosExigidos_SoARevisaoEAceite() =>
        EstruturaFormulario.BlocosExigidos().Should().BeEquivalentTo([BlocoSistema.RevisaoEAceite]);

    [Fact(DisplayName = "Seções seguidas da revisão e aceite formam um formulário válido")]
    public void ValidarPaginas_Valido() =>
        EstruturaFormulario.ValidarPaginas(
            ServeAInscricao,
            [Secao("DADOS", 0), Bloco(BlocoSistema.ModalidadesCalculadas, 1), Bloco(BlocoSistema.RevisaoEAceite, 2)])
            .Should().BeEmpty();

    [Fact(DisplayName = "A página recusada não muda a posição das outras: o código repetido aponta a posição do pedido")]
    public void ValidarCadaPagina_PaginaRecusadaNaoMudaAPosicao() =>
        EstruturaFormulario.ValidarCadaPagina(ServeAInscricao, [Secao("DADOS", 0), null, Secao("DADOS", 2)])
            .Select(static e => (e.Field, e.Error.Code)).Should().Equal(("paginas[2].codigo", EstruturaFormularioErrorCodes.PaginaCodigoDuplicado));

    [Fact(DisplayName = "Cada página não confere o conjunto: sem revisão e aceite e com página recusada não há bloco exigido ausente nem revisão fora do fim")]
    public void ValidarCadaPagina_NaoConfereOConjunto() =>
        EstruturaFormulario.ValidarCadaPagina(ServeAInscricao, [Secao("DADOS", 0), null]).Should().BeEmpty();

    [Fact(DisplayName = "Cada página não confere a revisão e aceite no fim, que só se conclui com todas as páginas")]
    public void ValidarCadaPagina_NaoConfereARevisaoNoFim() =>
        EstruturaFormulario.ValidarCadaPagina(ServeAInscricao, [Bloco(BlocoSistema.RevisaoEAceite, 0), null, Secao("DADOS", 2)]).Should().BeEmpty();

    [Fact(DisplayName = "A seção com bloco é recusada ao lado de página recusada")]
    public void ValidarCadaPagina_SecaoComBloco() =>
        EstruturaFormulario.ValidarCadaPagina(
            ServeAInscricao, [null, new PaginaEstrutura("DADOS", 1, TipoPaginaFormulario.Secao, BlocoSistema.ComprovacaoDocumental)])
            .Select(static e => (e.Field, e.Error.Code)).Should().Equal(("paginas[1].bloco", EstruturaFormularioErrorCodes.SecaoComBloco));

    [Fact(DisplayName = "O bloco não admitido é recusado ao lado de página recusada")]
    public void ValidarCadaPagina_BlocoNaoAdmitido() =>
        EstruturaFormulario.ValidarCadaPagina(ObrigatorioForaDaInscricao, [null, Bloco(BlocoSistema.ModalidadesCalculadas, 1)])
            .Select(static e => (e.Field, e.Error.Code)).Should().Equal(("paginas[1].bloco", EstruturaFormularioErrorCodes.BlocoNaoAdmitido));

    [Fact(DisplayName = "O bloco repetido é recusado na segunda posição, ao lado de página recusada")]
    public void ValidarCadaPagina_BlocoRepetido() =>
        EstruturaFormulario.ValidarCadaPagina(
            ServeAInscricao, [Bloco(BlocoSistema.ComprovacaoDocumental, 0), null, new PaginaEstrutura("OUTRO", 2, TipoPaginaFormulario.Bloco, BlocoSistema.ComprovacaoDocumental)])
            .Select(static e => (e.Field, e.Error.Code)).Should().Equal(("paginas[2].bloco", EstruturaFormularioErrorCodes.BlocoRepetido));

    [Theory(DisplayName = "As recusas com uma página trocada por nula estão contidas nas do pedido completo, no mesmo campo e código")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ValidarCadaPagina_SubconjuntoDoPedidoCompleto(int posicaoRecusada)
    {
        PaginaEstrutura?[] completo =
        [
            Secao("DADOS", 0), Secao("DADOS", 0), Bloco(BlocoSistema.ModalidadesCalculadas, 2), Bloco(BlocoSistema.ModalidadesCalculadas, 3),
        ];
        PaginaEstrutura?[] comRecusada = [.. completo];
        comRecusada[posicaoRecusada] = null;

        IEnumerable<(string?, string)> doPedidoCompleto = EstruturaFormulario.ValidarPaginas(ObrigatorioForaDaInscricao, [.. completo.OfType<PaginaEstrutura>()])
            .Select(static e => (e.Field, e.Error.Code));
        IEnumerable<(string?, string)> comPaginaRecusada = EstruturaFormulario.ValidarCadaPagina(ObrigatorioForaDaInscricao, comRecusada)
            .Select(static e => (e.Field, e.Error.Code));

        comPaginaRecusada.Should().BeSubsetOf(doPedidoCompleto);
    }

    [Theory(DisplayName = "A declaração decide os blocos: o formulário que não serve à fase de inscrição recusa as modalidades calculadas")]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ValidarPaginas_BlocoNaoAdmitido(bool serveAFaseDeInscricao, bool obrigatorio) =>
        EstruturaFormulario.ValidarPaginas(
            new DeclaracaoDoFormulario(serveAFaseDeInscricao, obrigatorio),
            [Bloco(BlocoSistema.ModalidadesCalculadas, 0), Bloco(BlocoSistema.RevisaoEAceite, 1)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.BlocoNaoAdmitido);

    [Fact(DisplayName = "O formulário que serve à fase de inscrição sem ser obrigatório também admite as modalidades calculadas")]
    public void ValidarPaginas_ServeAInscricaoSemSerObrigatorio_AdmiteModalidades() =>
        EstruturaFormulario.ValidarPaginas(
            new DeclaracaoDoFormulario(ServeAFaseDeInscricao: true, Obrigatorio: false),
            [Bloco(BlocoSistema.ModalidadesCalculadas, 0), Bloco(BlocoSistema.RevisaoEAceite, 1)])
            .Should().BeEmpty();

    [Fact(DisplayName = "A segunda página sem identificação do bloco aponta o caminho da página")]
    public void ValidarPaginas_SegundaPaginaSemBloco_ApontaPagina()
    {
        PaginaEstrutura[] paginas = [Secao("DADOS", 0), new("BLOCO", 1, TipoPaginaFormulario.Bloco, null)];

        IReadOnlyList<FieldError> recusas = EstruturaFormulario.ValidarPaginas(ObrigatorioForaDaInscricao, paginas);

        recusas.Should().ContainSingle(e => e.Error.Code == EstruturaFormularioErrorCodes.BlocoNaoAdmitido)
            .Which.Field.Should().Be("paginas[1].bloco");
    }

    [Fact(DisplayName = "Sem a revisão e aceite, o formulário é recusado")]
    public void ValidarPaginas_SemRevisao() =>
        EstruturaFormulario.ValidarPaginas(ObrigatorioForaDaInscricao, [Secao("DADOS", 0)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.BlocoExigidoAusente);

    [Fact(DisplayName = "A revisão e aceite fora da última posição é recusada")]
    public void ValidarPaginas_RevisaoForaDoFim() =>
        EstruturaFormulario.ValidarPaginas(ObrigatorioForaDaInscricao, [Bloco(BlocoSistema.RevisaoEAceite, 0), Secao("DADOS", 1)])
            .Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.RevisaoEAceiteForaDoFim);

    [Fact(DisplayName = "Código e ordem de página repetidos acumulam as duas recusas")]
    public void ValidarPaginas_Repeticoes() =>
        EstruturaFormulario.ValidarPaginas(
            ObrigatorioForaDaInscricao, [Secao("DADOS", 0), Secao("DADOS", 0), Bloco(BlocoSistema.RevisaoEAceite, 1)])
            .Select(static e => e.Error.Code).Should().BeEquivalentTo(
                [EstruturaFormularioErrorCodes.PaginaCodigoDuplicado, EstruturaFormularioErrorCodes.PaginaOrdemDuplicada]);

    [Fact(DisplayName = "As recusas das páginas saem na ordem: cada página na sua posição, depois as do formulário")]
    public void ValidarPaginas_RecusasEmOrdemComCampoECodigo() =>
        EstruturaFormulario.ValidarPaginas(ObrigatorioForaDaInscricao, PaginasComTodasAsRecusas)
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
