namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ConteudosDoModeloDeTeste;

/// <summary>O conteúdo do modelo na forma em que é conferido e gravado, e na ordem em que é gravado.</summary>
public sealed class ConteudoDoModeloFormaGravadaTests
{
    private const string CComCedilhaDecomposto = "Ç";
    private const string CComCedilha = "Ç";

    [Fact(DisplayName = "O título é aparado, e o título em branco fica ausente")]
    public void NaFormaGravada_AparaOTitulo()
    {
        (Vazio with { Titulo = "  Habilitação  " }).NaFormaGravada().Titulo.Should().Be("Habilitação");
        (Vazio with { Titulo = "   " }).NaFormaGravada().Titulo.Should().BeNull();
    }

    [Fact(DisplayName = "Os códigos de etapa, item, grupo e campo de grupo ficam aparados e em NFC")]
    public void NaFormaGravada_CodigosDeEstruturaAparadosEmNfc()
    {
        ConteudoDoModelo conteudo = Vazio with
        {
            Etapas = [Etapa($" ETAPA{CComCedilhaDecomposto} ")],
            Itens = [Item($" ITEM{CComCedilhaDecomposto}")],
            Grupos = [Grupo($"GRUPO{CComCedilhaDecomposto}", 0, null, Item($"CAMPO{CComCedilhaDecomposto}"))],
        };

        ConteudoDoModelo gravado = conteudo.NaFormaGravada();

        gravado.Etapas.Single().Codigo.Should().Be($"ETAPA{CComCedilha}");
        gravado.Itens.Single().FatoCodigo.Should().Be($"ITEM{CComCedilha}");
        gravado.Grupos.Single().Codigo.Should().Be($"GRUPO{CComCedilha}");
        gravado.Grupos.Single().Subitens.Single().FatoCodigo.Should().Be($"CAMPO{CComCedilha}");
    }

    [Fact(DisplayName = "Os códigos de termo, pressuposto e documento ficam aparados e em NFC")]
    public void NaFormaGravada_CodigosDeTermoPressupostoEDocumentoAparadosEmNfc()
    {
        ConteudoDoModelo conteudo = Vazio with
        {
            Termos = [Termo($"TERMO{CComCedilhaDecomposto} ")],
            Pressupostos = [$" PRESSUPOSTO{CComCedilhaDecomposto}"],
            DocumentosExigidos = [Documento($" TIPO{CComCedilhaDecomposto}") with { FaseCodigo = $"FASE{CComCedilhaDecomposto}" }],
        };

        ConteudoDoModelo gravado = conteudo.NaFormaGravada();

        gravado.Termos.Single().Codigo.Should().Be($"TERMO{CComCedilha}");
        gravado.Pressupostos.Should().Equal($"PRESSUPOSTO{CComCedilha}");
        gravado.Documentos.Single().TipoDocumentoCodigo.Should().Be($"TIPO{CComCedilha}");
        gravado.Documentos.Single().FaseCodigo.Should().Be($"FASE{CComCedilha}");
    }

    [Fact(DisplayName = "O fato e os valores da exigência por opção do documento ficam aparados e em NFC")]
    public void NaFormaGravada_ExigenciaPorOpcaoAparadaEmNfc()
    {
        ConteudoDoModelo conteudo = Vazio with
        {
            DocumentosExigidos = [Documento("LAUDO", porOpcao: new($" MODALIDADE{CComCedilhaDecomposto}", [$" AC{CComCedilhaDecomposto} "]))],
        };

        ExigenciaPorOpcao porOpcao = conteudo.NaFormaGravada().Documentos.Single().PorOpcao!;

        porOpcao.FatoCodigo.Should().Be($"MODALIDADE{CComCedilha}");
        porOpcao.Exceto.Should().Equal($"AC{CComCedilha}");
    }

    [Fact(DisplayName = "Rótulo, título de etapa e texto opcional ficam aparados, e o texto opcional em branco fica ausente")]
    public void NaFormaGravada_AparaOsTextos()
    {
        ItemDoModelo campo = (Item("RENDA").ComRotulo("  Renda  ").ComAjuda("  Informe  ")) with { Formato = "   " };

        ConteudoDoModelo gravado = (Vazio with { Etapas = [Etapa() with { Titulo = " Dados ", Aviso = "  " }], Itens = [campo] }).NaFormaGravada();

        gravado.Etapas.Single().Titulo.Should().Be("Dados");
        gravado.Etapas.Single().Aviso.Should().BeNull();
        gravado.Itens.Single().Apresentacao.Rotulo.Should().Be("Renda");
        gravado.Itens.Single().Apresentacao.Ajuda.Should().Be("Informe");
        gravado.Itens.Single().Formato.Should().BeNull();
    }

    [Fact(DisplayName = "A parte ausente do conteúdo fica como vazia, inclusive o rótulo do campo")]
    public void NaFormaGravada_PartesAusentesViramVazias()
    {
        ItemDoModelo semRotulo = Item("RENDA").ComRotulo(null!);

        ConteudoDoModelo gravado = new ConteudoDoModelo(null, null!, [semRotulo], null!, null!, null!).NaFormaGravada();

        gravado.Etapas.Should().BeEmpty();
        gravado.Termos.Should().BeEmpty();
        gravado.Pressupostos.Should().BeEmpty();
        gravado.Grupos.Should().BeEmpty();
        gravado.Documentos.Should().BeEmpty();
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

    [Fact(DisplayName = "O código que não se normaliza fica como veio, só aparado, sem exceção")]
    public void NaFormaGravada_CodigoComSurrogateSemPar_FicaComoVeio()
    {
        ConteudoDoModelo gravado = (Vazio with { Itens = [Item(" CERTIFICADO\uD800 ")] }).NaFormaGravada();

        gravado.Itens.Single().FatoCodigo.Should().Be("CERTIFICADO\uD800");
    }

    [Fact(DisplayName = "Etapas, itens e termos ficam na ordem declarada")]
    public void EmOrdem_EtapasItensETermos_OrdenaPelaOrdemDeclarada()
    {
        ConteudoDoModelo conteudo = Vazio with
        {
            Etapas = [Etapa("B", 1), Etapa("A", 0)],
            Itens = [Item("SEGUNDO", 1), Item("PRIMEIRO", 0)],
            Termos = [Termo("T2", 1), Termo("T1", 0)],
        };

        ConteudoDoModelo ordenado = conteudo.EmOrdem();

        ordenado.Etapas.Select(static e => e.Codigo).Should().Equal("A", "B");
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
        (Vazio with { DocumentosExigidos = null }).EmOrdem().DocumentosExigidos.Should().BeEmpty();
        (Vazio with { DocumentosExigidos = [Documento("LAUDO")] }).EmOrdem().Documentos.Should().ContainSingle();
    }

    [Fact(DisplayName = "Ordenar devolve um conteúdo novo e não muda o original")]
    public void EmOrdem_NaoMudaOOriginal()
    {
        ConteudoDoModelo conteudo = Vazio with { Itens = [Item("SEGUNDO", 1), Item("PRIMEIRO", 0)] };

        conteudo.EmOrdem().Itens.Select(static i => i.FatoCodigo).Should().Equal("PRIMEIRO", "SEGUNDO");
        conteudo.Itens.Select(static i => i.FatoCodigo).Should().Equal("SEGUNDO", "PRIMEIRO");
    }
}
