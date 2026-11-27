namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A leitura dos requisitos de um documento do modelo: cada código vem do catálogo e o desconhecido é recusado
/// no caminho dele; que os detalhes combinem com a escolha quem confere é o processo, ao receber a cópia.
/// </summary>
public sealed class RequisitosLidosDoDocumentoTests
{
    private const string CodigoDesconhecido = "naoExiste";

    private static RequisitosDoDocumentoDoModeloBuilder Requisitos() => RequisitosDoDocumentoDoModeloBuilder.AindaNaoEscolhidos();

    private static RequisitosDoDocumentoDoModelo PrazoDe90DiasEValidadeNoFimDaMatricula() => new(
        new PrazoDeEmissaoDoModelo(
            EscolhaDoPrazoDeEmissao.ComPrazo, 90, UnidadeDoPrazoDeEmissao.Dias,
            new DataDeReferenciaDoDocumentoDoModelo(TipoDaDataDeReferencia.InicioDaFase, "HABILITACAO", null), EfeitoNaAnaliseDoDocumento.RecusarDocumento),
        new ValidadeDoDocumentoDoModelo(
            EscolhaDaValidade.ExigeDocumentoValido,
            new DataDeReferenciaDoDocumentoDoModelo(TipoDaDataDeReferencia.FimDaFase, "MATRICULA", null),
            EfeitoNaAnaliseDoDocumento.RecusarDocumento));

    private static void DeveRecusar(RequisitosDoDocumentoDoModeloInput entrada, string campo, string codigo)
    {
        RequisitosLidosDoDocumento lidos = RequisitosLidosDoDocumento.Ler(entrada);

        FieldError recusa = lidos.Recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be(campo);
        recusa.Error.Code.Should().Be(codigo);
        recusa.Error.Message.Should().NotContain(CodigoDesconhecido);
        lidos.Declarados.Should().BeNull();
    }

    [Fact(DisplayName = "Sem requisitos na entrada, nenhuma escolha foi feita e não há recusa")]
    public void Ler_EntradaAusente_AindaNaoEscolhidos()
    {
        RequisitosLidosDoDocumento lidos = RequisitosLidosDoDocumento.Ler(null);

        lidos.Recusas.Should().BeEmpty();
        lidos.Declarados.Should().Be(RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos);
    }

    [Fact(DisplayName = "O prazo de emissão e a validade com códigos do catálogo são lidos com a fase pelo código")]
    public void Ler_PrazoEValidadeComCodigosConhecidos_DeclaraOsRequisitos()
    {
        RequisitosDoDocumentoDoModeloInput entrada = Requisitos()
            .ComPrazoDe(90, "DIAS", RequisitosDoDocumentoDoModeloBuilder.NoInicioDaFase("HABILITACAO"))
            .ExigindoValidadeNa(RequisitosDoDocumentoDoModeloBuilder.NoFimDaFase("MATRICULA"), "RECUSAR_DOCUMENTO")
            .Construir();

        RequisitosLidosDoDocumento lidos = RequisitosLidosDoDocumento.Ler(entrada);

        lidos.Recusas.Should().BeEmpty();
        lidos.Declarados.Should().Be(PrazoDe90DiasEValidadeNoFimDaMatricula());
    }

    [Fact(DisplayName = "A data específica da referência é lida junto com o tipo")]
    public void Ler_ReferenciaComDataEspecifica_GuardaAData()
    {
        DataDeReferenciaDoDocumentoDoModeloInput referencia = new("DATA_ESPECIFICA", new DateOnly(2027, 2, 1), null);

        RequisitosLidosDoDocumento lidos = RequisitosLidosDoDocumento.Ler(Requisitos().ExigindoValidadeNa(referencia).Construir());

        lidos.Declarados!.Validade!.Referencia.Should().Be(new DataDeReferenciaDoDocumentoDoModelo(TipoDaDataDeReferencia.DataEspecifica, null, new DateOnly(2027, 2, 1)));
    }

