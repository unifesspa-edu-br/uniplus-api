namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;
using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ConteudosDoModeloDeTeste;

/// <summary>O conteúdo do modelo na forma em que é conferido e gravado, e na ordem em que é gravado.</summary>
public sealed class ConteudoDoModeloFormaGravadaTests
{
    private const string CComCedilhaDecomposto = "Ç";

    [Fact(DisplayName = "O título é aparado, e o título em branco fica ausente")]
    public void NaFormaGravada_AparaOTitulo()
    {
        (Vazio with { Titulo = "  Habilitação  " }).NaFormaGravada().Titulo.Should().Be("Habilitação");
        (Vazio with { Titulo = "   " }).NaFormaGravada().Titulo.Should().BeNull();
    }

    [Fact(DisplayName = "Os códigos de página, item, grupo e campo de grupo ficam como enviados")]
    public void NaFormaGravada_CodigosDeEstrutura_FicamComoEnviados()
    {
        ConteudoDoModelo conteudo = Vazio with
        {
            Paginas = [Pagina($" PAGINA{CComCedilhaDecomposto} ")],
            Itens = [Item($" ITEM{CComCedilhaDecomposto}")],
            Grupos = [Grupo($"GRUPO{CComCedilhaDecomposto}", 0, null, Item($"CAMPO{CComCedilhaDecomposto}"))],
        };

        ConteudoDoModelo gravado = conteudo.NaFormaGravada();

        gravado.Paginas.Single().Codigo.Should().Be($" PAGINA{CComCedilhaDecomposto} ");
        gravado.Itens.Single().FatoCodigo.Should().Be($" ITEM{CComCedilhaDecomposto}");
        gravado.Grupos.Single().Codigo.Should().Be($"GRUPO{CComCedilhaDecomposto}");
        gravado.Grupos.Single().Subitens.Single().FatoCodigo.Should().Be($"CAMPO{CComCedilhaDecomposto}");
    }

    [Fact]
    public void NaFormaGravada_CitacoesDePagina_FicamComoEnviadas()
    {
        ItemDoModelo campo = Item("FATO") with { PaginaCodigo = " PAGINA " };
        ConteudoDoModelo conteudo = Vazio with { Itens = [campo], Grupos = [Grupo("GRUPO", 0, null, campo) with { PaginaCodigo = " PAGINA " }] };

        ConteudoDoModelo gravado = conteudo.NaFormaGravada();

        gravado.Itens.Single().PaginaCodigo.Should().Be(" PAGINA ");
        gravado.Grupos.Single().PaginaCodigo.Should().Be(" PAGINA ");
        gravado.Grupos.Single().Subitens.Single().PaginaCodigo.Should().Be(" PAGINA ");
    }

