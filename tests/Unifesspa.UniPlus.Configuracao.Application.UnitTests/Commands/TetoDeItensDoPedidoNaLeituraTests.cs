namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.RecusasParaTeste;

/// <summary>
/// O teto de itens conta o pedido inteiro, inclusive o que a leitura recusa: o excesso é recusado uma vez, na lista, e
/// os itens e campos que a leitura aceita seguem pelas demais regras.
/// </summary>
public sealed class TetoDeItensDoPedidoNaLeituraTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    [Fact(DisplayName = "O pedido com 201 itens e um deles recusado na leitura devolve a recusa do excesso e a do item")]
    public void Ler_DuzentosEUmItensComUmRecusado_RecusaOExcessoEOItem()
    {
        FatoColetadoInput[] itens = [.. Itens(201)];
        itens[7] = ItemNaoLido("CERTIFICADO", 7);

        (string Campo, string Codigo)[] recusas = DaLeituraComOConteudo(new ConteudoDoModeloInput(null, null, itens, null, null), out ConteudoLido lido);

        recusas.Should().Equal(
            ("itens", ItemFormularioErrorCodes.ItensEmExcesso),
            ("itens[7].regras.obrigatoriedade", "ModeloFormulario.ObrigatoriedadeInvalida"));
        lido.Itens.Should().HaveCount(200);
        lido.PorInteiro.Should().BeFalse();
    }

    [Fact(DisplayName = "O pedido com 200 itens está no teto e é lido por inteiro")]
    public void Ler_DuzentosItens_NaoRecusaOTeto()
    {
        (string Campo, string Codigo)[] recusas = DaLeituraComOConteudo(new ConteudoDoModeloInput(null, null, [.. Itens(200)], null, null), out ConteudoLido lido);

        recusas.Should().BeEmpty();
        lido.Itens.Should().HaveCount(200);
        lido.PorInteiro.Should().BeTrue();
    }

    [Fact(DisplayName = "Os grupos e os campos deles entram na conta: 100 itens e dois grupos de 50 campos passam do teto, e todos são lidos")]
    public void Ler_GruposECamposDeGrupo_EntramNaContaDoTeto()
    {
        ConteudoDoModeloInput entrada = new(null, null, [.. Itens(100)], null, null, [Grupo("PRIMEIRO", 0, 50), Grupo("SEGUNDO", 1, 50)]);

        (string Campo, string Codigo)[] recusas = DaLeituraComOConteudo(entrada, out ConteudoLido lido);

        recusas.Should().Equal(("itens", ItemFormularioErrorCodes.ItensEmExcesso));
        lido.Grupos.Lidos.Should().HaveCount(2);
        lido.Itens.Should().HaveCount(100);
    }

    [Fact(DisplayName = "Um grupo com 99 campos e 100 itens somam 200 e ficam no teto")]
    public void Ler_GrupoQueFechaOTeto_NaoRecusa()
    {
        ConteudoDoModeloInput entrada = new(null, null, [.. Itens(100)], null, null, [Grupo("UNICO", 0, 99)]);

        DaLeitura(entrada).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com o teto estourado, a recusa da página ainda sai, antes da do excesso")]
    public void Ler_TetoEstouradoEPaginaRecusada_RecusaAsDuas()
    {
        PaginaDoFormularioInput pagina = EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir() with { Tipo = "secao" };

        DaLeitura(new ConteudoDoModeloInput(null, [pagina], [.. Itens(201)], null, null)).Should().Equal(
            ("paginas[0].tipo", EstruturaFormularioErrorCodes.TipoDePaginaObrigatorio),
            ("itens", ItemFormularioErrorCodes.ItensEmExcesso));
    }

    [Fact(DisplayName = "O pedido com 201 itens todos válidos devolve só a recusa do excesso, sem repeti-la pelo domínio")]
    public async Task Criar_DuzentosEUmItensValidos_RecusaOExcessoUmaVez()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo([.. Itens(201)]));

        Campos(resultado).Should().Equal(("conteudo.itens", ItemFormularioErrorCodes.ItensEmExcesso));
    }

    [Fact(DisplayName = "Com 201 itens e um item lido com ajuda acima de 1000 caracteres, saem o excesso e a ajuda na posição do item")]
    public async Task Criar_DuzentosEUmItensComAjudaLonga_RecusaOExcessoEAAjuda()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(ComItemNaPosicao(Montar("CERTIFICADO", 5).ComAjuda(new string('a', 1001)).Construir())));

        Campos(resultado).Should().Equal(
            ("conteudo.itens", ItemFormularioErrorCodes.ItensEmExcesso),
            ("conteudo.itens[5].apresentacao.ajuda", ItemFormularioErrorCodes.AjudaTamanho));
    }

    [Fact(DisplayName = "Com 201 itens e um item lido com caractere nulo no rótulo, saem o excesso e o texto não gravável")]
    public async Task Criar_DuzentosEUmItensComNuloNoRotulo_RecusaOExcessoEOTexto()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(ComItemNaPosicao(Montar("CERTIFICADO", 5).ComRotulo("Rótulo\u0000").Construir())));

        Campos(resultado).Should().Contain(
        [
            ("conteudo.itens", ItemFormularioErrorCodes.ItensEmExcesso),
            ("conteudo.itens[5].apresentacao.rotulo", ModeloFormularioErrorCodes.TextoNaoGravavel),
        ]);
    }

    [Fact(DisplayName = "Com 201 itens e um item lido com exibição que cita o próprio fato, saem o excesso e a autorreferência")]
    public async Task Criar_DuzentosEUmItensComExibicaoAutorreferente_RecusaOExcessoEAAutorreferencia()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(
            Conteudo(ComItemNaPosicao(Montar("CERTIFICADO", 5).ExibidoQuando(Quando("CERTIFICADO", true)).Construir())));

        Campos(resultado).Should().Equal(
            ("conteudo.itens", ItemFormularioErrorCodes.ItensEmExcesso),
            ("conteudo.itens[5].regras.exibicao", ItemFormularioErrorCodes.RegraAutorreferente));
    }

    [Fact(DisplayName = "Com 201 campos num grupo lido e um de fato desconhecido, saem o excesso e a recusa do catálogo na posição do campo")]
    public async Task Criar_GrupoLidoComDuzentosEUmCamposEFatoDesconhecido_RecusaOExcessoEOCatalogo()
    {
        FatoColetadoInput[] campos = [.. Enumerable.Range(0, 200).Select(static posicao => Campo("MAIOR_IDADE", posicao)), Campo("FATO_FORA_DO_CATALOGO", 200)];
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO", 1).ComRotulo("Composição").NaSecao("DADOS").ComOcorrencias(1, 10)
            .ComObrigatoriedade("SEMPRE").ComCampos(campos).Construir();

        Result<Guid> resultado = await _cenario.CriarAsync(Conteudo(Item("CERTIFICADO", 0)) with { Grupos = [grupo] });

        Campos(resultado).Should().Contain(
        [
            ("conteudo.itens", ItemFormularioErrorCodes.ItensEmExcesso),
            ("conteudo.grupos[0].subitens[200].fatoCodigo", ItemFormularioErrorCodes.FatoDesconhecido),
        ]);
    }

    [Fact(DisplayName = "Com 201 itens e um pressuposto igual ao fato de um item lido, saem o excesso e o pressuposto que também é item")]
    public async Task Criar_DuzentosEUmItensEPressupostoQueEItem_RecusaOExcessoEOPressuposto()
    {
        ConteudoDoModeloInput conteudo = Conteudo([.. Itens(201)]) with { Pressupostos = ["CERTIFICADO"] };

        Result<Guid> resultado = await _cenario.CriarAsync(conteudo);

        Campos(resultado).Select(static c => c.Codigo).Should().Contain([ItemFormularioErrorCodes.ItensEmExcesso, ModeloFormularioErrorCodes.PressupostoTambemItem]);
        Campos(resultado).Count(static c => c.Codigo == ItemFormularioErrorCodes.ItensEmExcesso).Should().Be(1);
    }

    private static FatoColetadoInput[] ComItemNaPosicao(FatoColetadoInput item)
    {
        FatoColetadoInput[] itens = [.. Itens(201)];
        itens[item.Ordem.GetValueOrDefault()] = item;
        return itens;
    }

    private static IEnumerable<FatoColetadoInput> Itens(int quantidade) =>
        Enumerable.Range(0, quantidade).Select(static ordem => Item("CERTIFICADO", ordem));

    private static GrupoColetadoInput Grupo(string codigo, int ordem, int campos) =>
        EntradaDoGrupoColetadoDeTeste.Compondo(codigo, ordem).ComRotulo("Composição").NaSecao("DADOS").ComOcorrencias(1, 10).ComObrigatoriedade("SEMPRE")
            .ComCampos([.. Enumerable.Range(0, campos).Select(static posicao => Campo("MAIOR_IDADE", posicao))]).Construir();
}