    [Fact(DisplayName = "Escolher só os códigos de escolha, sem detalhes, é lido sem recusa")]
    public void Ler_SomenteAsEscolhas_DeclaraSemDetalhes()
    {
        RequisitosDoDocumentoDoModeloInput entrada = Requisitos()
            .SemPrazo()
            .ComValidade(new ValidadeDoDocumentoDoModeloInput("NAO_EXIGE_DOCUMENTO_VALIDO", null, null))
            .Construir();

        RequisitosLidosDoDocumento lidos = RequisitosLidosDoDocumento.Ler(entrada);

        lidos.Recusas.Should().BeEmpty();
        lidos.Declarados.Should().Be(new RequisitosDoDocumentoDoModelo(
            new PrazoDeEmissaoDoModelo(EscolhaDoPrazoDeEmissao.SemPrazo, null, null, null, null),
            new ValidadeDoDocumentoDoModelo(EscolhaDaValidade.NaoExigeDocumentoValido, null, null)));
    }

    [Fact(DisplayName = "A coerência entre a escolha e os detalhes não é conferida no modelo: sem prazo com valor é lido sem recusa")]
    public void Ler_DetalhesQueNaoCombinamComAEscolha_NaoSaoRecusados()
    {
        PrazoDeEmissaoDoModeloInput prazo = new("SEM_PRAZO", 30, "DIAS", null, "RECUSAR_DOCUMENTO");

        RequisitosLidosDoDocumento lidos = RequisitosLidosDoDocumento.Ler(Requisitos().ComPrazo(prazo).Construir());

        lidos.Recusas.Should().BeEmpty();
        lidos.Declarados!.PrazoDeEmissao.Should().Be(
            new PrazoDeEmissaoDoModelo(EscolhaDoPrazoDeEmissao.SemPrazo, 30, UnidadeDoPrazoDeEmissao.Dias, null, EfeitoNaAnaliseDoDocumento.RecusarDocumento));
    }

    [Fact(DisplayName = "O código de escolha do prazo de emissão desconhecido é recusado no campo dele")]
    public void Ler_EscolhaDoPrazoDesconhecida_Recusa() =>
        DeveRecusar(Requisitos().ComEscolhaDePrazo(CodigoDesconhecido).Construir(), "requisitos.prazoDeEmissao.escolha", PrazoDeEmissaoErrorCodes.EscolhaInvalida);

    [Fact(DisplayName = "O prazo de emissão informado sem código de escolha é recusado no campo da escolha")]
    public void Ler_EscolhaDoPrazoAusente_Recusa() =>
        DeveRecusar(Requisitos().ComEscolhaDePrazo(null).Construir(), "requisitos.prazoDeEmissao.escolha", PrazoDeEmissaoErrorCodes.EscolhaInvalida);

    [Fact(DisplayName = "O código de unidade do prazo desconhecido é recusado no campo dele")]
    public void Ler_UnidadeDoPrazoDesconhecida_Recusa() =>
        DeveRecusar(
            Requisitos().ComPrazoDe(1, CodigoDesconhecido, RequisitosDoDocumentoDoModeloBuilder.NoFimDasInscricoes()).Construir(),
            "requisitos.prazoDeEmissao.unidade",
            PrazoDeEmissaoErrorCodes.UnidadeInvalida);

    [Fact(DisplayName = "O código de efeito do prazo desconhecido é recusado no campo dele")]
    public void Ler_EfeitoDoPrazoDesconhecido_Recusa() =>
        DeveRecusar(
            Requisitos().ComPrazoDe(1, "DIAS", RequisitosDoDocumentoDoModeloBuilder.NoFimDasInscricoes(), CodigoDesconhecido).Construir(),
            "requisitos.prazoDeEmissao.efeitoForaDoPrazo",
            EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido);

    [Fact(DisplayName = "O tipo da referência do prazo desconhecido é recusado no campo dele")]
    public void Ler_TipoDaReferenciaDoPrazoDesconhecido_Recusa() =>
        DeveRecusar(
            Requisitos().ComPrazoDe(1, "DIAS", new DataDeReferenciaDoDocumentoDoModeloInput(CodigoDesconhecido, null, null)).Construir(),
            "requisitos.prazoDeEmissao.referencia.tipo",
            DataDeReferenciaDoDocumentoErrorCodes.TipoInvalido);

    [Fact(DisplayName = "O código de escolha da validade desconhecido é recusado no campo dele")]
    public void Ler_EscolhaDaValidadeDesconhecida_Recusa() =>
        DeveRecusar(
            Requisitos().ComValidade(new ValidadeDoDocumentoDoModeloInput(CodigoDesconhecido, null, null)).Construir(),
            "requisitos.validade.escolha",
            ValidadeDoDocumentoErrorCodes.EscolhaInvalida);

