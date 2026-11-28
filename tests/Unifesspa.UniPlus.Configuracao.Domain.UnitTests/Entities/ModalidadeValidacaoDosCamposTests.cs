namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDaModalidade;

/// <summary>Acúmulo de violações independentes e validação isolada dos campos de <see cref="Modalidade"/> (ADR-0125).</summary>
public sealed class ModalidadeValidacaoDosCamposTests
{
    // ── Acumulação e dependência entre campos (ADR-0125) ────────────────

    [Fact(DisplayName = "Código ausente, descrição longa e natureza inválida acumulam as três violações")]
    public void Criar_TresViolacoesIndependentes_AcumulaAsTres()
    {
        Result<Modalidade> r = Criar("", d => d with { Descricao = new string('a', 301), CodigoDaNaturezaLegal = "XPTO" });

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().HaveCount(3);
        r.Errors[0].Field.Should().Be("codigo");
        r.Errors[0].Error.Code.Should().Be(ModalidadeErrorCodes.CodigoObrigatorio);
        r.Errors[1].Field.Should().Be("descricao");
        r.Errors[2].Field.Should().Be("naturezaLegal");
    }

    [Fact(DisplayName = "Os quatro códigos de enum desconhecidos acumulam uma recusa cada, sem repetir o valor recebido")]
    public void Criar_QuatroCodigosDesconhecidos_AcumulaUmaRecusaPorCampo()
    {
        Result<Modalidade> r = Criar("AC_X", d => d with
        {
            CodigoDaNaturezaLegal = "ampla",
            CodigoDaComposicaoDeVagas = " DENTRO_DA_RESERVA_LEGAL",
            CodigoDaRegraDeRemanejamento = "CRUZADO ",
            CodigoDaAcaoQuandoIndeferido = "ELIMINA_DO_PROCESSO",
        });

        r.Errors.Select(erro => erro.Field).Should().Equal("naturezaLegal", "composicaoVagas", "remanejamento.regra", "acaoQuandoIndeferido");
        r.Errors.Select(erro => erro.Error.Message).Should().NotContain(mensagem => mensagem.Contains("ELIMINA_DO_PROCESSO", StringComparison.Ordinal));
        r.Errors[0].Error.Message.Should().Be("Natureza legal deve ser uma de: COTA_RESERVADA, AMPLA, ACAO_AFIRMATIVA.");
    }

    [Theory(DisplayName = "Códigos vazios, em branco ou minúsculos são recusados por campo")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ampla")]
    public void Criar_CodigosInvalidos_RecusaCadaCampo(string codigo)
    {
        Result<Modalidade> resultado = Criar("AC_X", d => d with
        {
            CodigoDaNaturezaLegal = codigo,
            CodigoDaComposicaoDeVagas = codigo,
            CodigoDaRegraDeRemanejamento = codigo,
            CodigoDaAcaoQuandoIndeferido = codigo,
        });

        resultado.Errors.Select(erro => erro.Error.Code).Should().Equal(
            ModalidadeErrorCodes.NaturezaInvalida, ModalidadeErrorCodes.ComposicaoVagasInvalida,
            ModalidadeErrorCodes.RegraRemanejamentoInvalida, ModalidadeErrorCodes.AcaoIndeferimentoInvalida);
    }

    [Fact(DisplayName = "Códigos ausentes usam os padrões e mantêm os campos opcionais ausentes")]
    public void Criar_CodigosAusentes_AceitaPadroes()
    {
        Result<Modalidade> resultado = Criar("AC_X", d => d with
        {
            CodigoDaNaturezaLegal = null,
            CodigoDaComposicaoDeVagas = null,
            CodigoDaRegraDeRemanejamento = null,
            CodigoDaAcaoQuandoIndeferido = null,
        });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.NaturezaLegal.Should().Be(Unifesspa.UniPlus.Configuracao.Domain.Enums.NaturezaLegal.Ampla);
        resultado.Value.ComposicaoVagas.Should().Be(Unifesspa.UniPlus.Configuracao.Domain.Enums.ComposicaoVagas.RestanteDasVagasOfertadas);
    }

    [Fact(DisplayName = "Regra fora do domínio com parâmetro presente reporta só o código inválido, não a incoerência derivada")]
    public void Criar_RegraInvalidaComParametro_NaoDerivaIncoerenciaDeParametro()
    {
        Result<Modalidade> r = Criar("AC_I", d => d.ComoAcaoAfirmativa("CASCATA") with { RemanejamentoDestino = "AC" });

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().HaveCount(1,
            "a invariante de parâmetros por regra depende de RegraRemanejamento já reconhecida — " +
            "reportá-la sobre um código que nem chegou a resolver seria um erro derivado, não independente");
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.RegraRemanejamentoInvalida);
    }

    [Fact(DisplayName = "ValidarCampos isolado é público e reusável sem instanciar o agregado")]
    public void ValidarCampos_CamposValidos_Aceita()
    {
        Result<CamposResolvidosDaModalidade> r = ValidacaoDosCamposDaModalidade.ValidarCampos(new DeclaracaoDaModalidade(
            Nome: "Modalidade de teste", Descricao: null, CodigoDaNaturezaLegal: "AMPLA", CodigoDaComposicaoDeVagas: "RESTANTE_DAS_VAGAS_OFERTADAS",
            ComposicaoOrigem: null, CodigoDaRegraDeRemanejamento: null, RemanejamentoDestino: null,
            RemanejamentoPar: null, RemanejamentoFallback: null, RegraDeAplicabilidade: Incondicional(),
            CodigoDaAcaoQuandoIndeferido: null, BaseLegal: null));

        r.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "ValidarCamposDoPayload não avalia coerência cruzada — é só o pré-check do handler")]
    public void ValidarCamposDoPayload_PayloadIncoerenteMasComTokensValidos_Aceita()
    {
        // AMPLA + SEGUE_ORDEM_DE_REMANEJAMENTO é incoerente (invariante 3), mas ValidarCamposDoPayload
        // não avalia coerência cruzada — só parse e tamanhos. A guarda do catálogo
        // legal fixo (handler) precisa rodar ANTES dessa incoerência ser reportada.
        Result r = ValidacaoDosCamposDaModalidade.ValidarCamposDoPayload(new DeclaracaoDaModalidade(
            Nome: "Modalidade de teste", Descricao: null, CodigoDaNaturezaLegal: "AMPLA", CodigoDaComposicaoDeVagas: "DENTRO_DA_RESERVA_LEGAL",
            ComposicaoOrigem: null, CodigoDaRegraDeRemanejamento: "SEGUE_ORDEM_DE_REMANEJAMENTO", RemanejamentoDestino: null,
            RemanejamentoPar: null, RemanejamentoFallback: null, RegraDeAplicabilidade: Incondicional(),
            CodigoDaAcaoQuandoIndeferido: null, BaseLegal: null));

        r.IsSuccess.Should().BeTrue();
    }
}
