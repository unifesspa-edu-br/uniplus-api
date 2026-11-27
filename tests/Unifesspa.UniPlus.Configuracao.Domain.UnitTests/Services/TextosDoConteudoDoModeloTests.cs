namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ConteudosDoModeloDeTeste;

/// <summary>
/// Os textos de cada parte do conteúdo do modelo, com o campo de cada um no caminho dado: servem à parte lida
/// e à parte que a leitura recusou, com o que conseguiu ler.
/// </summary>
public sealed class TextosDoConteudoDoModeloTests
{
    private static readonly JsonElement ValorEmTexto = JsonSerializer.SerializeToElement("VALOR");

    private static PredicadoDnf Condicao(string fato = "FATO") => Quando(fato, Operador.Igual, ValorEmTexto);

    private static RegrasLidasDoCampo SemRegras => new(null, Obrigatoriedade.Sempre, [], null);

    private static RegrasLidasDoCampo ComTodasAsRegras => new(
        Condicao("ANTERIOR"),
        Obrigatoriedade.Quando(Condicao("OBRIGA")),
        [new MunicipiosDaUf("UF_RESPONDIDA")],
        new Impedimento(Condicao("RENDA"), "Mensagem"));

    private static DocumentoExigidoDoModelo DocumentoCompleto => Documento(
        "CERTIDAO", Condicao("RENDA"), new ExigenciaPorOpcao("OPCAO_FATO", ["EXCETO"]), RequisitosAPartirDasFases("FASE_PRAZO", "FASE_VALIDADE"))
        with
    {
        Indeferimento = new IndeferimentoDoDocumentoDoModelo(true, "CONSEQUENCIA", [new BaseLegalDoModelo("REF", "ABRANGENCIA", "SITUACAO", null)]),
    };

    private static IEnumerable<(string Campo, string? Texto)> TextosDoDocumento(DocumentoExigidoDoModelo documento) =>
        TextosDoConteudoDoModelo.DoDocumentoLido("d[0]", documento);

    private static string[] Campos(IEnumerable<(string Campo, string? Texto)> textos) => [.. textos.Select(static t => t.Campo)];

    private static string?[] Valores(IEnumerable<(string Campo, string? Texto)> textos) => [.. textos.Select(static t => t.Texto)];

    [Fact(DisplayName = "A página tem título, descrição, aviso e os textos da condição de exibição, cada um no seu campo")]
    public void DaPagina_TemCadaTextoNoSeuCampo()
    {
        IEnumerable<(string Campo, string? Texto)> textos = TextosDoConteudoDoModelo.DaPagina("paginas[2]", "Título", "Descrição", null, Condicao("RENDA"));

        Campos(textos).Should().Equal(
            "paginas[2].apresentacao.titulo", "paginas[2].apresentacao.descricao", "paginas[2].apresentacao.aviso",
            "paginas[2].exibicao", "paginas[2].exibicao");
        Valores(textos).Should().Equal("Título", "Descrição", null, "RENDA", "VALOR");
    }

    [Fact(DisplayName = "A página sem condição de exibição só tem os textos da apresentação")]
    public void DaPagina_SemExibicao_SoApresentacao() =>
        TextosDoConteudoDoModelo.DaPagina("paginas[0]", "T", null, null, null).Should().HaveCount(3);

    [Fact(DisplayName = "O campo tem os códigos, a apresentação, o formato e os textos das regras, na ordem do caminho")]
    public void DoCampo_ComTodasAsRegras_TemCadaTextoNoSeuCampo()
    {
        IEnumerable<(string Campo, string? Texto)> textos =
            TextosDoConteudoDoModelo.DoCampo("itens[1]", "RENDA", "DADOS", "Rótulo", "Ajuda", "FORMATO", ComTodasAsRegras);

        Campos(textos).Should().Equal(
            "itens[1].fatoCodigo", "itens[1].paginaCodigo", "itens[1].apresentacao.rotulo", "itens[1].formato", "itens[1].apresentacao.ajuda",
            "itens[1].regras.exibicao", "itens[1].regras.exibicao",
            "itens[1].regras.obrigatoriedade.predicado", "itens[1].regras.obrigatoriedade.predicado",
            "itens[1].regras.restricoes[0].fatos",
            "itens[1].regras.impedimento.quando", "itens[1].regras.impedimento.quando", "itens[1].regras.impedimento.mensagem");
        Valores(textos).Should().Equal(
            "RENDA", "DADOS", "Rótulo", "FORMATO", "Ajuda", "ANTERIOR", "VALOR", "OBRIGA", "VALOR", "UF_RESPONDIDA", "RENDA", "VALOR", "Mensagem");
    }

