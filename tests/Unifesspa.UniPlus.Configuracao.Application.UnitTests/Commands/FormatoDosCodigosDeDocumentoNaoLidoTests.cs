namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.RecusasParaTeste;

/// <summary>
/// O documento que a leitura recusa tem o formato dos códigos conferido mesmo assim, na posição que veio, e o código ausente,
/// já recusado na leitura, não é recusado de novo.
/// </summary>
public sealed class FormatoDosCodigosDeDocumentoNaoLidoTests
{
    private const string Documento = "conjuntoDeDocumentos.documentosExigidos[0]";
    private const string Grupo = "conjuntoDeDocumentos.gruposDeDocumentos[0]";

    private readonly CenarioDoHandlerDoModelo _cenario = new();

    [Fact(DisplayName = "O documento sem a obrigatoriedade informada e com o código de fase fora do formato recusa os dois")]
    public async Task Criar_DocumentoNaoLidoComFaseForaDoFormato_RecusaAsDuas()
    {
        DocumentoExigidoDoModeloInput documento = Documentos("inscricao-ficticia", "LAUDO_MEDICO") with { Indeferimento = new(null, null, [], false) };

        Result<Guid> resultado = await _cenario.CriarAsync(ComDocumentos([documento], []));

        Campos(resultado).Should().Equal(
            ($"conteudo.{Documento}.indeferimento.obrigatorio", ModeloFormularioErrorCodes.EntradaMalformada),
            ($"conteudo.{Documento}.faseCodigo", ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido));
        resultado.Errors.Select(static e => e.Error.Message).Should().OnlyContain(static m => !m.Contains("inscricao-ficticia", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O documento com a condição malformada e o tipo de documento fora do formato recusa os dois")]
    public void Ler_DocumentoComCondicaoMalformadaETipoForaDoFormato_RecusaOsDois()
    {
        DocumentoExigidoDoModeloInput documento = Documentos("INSCRICAO", "tipo-ficticio") with { Quando = [[]] };

        DaLeitura(ComDocumentos([documento], [])).Should().Equal(
            ($"{Documento}.quando[0]", "ClausulaDnf.ClausulaVazia"),
            ($"{Documento}.tipoDocumentoCodigo", ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido));
    }

    [Fact(DisplayName = "O código de fase vazio é recusado só pela leitura, sem recusa de formato")]
    public void Ler_DocumentoComFaseVazia_RecusaSoALeitura()
    {
        DaLeitura(ComDocumentos([Documentos(string.Empty, "LAUDO_MEDICO")], [])).Should().Equal(
            ($"{Documento}.faseCodigo", ModeloFormularioErrorCodes.EntradaMalformada));
    }

    [Fact(DisplayName = "A opção e o fato da exigência por opção fora do formato são recusados no documento que a leitura recusou")]
    public void Ler_DocumentoNaoLidoComExigenciaPorOpcaoForaDoFormato_RecusaOFatoEAOpcao()
    {
        DocumentoExigidoDoModeloInput documento = Documentos("INSCRICAO", "LAUDO_MEDICO") with
        {
            Quando = [[]],
            PorOpcao = new ExigenciaPorOpcaoInput("fato-ficticio", ["opcao-ficticia"]),
        };

        DaLeitura(ComDocumentos([documento], [])).Select(static r => r.Campo).Should().Equal(
            $"{Documento}.quando[0]", $"{Documento}.porOpcao.fatoCodigo", $"{Documento}.porOpcao.exceto[0]");
    }

    [Fact(DisplayName = "O documento lido de um grupo de documentos recusado tem o tipo fora do formato recusado junto com o grupo")]
    public void Ler_DocumentoLidoDeGrupoRecusado_RecusaOFormatoDoTipo()
    {
        GrupoDeDocumentosDoModeloInput grupo = new(1, new("DESCONHECIDA", [], false), [Documentos("INSCRICAO", "tipo-ficticio")]);

        DaLeitura(ComDocumentos([], [grupo])).Should().Equal(
            ($"{Grupo}.indeferimento.consequenciaDoDescumprimento", ModeloFormularioErrorCodes.ConsequenciaDoDescumprimentoDesconhecida),
            ($"{Grupo}.documentos[0].tipoDocumentoCodigo", ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido));
    }

    [Fact(DisplayName = "Os códigos de documento no formato aceito, em conteúdo sem recusa, não recebem recusa de formato")]
    public void Ler_DocumentoComCodigosNoFormato_NaoRecusa()
    {
        DaLeitura(ComDocumentos([Documentos("INSCRICAO", "LAUDO_MEDICO")], [])).Should().BeEmpty();
    }

    [Fact(DisplayName = "A escolha do prazo desconhecida e o código de fase da referência fora do formato recusam as duas")]
    public void Ler_EscolhaDoPrazoDesconhecidaEFaseDaReferenciaForaDoFormato_RecusaAsDuas()
    {
        PrazoDeEmissaoDoModeloInput prazo = new("DESCONHECIDA", 90, "DIAS", new DataDeReferenciaDoDocumentoDoModeloInput("INICIO_DA_FASE", null, "fase-ficticia"), null);
        DocumentoExigidoDoModeloInput documento = Documentos("INSCRICAO", "LAUDO_MEDICO") with { Requisitos = new RequisitosDoDocumentoDoModeloInput(prazo, null) };

        DaLeitura(ComDocumentos([documento], [])).Select(static r => r.Campo).Should().Equal(
            $"{Documento}.requisitos.prazoDeEmissao.escolha", $"{Documento}.requisitos.prazoDeEmissao.referencia.faseCodigo");
    }

    [Fact(DisplayName = "A escolha do prazo desconhecida e o código de fase da referência com caractere nulo recusam o texto e o formato")]
    public void Ler_EscolhaDoPrazoDesconhecidaEFaseDaReferenciaComNulo_RecusaOTextoEOFormato()
    {
        PrazoDeEmissaoDoModeloInput prazo = new("DESCONHECIDA", 90, "DIAS", new DataDeReferenciaDoDocumentoDoModeloInput("INICIO_DA_FASE", null, "FASE\u0000"), null);
        DocumentoExigidoDoModeloInput documento = Documentos("INSCRICAO", "LAUDO_MEDICO") with { Requisitos = new RequisitosDoDocumentoDoModeloInput(prazo, null) };

        DaLeitura(ComDocumentos([documento], [])).Where(static r => r.Campo.EndsWith("referencia.faseCodigo", StringComparison.Ordinal)).Select(static r => r.Codigo)
            .Should().Equal(ModeloFormularioErrorCodes.TextoNaoGravavel, ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido);
    }

    [Fact(DisplayName = "A escolha do prazo desconhecida e o código de fase da referência em branco recusam as duas, como no documento lido")]
    public void Ler_EscolhaDoPrazoDesconhecidaEFaseDaReferenciaEmBranco_RecusaAsDuas()
    {
        PrazoDeEmissaoDoModeloInput prazo = new("DESCONHECIDA", 90, "DIAS", new DataDeReferenciaDoDocumentoDoModeloInput("INICIO_DA_FASE", null, string.Empty), null);
        DocumentoExigidoDoModeloInput documento = Documentos("INSCRICAO", "LAUDO_MEDICO") with { Requisitos = new RequisitosDoDocumentoDoModeloInput(prazo, null) };

        DaLeitura(ComDocumentos([documento], [])).Should().Contain(
            ($"{Documento}.requisitos.prazoDeEmissao.referencia.faseCodigo", ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido));
    }

    [Fact(DisplayName = "O tipo da referência desconhecido e o código de fase fora do formato recusam os dois")]
    public void Ler_TipoDaReferenciaDesconhecidoEFaseForaDoFormato_RecusaOsDois()
    {
        ValidadeDoDocumentoDoModeloInput validade = new("EXIGE_DOCUMENTO_VALIDO", new DataDeReferenciaDoDocumentoDoModeloInput("DESCONHECIDO", null, "fase-ficticia"), null);
        DocumentoExigidoDoModeloInput documento = Documentos("INSCRICAO", "LAUDO_MEDICO") with { Requisitos = new RequisitosDoDocumentoDoModeloInput(null, validade) };

        DaLeitura(ComDocumentos([documento], [])).Select(static r => r.Campo).Should().Equal(
            $"{Documento}.requisitos.validade.referencia.tipo", $"{Documento}.requisitos.validade.referencia.faseCodigo");
    }

    [Fact(DisplayName = "A opção excetuada inválida depois de uma nula é recusada na posição um")]
    public void Ler_ExcetoComNuloAntes_RecusaNaPosicaoDoPedido()
    {
        DocumentoExigidoDoModeloInput documento = Documentos("INSCRICAO", "LAUDO_MEDICO") with
        {
            Quando = [[]],
            PorOpcao = new ExigenciaPorOpcaoInput("FATO_VALIDO", [null, "opcao-ficticia"]),
        };

        DaLeitura(ComDocumentos([documento], [])).Select(static r => r.Campo).Should().Equal(
            $"{Documento}.quando[0]", $"{Documento}.porOpcao.exceto", $"{Documento}.porOpcao.exceto[1]");
    }

    [Fact(DisplayName = "O texto não gravável da base legal depois de uma nula é recusado na posição um")]
    public void Ler_BaseLegalComNuloAntes_RecusaTextoNaPosicaoDoPedido()
    {
        BaseLegalDoModeloInput baseLegal = new("Lei\u0000", "FEDERAL", "RESOLVIDO", null);
        DocumentoExigidoDoModeloInput documento = Documentos("INSCRICAO", "LAUDO_MEDICO") with
        {
            Quando = [[]],
            Indeferimento = new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", [null, baseLegal], false),
        };

        DaLeitura(ComDocumentos([documento], [])).Should().Contain(
            ($"{Documento}.indeferimento.basesLegais[1].referencia", ModeloFormularioErrorCodes.TextoNaoGravavel));
    }

    [Fact(DisplayName = "No grupo recusado, o texto não gravável da base legal do grupo depois de uma nula é recusado na posição um")]
    public void Ler_BaseLegalDoGrupoComNuloAntes_RecusaTextoNaPosicaoDoPedido()
    {
        BaseLegalDoModeloInput baseLegal = new("Lei\u0000", "FEDERAL", "RESOLVIDO", null);
        GrupoDeDocumentosDoModeloInput grupo = new(1, new("DESCONHECIDA", [null, baseLegal], false), []);

        DaLeitura(ComDocumentos([], [grupo])).Should().Contain(
            ($"{Grupo}.indeferimento.basesLegais[1].referencia", ModeloFormularioErrorCodes.TextoNaoGravavel));
    }

    [Fact(DisplayName = "No grupo recusado, as recusas dos documentos saem na ordem do pedido: o documento zero, lido, antes do um, não lido")]
    public void Ler_DocumentoZeroLidoEUmNaoLido_RecusasNaOrdemDoPedido()
    {
        DocumentoExigidoDoModeloInput lido = Documentos("INSCRICAO", "tipo-ficticio");
        DocumentoExigidoDoModeloInput naoLido = Documentos("INSCRICAO", "LAUDO_MEDICO") with { Quando = [[]] };
        GrupoDeDocumentosDoModeloInput grupo = new(1, null, [lido, naoLido]);

        DaLeitura(ComDocumentos([], [grupo])).Select(static r => r.Campo).Should().Equal(
            $"{Grupo}.documentos[0].tipoDocumentoCodigo", $"{Grupo}.documentos[1].quando[0]");
    }

    [Fact(DisplayName = "No grupo recusado, as recusas dos documentos saem na ordem do pedido: o documento zero, não lido, antes do um, lido")]
    public void Ler_DocumentoZeroNaoLidoEUmLido_RecusasNaOrdemDoPedido()
    {
        DocumentoExigidoDoModeloInput naoLido = Documentos("INSCRICAO", "LAUDO_MEDICO") with { Quando = [[]] };
        DocumentoExigidoDoModeloInput lido = Documentos("INSCRICAO", "tipo-ficticio");
        GrupoDeDocumentosDoModeloInput grupo = new(1, null, [naoLido, lido]);

        DaLeitura(ComDocumentos([], [grupo])).Select(static r => r.Campo).Should().Equal(
            $"{Grupo}.documentos[0].quando[0]", $"{Grupo}.documentos[1].tipoDocumentoCodigo");
    }

    [Fact(DisplayName = "O documento um, não lido, com erro de leitura sai depois do documento zero, com erro de formato")]
    public void Ler_DocumentoZeroComFormatoEDocumentoUmComLeitura_ZeroAntesDeUm()
    {
        DocumentoExigidoDoModeloInput zero = Documentos("fase-ficticia", "LAUDO_MEDICO") with { Quando = [[]] };
        DocumentoExigidoDoModeloInput um = Documentos("INSCRICAO", "LAUDO_MEDICO") with { Quando = [[]] };

        DaLeitura(ComDocumentos([zero, um], [])).Select(static r => r.Campo).Should().Equal(
            $"{Documento}.quando[0]", $"{Documento}.faseCodigo", "conjuntoDeDocumentos.documentosExigidos[1].quando[0]");
    }

    private static ConteudoDoModeloInput ComDocumentos(DocumentoExigidoDoModeloInput[] documentos, GrupoDeDocumentosDoModeloInput[] grupos) =>
        Conteudo(Item("CERTIFICADO", 0)) with { ConjuntoDeDocumentos = new ConjuntoDeDocumentosDoModeloInput(documentos, grupos) };

    private static DocumentoExigidoDoModeloInput Documentos(string fase, string tipo) =>
        new(fase, tipo, null, new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", [], false));
}