    [Fact(DisplayName = "O código de efeito da validade desconhecido é recusado no campo dele")]
    public void Ler_EfeitoDaValidadeDesconhecido_Recusa() =>
        DeveRecusar(
            Requisitos().ExigindoValidadeNa(RequisitosDoDocumentoDoModeloBuilder.NoFimDasInscricoes(), CodigoDesconhecido).Construir(),
            "requisitos.validade.efeitoDoVencimento",
            EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido);

    [Fact(DisplayName = "O tipo da referência da validade desconhecido é recusado no campo dele")]
    public void Ler_TipoDaReferenciaDaValidadeDesconhecido_Recusa() =>
        DeveRecusar(
            Requisitos().ExigindoValidadeNa(new DataDeReferenciaDoDocumentoDoModeloInput(CodigoDesconhecido, null, null)).Construir(),
            "requisitos.validade.referencia.tipo",
            DataDeReferenciaDoDocumentoErrorCodes.TipoInvalido);

    [Fact(DisplayName = "Os códigos desconhecidos do prazo e da validade são recusados juntos, na ordem da leitura")]
    public void Ler_VariosCodigosDesconhecidos_AcumulaAsRecusas()
    {
        RequisitosDoDocumentoDoModeloInput entrada = Requisitos()
            .ComPrazoDe(1, CodigoDesconhecido, new DataDeReferenciaDoDocumentoDoModeloInput(CodigoDesconhecido, null, null), CodigoDesconhecido)
            .ComValidade(new ValidadeDoDocumentoDoModeloInput(CodigoDesconhecido, null, CodigoDesconhecido))
            .Construir();

        RequisitosLidosDoDocumento lidos = RequisitosLidosDoDocumento.Ler(entrada);

        lidos.Recusas.Select(static recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("requisitos.prazoDeEmissao.unidade", PrazoDeEmissaoErrorCodes.UnidadeInvalida),
            ("requisitos.prazoDeEmissao.referencia.tipo", DataDeReferenciaDoDocumentoErrorCodes.TipoInvalido),
            ("requisitos.prazoDeEmissao.efeitoForaDoPrazo", EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido),
            ("requisitos.validade.escolha", ValidadeDoDocumentoErrorCodes.EscolhaInvalida),
            ("requisitos.validade.efeitoDoVencimento", EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido));
        lidos.Declarados.Should().BeNull();
    }

    [Fact(DisplayName = "A escolha do prazo desconhecida não impede guardar o código de fase da referência no caminho do prazo")]
    public void Ler_EscolhaDoPrazoDesconhecidaComReferencia_GuardaOCodigoDeFase()
    {
        PrazoDeEmissaoDoModeloInput prazo = new(CodigoDesconhecido, 90, "DIAS", new DataDeReferenciaDoDocumentoDoModeloInput("INICIO_DA_FASE", null, "FASE_DO_PRAZO"), null);

        RequisitosLidosDoDocumento lidos = RequisitosLidosDoDocumento.Ler(new RequisitosDoDocumentoDoModeloInput(prazo, null));

        lidos.ReferenciasInformadas.Should().Equal(("requisitos.prazoDeEmissao.referencia.faseCodigo", "FASE_DO_PRAZO"));
        lidos.Declarados.Should().BeNull();
    }

    [Fact(DisplayName = "O tipo da referência da validade desconhecido não impede guardar o código de fase dela")]
    public void Ler_TipoDaReferenciaDesconhecido_GuardaOCodigoDeFase()
    {
        ValidadeDoDocumentoDoModeloInput validade = new("EXIGE_DOCUMENTO_VALIDO", new DataDeReferenciaDoDocumentoDoModeloInput(CodigoDesconhecido, null, "FASE_DA_VALIDADE"), null);

        RequisitosLidosDoDocumento lidos = RequisitosLidosDoDocumento.Ler(new RequisitosDoDocumentoDoModeloInput(null, validade));

        lidos.ReferenciasInformadas.Should().Equal(("requisitos.validade.referencia.faseCodigo", "FASE_DA_VALIDADE"));
    }

    [Fact(DisplayName = "Sem referência no pedido, nenhum código de fase é guardado")]
    public void Ler_SemReferencia_NaoGuardaCodigoDeFase() =>
        RequisitosLidosDoDocumento.Ler(new RequisitosDoDocumentoDoModeloInput(new PrazoDeEmissaoDoModeloInput("SEM_PRAZO", null, null, null, null), null))
            .ReferenciasInformadas.Should().BeEmpty();
}