    [Fact(DisplayName = "No campo, a regra recusada na leitura chega nula e não tem texto, e a posição da restrição lida segue a do pedido")]
    public void DoCampo_ComRegrasRecusadas_SoTemOsTextosLidos()
    {
        RegrasLidasDoCampo regras = new(null, null, [null, new OpcoesDasRespostas(["FATO_DA_OPCAO"])], null);

        IEnumerable<(string Campo, string? Texto)> textos = TextosDoConteudoDoModelo.DoCampo("itens[0]", null, null, null, null, null, regras);

        Campos(textos).Should().Equal(
            "itens[0].fatoCodigo", "itens[0].paginaCodigo", "itens[0].apresentacao.rotulo", "itens[0].formato", "itens[0].apresentacao.ajuda",
            "itens[0].regras.restricoes[1].fatos");
    }

    [Fact(DisplayName = "As opções permitidas têm os valores e a condição de cada entrada")]
    public void DoCampo_OpcoesPermitidas_TemValoresECondicaoDaEntrada()
    {
        RegrasLidasDoCampo regras = SemRegras with
        {
            Restricoes = [new OpcoesPermitidas([new OpcoesCondicionadas(Condicao("CONDICAO"), ["A", "B"])])],
        };

        IEnumerable<(string Campo, string? Texto)> textos = TextosDoConteudoDoModelo.DoCampo("grupos[0].subitens[1]", "F", "P", "R", null, null, regras)
            .Skip(5);

        Campos(textos).Should().Equal(
            "grupos[0].subitens[1].regras.restricoes[0].entradas[0].valores", "grupos[0].subitens[1].regras.restricoes[0].entradas[0].valores",
            "grupos[0].subitens[1].regras.restricoes[0].entradas[0].quando", "grupos[0].subitens[1].regras.restricoes[0].entradas[0].quando");
        Valores(textos).Should().Equal("A", "B", "CONDICAO", "VALOR");
    }

    [Fact(DisplayName = "O valor da condição em lista entra com cada texto dele")]
    public void DoCampo_ValorEmLista_TemCadaTexto()
    {
        JsonElement valor = JsonSerializer.SerializeToElement(new[] { "UM", "DOIS" });
        RegrasLidasDoCampo regras = SemRegras with { Exibicao = Quando("FATO", Operador.AlgumDe, valor) };

        Valores(TextosDoConteudoDoModelo.DoCampo("itens[0]", "F", "P", "R", null, null, regras).Skip(5)).Should().Equal("FATO", "UM", "DOIS");
    }

    [Fact(DisplayName = "O grupo tem a página, o rótulo e os textos da exibição e da obrigatoriedade, no caminho das regras do grupo")]
    public void DoGrupo_TemCadaTextoNoSeuCampo()
    {
        IEnumerable<(string Campo, string? Texto)> textos = TextosDoConteudoDoModelo.DoGrupo("grupos[1]", "DADOS", "Família", Condicao("A"), Condicao("B"));

        Campos(textos).Should().Equal(
            "grupos[1].paginaCodigo", "grupos[1].rotulo",
            "grupos[1].regras.exibicao", "grupos[1].regras.exibicao",
            "grupos[1].regras.obrigatoriedade.predicado", "grupos[1].regras.obrigatoriedade.predicado");
        Valores(textos).Take(4).Should().Equal("DADOS", "Família", "A", "VALOR");
    }

    [Fact(DisplayName = "O grupo sem regras só tem a página e o rótulo")]
    public void DoGrupo_SemRegras_SoPaginaERotulo() =>
        Campos(TextosDoConteudoDoModelo.DoGrupo("grupos[0]", "DADOS", "Família", null, null)).Should().Equal("grupos[0].paginaCodigo", "grupos[0].rotulo");

    [Fact(DisplayName = "O termo tem os textos da exibição e da obrigatoriedade")]
    public void DoTermo_TemExibicaoEObrigatoriedade()
    {
        IEnumerable<(string Campo, string? Texto)> textos = TextosDoConteudoDoModelo.DoTermo("termos[3]", Condicao("A"), Condicao("B"));

        Campos(textos).Should().Equal(
            "termos[3].regras.exibicao", "termos[3].regras.exibicao",
            "termos[3].regras.obrigatoriedade.predicado", "termos[3].regras.obrigatoriedade.predicado");
        Valores(textos).Should().Equal("A", "VALOR", "B", "VALOR");
    }

    [Fact(DisplayName = "O termo sem regras não tem texto")]
    public void DoTermo_SemRegras_SemTexto() => TextosDoConteudoDoModelo.DoTermo("termos[0]", null, null).Should().BeEmpty();