    [Theory]
    [InlineData(false, "conteudo.itens[0].paginaCodigo")]
    [InlineData(true, "conteudo.itens[0].paginaCodigo")]
    public void Conferir_CitacaoDePaginaComEspacos_Recusa(bool grupo, string campo)
    {
        ItemDoModelo item = Item("FATO") with { PaginaCodigo = grupo ? null : " DADOS " };
        ConteudoDoModelo conteudo = Vazio with
        {
            Paginas = [Pagina(), new("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null)],
            Itens = grupo ? [] : [item],
            Grupos = grupo ? [Grupo("GRUPO", 0, null, item) with { PaginaCodigo = " DADOS " }] : []
        };

        List<FieldError> erros = ConferenciaDoConteudoDoModelo.Conferir(DeOutraFaseObrigatoria, conteudo.NaFormaGravada(), new Dictionary<string, IReadOnlyCollection<string>>());

        erros.Should().ContainSingle(erro => erro.Field == campo && erro.Error.Code == EstruturaFormularioErrorCodes.ItemForaDeSecao);
    }

    [Fact(DisplayName = "O código do termo fica como enviado; pressuposto e documento ficam como enviados")]
    public void NaFormaGravada_CodigosDeTermoPressupostoEDocumentoComoEnviados()
    {
        ConteudoDoModelo conteudo = Vazio with
        {
            Termos = [Termo($"TERMO{CComCedilhaDecomposto} ")],
            Pressupostos = [$" PRESSUPOSTO{CComCedilhaDecomposto}"],
            ConjuntoDeDocumentos = Conjunto(Documento($" TIPO{CComCedilhaDecomposto}") with { FaseCodigo = $"FASE{CComCedilhaDecomposto}" }),
        };

        ConteudoDoModelo gravado = conteudo.NaFormaGravada();

        gravado.Termos.Single().Codigo.Should().Be($"TERMO{CComCedilhaDecomposto} ");
        gravado.Pressupostos.Should().Equal($" PRESSUPOSTO{CComCedilhaDecomposto}");
        gravado.Documentos.DocumentosExigidos.Single().TipoDocumentoCodigo.Should().Be($" TIPO{CComCedilhaDecomposto}");
        gravado.Documentos.DocumentosExigidos.Single().FaseCodigo.Should().Be($"FASE{CComCedilhaDecomposto}");
    }

    [Fact(DisplayName = "O fato e os valores da exigência por opção do documento ficam como enviados")]
    public void NaFormaGravada_ExigenciaPorOpcaoComoEnviada()
    {
        ConteudoDoModelo conteudo = Vazio with
        {
            ConjuntoDeDocumentos = Conjunto(Documento("LAUDO", porOpcao: new($" MODALIDADE{CComCedilhaDecomposto}", [$" AC{CComCedilhaDecomposto} "]))),
        };

        ExigenciaPorOpcao porOpcao = conteudo.NaFormaGravada().Documentos.DocumentosExigidos.Single().PorOpcao!;

        porOpcao.FatoCodigo.Should().Be($" MODALIDADE{CComCedilhaDecomposto}");
        porOpcao.Exceto.Should().Equal($" AC{CComCedilhaDecomposto} ");
    }

    [Fact(DisplayName = "A fase da referência do prazo de emissão e da validade fica como enviada")]
    public void NaFormaGravada_FaseDasReferenciasDosRequisitosComoEnviada()
    {
        ConteudoDoModelo conteudo = Vazio with
        {
            ConjuntoDeDocumentos = Conjunto(Documento("LAUDO", requisitos: RequisitosAPartirDasFases($" FASE{CComCedilhaDecomposto}", $"OUTRA{CComCedilhaDecomposto} "))),
        };

        RequisitosDoDocumentoDoModelo requisitos = conteudo.NaFormaGravada().Documentos.DocumentosExigidos.Single().Requisitos;

        requisitos.PrazoDeEmissao!.Referencia!.FaseCodigo.Should().Be($" FASE{CComCedilhaDecomposto}");
        requisitos.Validade!.Referencia!.FaseCodigo.Should().Be($"OUTRA{CComCedilhaDecomposto} ");
    }

    [Fact(DisplayName = "Os requisitos ainda não escolhidos e a referência sem fase ficam como estão na forma gravada")]
    public void NaFormaGravada_RequisitosAusentesOuSemFase_FicamComoEstao()
    {
        RequisitosDoDocumentoDoModelo semFase = new(
            new PrazoDeEmissaoDoModelo(
                EscolhaDoPrazoDeEmissao.ComPrazo, 1, UnidadeDoPrazoDeEmissao.Anos,
                new DataDeReferenciaDoDocumentoDoModelo(TipoDaDataDeReferencia.FimDasInscricoes, null, null), EfeitoNaAnaliseDoDocumento.AceitarComAviso),
            null);
        ConteudoDoModelo conteudo = Vazio with
        {
            ConjuntoDeDocumentos = Conjunto(Documento("LAUDO"), Documento("RG", requisitos: semFase)),
        };

        IReadOnlyList<DocumentoExigidoDoModelo> documentos = conteudo.NaFormaGravada().Documentos.DocumentosExigidos;

        documentos[0].Requisitos.Should().Be(RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos);
        documentos[1].Requisitos.Should().Be(semFase);
    }

    [Fact(DisplayName = "Rótulo, título de pagina e texto opcional ficam aparados, e o texto opcional em branco fica ausente")]
    public void NaFormaGravada_AparaOsTextos()
    {
        ItemDoModelo campo = (Item("RENDA").ComRotulo("  Renda  ").ComAjuda("  Informe  ")) with { Formato = "   " };

        ConteudoDoModelo gravado = (Vazio with { Paginas = [Pagina() with { Titulo = " Dados ", Aviso = "  " }], Itens = [campo] }).NaFormaGravada();

        gravado.Paginas.Single().Titulo.Should().Be("Dados");
        gravado.Paginas.Single().Aviso.Should().BeNull();
        gravado.Itens.Single().Apresentacao.Rotulo.Should().Be("Renda");
        gravado.Itens.Single().Apresentacao.Ajuda.Should().Be("Informe");
        gravado.Itens.Single().Formato.Should().BeNull();
    }

    [Fact(DisplayName = "Os documentos de um grupo ficam na forma gravada, nas mesmas posições, e o grupo mantém a quantidade e o indeferimento")]
    public void NaFormaGravada_DocumentosDeGrupo_CodigosComoEnviados()
    {
        IndeferimentoDoGrupoDeDocumentosDoModelo indeferimento = new("REMOVE_VANTAGEM", []);
        DocumentoExigidoDoModelo comCodigosDecompostos = Documento($" TIPO{CComCedilhaDecomposto}") with { FaseCodigo = $"FASE{CComCedilhaDecomposto}" };
        ConteudoDoModelo conteudo = Vazio with
        {
            ConjuntoDeDocumentos = ConjuntoComGrupos(GrupoDeDocumentos(2, Documento("RG"), comCodigosDecompostos) with { Indeferimento = indeferimento }),
        };

        GrupoDeDocumentosDoModelo grupo = conteudo.NaFormaGravada().Documentos.GruposDeDocumentos.Should().ContainSingle().Subject;

        grupo.QuantidadeMinima.Should().Be(2);
        grupo.Indeferimento.Should().Be(indeferimento);
        grupo.Documentos.Select(static d => d.TipoDocumentoCodigo).Should().Equal("RG", $" TIPO{CComCedilhaDecomposto}");
        grupo.Documentos[1].FaseCodigo.Should().Be($"FASE{CComCedilhaDecomposto}");
    }

    [Fact(DisplayName = "A parte ausente do conteúdo fica como vazia, inclusive o rótulo do campo")]
    public void NaFormaGravada_PartesAusentesViramVazias()
    {
        ItemDoModelo semRotulo = Item("RENDA").ComRotulo(null!);

        ConteudoDoModelo gravado = new ConteudoDoModelo(null, null!, [semRotulo], null!, null!, null!).NaFormaGravada();

        gravado.Paginas.Should().BeEmpty();
        gravado.Termos.Should().BeEmpty();
        gravado.Pressupostos.Should().BeEmpty();
        gravado.Grupos.Should().BeEmpty();
        gravado.Documentos.DocumentosExigidos.Should().BeEmpty();
        gravado.Documentos.GruposDeDocumentos.Should().BeEmpty();
        gravado.Itens.Single().Should().Match<ItemDoModelo>(static i => i.Apresentacao.Rotulo.Length == 0);
    }

    [Fact(DisplayName = "As posições do conteúdo e das restrições são mantidas, para a recusa apontar o índice que veio")]
    public void NaFormaGravada_MantemAsPosicoes()
    {
        ItemDoModelo campo = Item("RENDA").ComRestricoes([new TamanhoTexto(1, 5), new FaixaNumerica(0, 1)]);
        ConteudoDoModelo conteudo = Vazio with { Itens = [Item("B", 1), Item("A", 0), campo] };

        ConteudoDoModelo gravado = conteudo.NaFormaGravada();

        gravado.Itens.Select(static i => i.FatoCodigo).Should().Equal("B", "A", "RENDA");
        gravado.Itens[2].Regras.Restricoes.Select(static r => r.Tipo).Should().Equal(conteudo.Itens[2].Regras.Restricoes.Select(static r => r.Tipo));
    }

    [Fact(DisplayName = "O código que não se normaliza fica como veio, sem exceção")]
    public void NaFormaGravada_CodigoComSurrogateSemPar_FicaComoVeio()
    {
        ConteudoDoModelo gravado = (Vazio with { Itens = [Item(" CERTIFICADO\uD800 ")] }).NaFormaGravada();

        gravado.Itens.Single().FatoCodigo.Should().Be(" CERTIFICADO\uD800 ");
    }

    [Fact(DisplayName = "Paginas, itens e termos ficam na ordem declarada")]
    public void EmOrdem_PaginasItensETermos_OrdenaPelaOrdemDeclarada()
    {
        ConteudoDoModelo conteudo = Vazio with
        {
            Paginas = [Pagina("B", 1), Pagina("A", 0)],
            Itens = [Item("SEGUNDO", 1), Item("PRIMEIRO", 0)],
            Termos = [Termo("T2", 1), Termo("T1", 0)],
        };

        ConteudoDoModelo ordenado = conteudo.EmOrdem();

        ordenado.Paginas.Select(static e => e.Codigo).Should().Equal("A", "B");
        ordenado.Itens.Select(static i => i.FatoCodigo).Should().Equal("PRIMEIRO", "SEGUNDO");
        ordenado.Termos.Select(static t => t.Codigo).Should().Equal("T1", "T2");
    }

    [Fact(DisplayName = "Os grupos e os campos de cada grupo ficam na ordem declarada")]
    public void EmOrdem_GruposECamposDeGrupo_OrdenaPelaOrdemDeclarada()
    {
        ConteudoDoModelo conteudo = Vazio with { Grupos = [Grupo("G2", 1, null, Item("C2", 1), Item("C1", 0)), Grupo("G1", 0)] };

        ConteudoDoModelo ordenado = conteudo.EmOrdem();

        ordenado.Grupos.Select(static g => g.Codigo).Should().Equal("G1", "G2");
        ordenado.Grupos[1].Subitens.Select(static s => s.FatoCodigo).Should().Equal("C1", "C2");
    }

    [Fact(DisplayName = "As restrições de cada campo, de item e de grupo, ficam na ordem do tipo")]
    public void EmOrdem_RestricoesNaOrdemDoTipo()
    {
        IReadOnlyList<RestricaoValor> desordenadas = [new OpcoesPermitidas([new OpcoesCondicionadas(null, ["A"])]), new TamanhoTexto(1, 5), new FaixaNumerica(0, 1)];
        ItemDoModelo campo = Item("RENDA").ComRestricoes(desordenadas);

        ConteudoDoModelo ordenado = (Vazio with { Itens = [campo], Grupos = [Grupo("G", 0, null, campo)] }).EmOrdem();

        ordenado.Itens.Single().Regras.Restricoes.Select(static r => r.Tipo).Should().BeInAscendingOrder();
        ordenado.Grupos.Single().Subitens.Single().Regras.Restricoes.Select(static r => r.Tipo).Should().BeInAscendingOrder();
    }

    [Fact(DisplayName = "Os documentos exigidos ausentes ficam como lista vazia, e os informados são mantidos")]
    public void EmOrdem_Documentos()
    {
        (Vazio with { ConjuntoDeDocumentos = null }).EmOrdem().Documentos.DocumentosExigidos.Should().BeEmpty();
        (Vazio with { ConjuntoDeDocumentos = Conjunto(Documento("LAUDO")) }).EmOrdem().Documentos.DocumentosExigidos.Should().ContainSingle();
    }

    [Fact(DisplayName = "Ordenar devolve um conteúdo novo e não muda o original")]
    public void EmOrdem_NaoMudaOOriginal()
    {
        ConteudoDoModelo conteudo = Vazio with { Itens = [Item("SEGUNDO", 1), Item("PRIMEIRO", 0)] };

        conteudo.EmOrdem().Itens.Select(static i => i.FatoCodigo).Should().Equal("PRIMEIRO", "SEGUNDO");
        conteudo.Itens.Select(static i => i.FatoCodigo).Should().Equal("SEGUNDO", "PRIMEIRO");
    }
}
