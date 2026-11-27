namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;
using AwesomeAssertions.Equivalency;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.ApoioDoConteudoLido;

using Condicoes = System.Collections.Generic.IReadOnlyList<System.Collections.Generic.IReadOnlyList<Unifesspa.UniPlus.Regras.Entradas.CondicaoPrecondicaoInput>>;
using Recusa = (string Campo, string Codigo, string Mensagem);


/// <summary>
/// A leitura do conteúdo do modelo pela forma das regras: acumula as recusas na ordem das partes
/// (páginas, itens, termos, grupos, documentos), confere a repetição de termos sobre o pedido inteiro e guarda a posição de cada parte lida na entrada.
/// </summary>
public sealed class ConteudoLidoTests
{
    [Fact(DisplayName = "A entrada que recusa em toda parte devolve as recusas na ordem da leitura, com a posição de cada uma na entrada")]
    public void Ler_RecusaEmTodasAsPartes_ListaAsRecusasNaOrdemDaLeitura()
    {
        ConteudoDoModeloInput entrada = new(
            "Habilitação", PaginasRecusadas(), ItensRecusados(), TermosRecusados(), ["CERTIFICADO", ElementoNulo<string>()], GruposRecusados(),
            ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(DocumentosRecusados()));

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(entrada);

        leitura.Recusas.Select(static e => (e.Field!, e.Error.Code, e.Error.Message)).Should().Equal(RecusasEsperadas());
        lido.ParaConteudo().Should().BeEquivalentTo(new ConteudoDoModelo("Habilitação", [], [], [], ["CERTIFICADO", string.Empty], [], ConjuntoDeDocumentosDoModelo.Nenhum), PorMembros);
    }

    [Fact(DisplayName = "A entrada válida em toda parte vira o conteúdo do modelo, sem recusa, com o formato do fato vindo do catálogo")]
    public void Ler_EntradaValidaEmTodasAsPartes_ProduzOConteudoEsperado()
    {
        ConteudoDoModeloInput entrada = new(
            "Habilitação", [PaginaValida()], [ItemValido()], [TermoValido()], ["CERTIFICADO"], [GrupoValido()], ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(DocumentoValido()));

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(entrada);

        leitura.Recusas.Should().BeEmpty();
        lido.ParaConteudo().Should().BeEquivalentTo(
            new ConteudoDoModelo("Habilitação", [PaginaEsperada()], [ItemEsperado()], [TermoEsperado()], ["CERTIFICADO"], [GrupoEsperado()], new ConjuntoDeDocumentosDoModelo([DocumentoEsperado()], [])),
            PorMembros);
    }

    [Fact(DisplayName = "A pagina com tipo e bloco que não são códigos do contrato é recusada em cada campo, depois da exibição, e não é montada")]
    public void Ler_PaginaComTipoEBlocoDesconhecidos_RecusaCadaCampoSemMontarAPagina()
    {
        PaginaDoFormularioInput pagina = PaginaValida() with { Exibicao = PredicadoSemCondicao, Tipo = "secao", Bloco = "RODAPE" };

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, [pagina], null, null, null));

        leitura.Recusas.Select(static e => (e.Field!, e.Error.Code)).Should().Equal(
            ("paginas[0].exibicao[0]", "ClausulaDnf.ClausulaVazia"),
            ("paginas[0].tipo", EstruturaFormularioErrorCodes.TipoDePaginaObrigatorio),
            ("paginas[0].bloco", EstruturaFormularioErrorCodes.BlocoNaoAdmitido));
        lido.Paginas.Should().BeEmpty();
    }

    [Fact(DisplayName = "As condições inválidas da exibição e do impedimento apontam os respectivos campos")]
    public void Ler_CondicoesInvalidas_RecusaNoCampoDeCadaRegra()
    {
        Condicoes condicoes = [[new CondicaoPrecondicaoInput(string.Empty, "IGUAL", JsonSerializer.SerializeToElement(true))]];
        FatoColetadoInput item = Item("CERTIFICADO", 0).ExibidoQuando(condicoes)
            .ComImpedimento(new ImpedimentoInput(condicoes, null)).Construir();
        LeituraDoConteudo leitura = new(Formatos);

        leitura.Ler(new ConteudoDoModeloInput(null, null, [item], null, null));

        leitura.Recusas.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("itens[0].regras.exibicao[0][0].fato", "CondicaoDnf.FatoObrigatorio"),
            ("itens[0].regras.impedimento.quando[0][0].fato", "CondicaoDnf.FatoObrigatorio"));
    }

    [Fact(DisplayName = "O bloco ausente é a pagina sem bloco, e o código do bloco lido vai para a pagina")]
    public void Ler_BlocoAusenteOuDeclarado_VaiParaAPagina()
    {
        PaginaDoFormularioInput comBloco = EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").Construir();

        LeituraDoConteudo leitura = new(Formatos);
        ConteudoLido lido = leitura.Ler(new ConteudoDoModeloInput(null, [PaginaValida(), comBloco], null, null, null));

        leitura.Recusas.Should().BeEmpty();
        lido.Paginas.Select(static e => (e.Pagina.Tipo, e.Pagina.Bloco)).Should().Equal(
            (TipoPaginaFormulario.Secao, (BlocoSistema?)null), (TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite));
    }

    [Fact(DisplayName = "A parte lida guarda a posição dela na entrada; a nula e a recusada ficam de fora")]
    public void Ler_PartesLidasEEntreRecusadas_GuardamAPosicaoNaEntrada()
    {
        ConteudoLido lido = new LeituraDoConteudo(Formatos).Ler(ComPartesRecusadasNoMeio());

        lido.Paginas.Select(static e => e.Indice).Should().Equal(0, 3);
        lido.Itens.Select(static i => i.Indice).Should().Equal(0, 2);
        lido.Termos.Select(static t => t.Indice).Should().Equal(1);
        lido.Grupos.Lidos.Select(static g => g.Indice).Should().Equal(0, 2);
        lido.Documentos.DocumentosExigidos.Select(static d => d.Indice).Should().Equal(0, 3);
    }

    [Fact(DisplayName = "A entrada ausente ou com as listas ausentes é lida como conteúdo vazio, sem recusa")]
    public void Ler_EntradaOuListasAusentes_ConteudoVazioSemRecusa()
    {
        LeituraDoConteudo leituraSemEntrada = new(Formatos);
        LeituraDoConteudo leituraSemListas = new(Formatos);
        ConteudoLido semEntrada = leituraSemEntrada.Ler(null);
        ConteudoLido semListas = leituraSemListas.Ler(new ConteudoDoModeloInput(null, null, null, null, null));

        leituraSemEntrada.Recusas.Should().BeEmpty();
        leituraSemListas.Recusas.Should().BeEmpty();
        semEntrada.ParaConteudo().Should().BeEquivalentTo(new ConteudoDoModelo(null, [], [], [], [], [], ConjuntoDeDocumentosDoModelo.Nenhum), PorMembros);
        semListas.ParaConteudo().Should().BeEquivalentTo(new ConteudoDoModelo(null, [], [], [], [], [], ConjuntoDeDocumentosDoModelo.Nenhum), PorMembros);
    }
}