    [Fact(DisplayName = "O documento tem os códigos, a consequência, a condição, as referências, as opções e as bases legais")]
    public void DoDocumento_ComTudo_TemCadaTextoNoSeuCampo()
    {
        IEnumerable<(string Campo, string? Texto)> textos = TextosDoDocumento(DocumentoCompleto);

        Campos(textos).Should().Equal(
            "d[0].faseCodigo", "d[0].tipoDocumentoCodigo",
            "d[0].quando", "d[0].quando",
            "d[0].requisitos.prazoDeEmissao.referencia.faseCodigo", "d[0].requisitos.validade.referencia.faseCodigo",
            "d[0].porOpcao.fatoCodigo", "d[0].porOpcao.exceto[0]",
            "d[0].indeferimento.consequenciaDoDescumprimento",
            "d[0].indeferimento.basesLegais[0].referencia", "d[0].indeferimento.basesLegais[0].abrangencia",
            "d[0].indeferimento.basesLegais[0].situacao", "d[0].indeferimento.basesLegais[0].observacao");
        Valores(textos).Should().Equal(
            "INSCRICAO", "CERTIDAO", "RENDA", "VALOR", "FASE_PRAZO", "FASE_VALIDADE", "OPCAO_FATO", "EXCETO", "CONSEQUENCIA",
            "REF", "ABRANGENCIA", "SITUACAO", null);
    }

    [Fact(DisplayName = "O documento sem condição, sem opção e sem bases legais só tem os textos fixos")]
    public void DoDocumento_Simples_SoTemOsTextosFixos()
    {
        DocumentoExigidoDoModelo documento = Documento("CERTIDAO");

        Campos(TextosDoConteudoDoModelo.DoDocumento(
            "d[0]", documento.FaseCodigo, documento.TipoDocumentoCodigo, null, documento.Requisitos.TextosDasReferencias("d[0]"), [],
            TextosDoConteudoDoModelo.DoIndeferimento("d[0]", documento.Indeferimento.ConsequenciaDoDescumprimento, documento.Indeferimento.BasesLegais)))
            .Should().Equal(
                "d[0].faseCodigo", "d[0].tipoDocumentoCodigo",
                "d[0].requisitos.prazoDeEmissao.referencia.faseCodigo", "d[0].requisitos.validade.referencia.faseCodigo",
                "d[0].indeferimento.consequenciaDoDescumprimento");
    }

    [Fact(DisplayName = "O indeferimento tem a consequência e as bases legais, cada uma na posição que veio")]
    public void DoIndeferimento_ComBases_TemConsequenciaEBasesNasPosicoes()
    {
        BaseLegalDoModelo?[] bases = [new BaseLegalDoModelo("REF", "A", "S", "OBS")];

        IEnumerable<(string Campo, string? Texto)> textos = TextosDoConteudoDoModelo.DoIndeferimento("g[0]", "CONSEQUENCIA", bases);

        Campos(textos).Should().Equal(
            "g[0].indeferimento.consequenciaDoDescumprimento",
            "g[0].indeferimento.basesLegais[0].referencia", "g[0].indeferimento.basesLegais[0].abrangencia",
            "g[0].indeferimento.basesLegais[0].situacao", "g[0].indeferimento.basesLegais[0].observacao");
        Valores(textos).Should().Equal("CONSEQUENCIA", "REF", "A", "S", "OBS");
    }

    [Fact(DisplayName = "A base legal nula mantém a posição das demais e não tem texto")]
    public void DoIndeferimento_BaseNula_MantemAPosicaoDasDemais()
    {
        BaseLegalDoModelo?[] bases = [null, new BaseLegalDoModelo("REF", "A", "S", null)];

        Campos(TextosDoConteudoDoModelo.DoIndeferimento("g[0]", null, bases)).Should().Equal(
            "g[0].indeferimento.consequenciaDoDescumprimento",
            "g[0].indeferimento.basesLegais[1].referencia", "g[0].indeferimento.basesLegais[1].abrangencia",
            "g[0].indeferimento.basesLegais[1].situacao", "g[0].indeferimento.basesLegais[1].observacao");
    }

    [Fact(DisplayName = "A exigência por opção tem o fato e cada opção excetuada na posição que veio, inclusive depois de uma nula")]
    public void DaExigenciaPorOpcao_OpcaoNula_MantemAPosicaoDasDemais()
    {
        IEnumerable<(string Campo, string? Texto)> textos = TextosDoConteudoDoModelo.DaExigenciaPorOpcao("d[0]", "FATO", [null, "OPCAO"]);

        Campos(textos).Should().Equal("d[0].porOpcao.fatoCodigo", "d[0].porOpcao.exceto[0]", "d[0].porOpcao.exceto[1]");
        Valores(textos).Should().Equal("FATO", null, "OPCAO");
    }
}
