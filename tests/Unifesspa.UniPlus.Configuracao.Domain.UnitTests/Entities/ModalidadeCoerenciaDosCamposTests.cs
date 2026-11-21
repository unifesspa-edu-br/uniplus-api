namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDaModalidade;

/// <summary>Coerência entre os campos de <see cref="Modalidade"/> na criação: natureza e remanejamento, origem da composição, parâmetros por regra, ação quando indeferido e regra de aplicabilidade pela natureza.</summary>
public sealed class ModalidadeCoerenciaDosCamposTests
{
    // ── Coerência natureza ↔ remanejamento (invariante 3) ──────────────────────

    [Fact(DisplayName = "Cota reservada SEM cascata é incoerente")]
    public void Criar_CotaSemCascata_Incoerente()
    {
        Result<Modalidade> r = Criar(ajuste: d => d with
        {
            NaturezaLegalToken = "COTA_RESERVADA",
            ComposicaoVagasToken = "DENTRO_DA_RESERVA_LEGAL",
            RegraRemanejamentoToken = null,
        });

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
    }

    [Fact(DisplayName = "Ampla COM remanejamento é incoerente")]
    public void Criar_AmplaComRemanejamento_Incoerente()
    {
        Result<Modalidade> r = Criar(ajuste: d => d with
        {
            NaturezaLegalToken = "AMPLA",
            RegraRemanejamentoToken = "SEGUE_CASCATA",
        });

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
    }

    [Fact(DisplayName = "Ação afirmativa com SEGUE_CASCATA é incoerente (exige destino único ou cruzado)")]
    public void Criar_AcaoAfirmativaComCascata_Incoerente()
    {
        Result<Modalidade> r = Criar(ajuste: d => d.ComoAcaoAfirmativa("SEGUE_CASCATA"));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
    }

    [Fact(DisplayName = "Contraprova: ação afirmativa com DESTINO_UNICO + destino é coerente")]
    public void Criar_AcaoAfirmativaComDestinoUnico_Coerente()
    {
        Result<Modalidade> r = Criar("AC_I", d => d.ComoAcaoAfirmativa("DESTINO_UNICO") with { RemanejamentoDestino = "AC" });

        r.IsSuccess.Should().BeTrue();
        r.Value!.RegraRemanejamento.Should().Be(RegraRemanejamento.DestinoUnico);
    }

    // ── Equivalência composição RETIRADA_DE_OUTRA_MODALIDADE ⟺ origem (invariante 4) ──────────────

    [Fact(DisplayName = "RETIRADA_DE_OUTRA_MODALIDADE sem origem é rejeitado (origem obrigatória)")]
    public void Criar_RetiradaDeOutraModalidadeSemOrigem_Falha()
    {
        Result<Modalidade> r = Criar(ajuste: d => d with
        {
            ComposicaoVagasToken = "RETIRADA_DE_OUTRA_MODALIDADE",
            ComposicaoOrigem = null,
        });

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.OrigemObrigatoriaParaRetiradaDeOutraModalidade);
    }

    [Fact(DisplayName = "Origem preenchida sem RETIRADA_DE_OUTRA_MODALIDADE é rejeitada (origem apenas para RETIRADA_DE_OUTRA_MODALIDADE)")]
    public void Criar_OrigemSemRetiradaDeOutraModalidade_Falha()
    {
        Result<Modalidade> r = Criar(ajuste: d => d with
        {
            ComposicaoVagasToken = "RESTANTE_DAS_VAGAS_OFERTADAS",
            ComposicaoOrigem = "AC",
        });

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.OrigemApenasParaRetiradaDeOutraModalidade);
    }

    // O field chega ao cliente sem tradução: apontar o seletor da regra faria a tela destacar um campo correto e
    // deixar o inválido sem marca.
    [Theory(DisplayName = "Modalidade que referencia a si mesma é rejeitada, apontando o campo da referência")]
    [InlineData("origem", "Modalidade.ComposicaoOrigemCircular", "composicaoOrigem")]
    [InlineData("destino", "Modalidade.RemanejamentoDestinoCircular", "remanejamentoDestino")]
    [InlineData("par", "Modalidade.RemanejamentoParCircular", "remanejamentoPar")]
    [InlineData("fallback", "Modalidade.RemanejamentoFallbackCircular", "remanejamentoFallback")]
    public void Criar_ReferenciandoSiMesma_Falha(string referencia, string codeEsperado, string fieldEsperado)
    {
        Result<Modalidade> resultado = referencia switch
        {
            "origem" => Criar("AC_PCD", d => d with
            {
                ComposicaoVagasToken = "RETIRADA_DE_OUTRA_MODALIDADE",
                ComposicaoOrigem = "AC_PCD",
            }),
            "destino" => Criar("AC_I", d => d.ComoAcaoAfirmativa("DESTINO_UNICO") with { RemanejamentoDestino = "AC_I" }),
            "par" => Criar("AC_I", d => d.ComoAcaoAfirmativa("CRUZADO") with { RemanejamentoPar = "AC_I" }),
            _ => Criar("AC_I", d => d.ComoAcaoAfirmativa("CRUZADO") with { RemanejamentoPar = "AC_Q", RemanejamentoFallback = "AC_I" }),
        };

        resultado.IsFailure.Should().BeTrue("uma modalidade que cita a si mesma descreve uma operação que não acontece");
        resultado.Errors.Should().Contain(e => e.Error.Code == codeEsperado && e.Field == fieldEsperado);
    }

    [Fact(DisplayName = "Autorreferência é reportada junto de violação sem relação, na mesma resposta")]
    public void Criar_AutorreferenciaComOutraViolacao_AcumulaAsDuas()
    {
        Result<Modalidade> resultado = Criar("AC_I", d => d.ComoAcaoAfirmativa("DESTINO_UNICO") with
        {
            Descricao = new string('x', 500),
            RemanejamentoDestino = "AC_I",
        });

        resultado.IsFailure.Should().BeTrue();

        // Guardar a autorreferência atrás do sucesso das demais validações faria o cliente
        // corrigir a descrição, reenviar e só então descobrir o segundo problema.
        resultado.Errors.Should().Contain(e => e.Error.Code == ModalidadeErrorCodes.RemanejamentoDestinoCircular);
        resultado.Errors.Should().Contain(e => e.Field == "descricao");
    }

    [Fact(DisplayName = "Contraprova: RETIRADA_DE_OUTRA_MODALIDADE com origem é aceito")]
    public void Criar_RetiradaDeOutraModalidadeComOrigem_Aceita()
    {
        Modalidade m = Criar("AC_PCD", d => d with
        {
            NaturezaLegalToken = "AMPLA",
            ComposicaoVagasToken = "RETIRADA_DE_OUTRA_MODALIDADE",
            ComposicaoOrigem = "AC",
        }).Value!;

        m.ComposicaoVagas.Should().Be(ComposicaoVagas.RetiradaDeOutraModalidade);
        m.ComposicaoOrigem.Should().Be("AC");
    }

    // ── Parâmetros por regra (invariante 5) ────────────────────────────────────

    [Fact(DisplayName = "DESTINO_UNICO sem destino é rejeitado")]
    public void Criar_DestinoUnicoSemDestino_Falha()
    {
        Result<Modalidade> r = Criar(ajuste: d => d.ComoAcaoAfirmativa("DESTINO_UNICO"));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.ParametrosRemanejamentoObrigatorio);
    }

    [Fact(DisplayName = "CRUZADO sem par é rejeitado — o par é o que define o cruzamento")]
    public void Criar_CruzadoSemPar_Falha()
    {
        Result<Modalidade> r = Criar(ajuste: d => d.ComoAcaoAfirmativa("CRUZADO") with { RemanejamentoFallback = "AC" });

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.ParametrosRemanejamentoObrigatorio);
    }

    [Fact(DisplayName = "CRUZADO com par e sem fallback é aceito — o par sem destino final deixa a vaga ociosa")]
    public void Criar_CruzadoSemFallback_Aceita()
    {
        Modalidade m = Criar(ajuste: d => d.ComoAcaoAfirmativa("CRUZADO") with { RemanejamentoPar = "AC_Q" }).Value!;

        m.RemanejamentoParametros.Par.Should().Be("AC_Q");
        m.RemanejamentoParametros.Fallback.Should().BeNull(
            "no certame isolado não há ampla concorrência para receber a vaga que nenhuma "
            + "das duas modalidades preencheu — a ausência de fallback declara isso");
        m.RemanejamentoParametros.Destino.Should().BeNull();
    }

    [Fact(DisplayName = "Contraprova: CRUZADO com par e fallback é aceito")]
    public void Criar_CruzadoComParFallback_Aceita()
    {
        Modalidade m = Criar("AC_I", d => d.ComoAcaoAfirmativa("CRUZADO") with
        {
            RemanejamentoPar = "AC",
            RemanejamentoFallback = "LB_PPI",
        }).Value!;

        m.RemanejamentoParametros.Par.Should().Be("AC");
        m.RemanejamentoParametros.Fallback.Should().Be("LB_PPI");
        m.RemanejamentoParametros.Destino.Should().BeNull();
    }

    [Fact(DisplayName = "SEGUE_CASCATA com parâmetros preenchidos é rejeitado (nenhum arg admitido)")]
    public void Criar_CascataComParametros_Falha()
    {
        Result<Modalidade> r = Criar(ajuste: d => d.ComoCotaReservada() with { RemanejamentoDestino = "AC" });

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.ParametrosRemanejamentoObrigatorio);
    }

    // ── Regra de aplicabilidade pela natureza ──────────────────────────────────

    [Fact(DisplayName = "Modalidade sem regra de aplicabilidade é recusada")]
    public void Criar_SemRegraDeAplicabilidade_Falha()
    {
        Result<Modalidade> r = Modalidade.Criar("AC", new DeclaracaoDaModalidade(
            null, "AMPLA", "RESTANTE_DAS_VAGAS_OFERTADAS", null, null, null, null, null, null, null, null));

        r.IsFailure.Should().BeTrue();
        r.Errors.Should().ContainSingle(e => e.Field == "regraDeAplicabilidade"
            && e.Error.Code == ModalidadeErrorCodes.RegraDeAplicabilidadeObrigatoria);
    }

    [Fact(DisplayName = "Ampla concorrência com condição na regra é recusada")]
    public void Criar_AmplaComCondicao_Falha()
    {
        Result<Modalidade> r = Criar(ajuste: d => d with { RegraDeAplicabilidade = Clausulas([("PCD", true)]) });

        r.Errors.Should().ContainSingle(e => e.Error.Code == ModalidadeErrorCodes.AmplaConcorrenciaComCondicao);
    }

    [Fact(DisplayName = "Cota em que uma cláusula não exige a opção pelas cotas da lei é recusada")]
    public void Criar_CotaComClausulaSemOpcao_Falha()
    {
        Result<Modalidade> r = Criar("PSIQ_COTA", d => d.ComoCotaReservada() with
        {
            RegraDeAplicabilidade = Clausulas([(OpcaoPelasCotasDaLei.Fato, true), ("PCD", true)], [("QUILOMBOLA", true)]),
        });

        r.Errors.Should().ContainSingle(e => e.Error.Code == ModalidadeErrorCodes.CotaSemOpcaoPelasCotasDaLei);
    }

    [Fact(DisplayName = "Cota incondicional é recusada: só concorre a ela quem optou pelas cotas da lei")]
    public void Criar_CotaIncondicional_Falha()
    {
        Result<Modalidade> r = Criar("PSIQ_COTA", d => d.ComoCotaReservada() with { RegraDeAplicabilidade = Incondicional() });

        r.Errors.Should().ContainSingle(e => e.Error.Code == ModalidadeErrorCodes.CotaSemOpcaoPelasCotasDaLei);
    }

    [Fact(DisplayName = "Ação afirmativa aberta a quem optou pelas cotas da lei é recusada")]
    public void Criar_AcaoAfirmativaComOpcao_Falha()
    {
        Result<Modalidade> r = Criar("PSIQ_AA", d => d.ComoAcaoAfirmativa("DESTINO_UNICO") with
        {
            RemanejamentoDestino = "AC",
            RegraDeAplicabilidade = ComOpcaoPelasCotas(optou: true, ("INDIGENA", true)),
        });

        r.Errors.Should().ContainSingle(e => e.Error.Code == ModalidadeErrorCodes.AcaoAfirmativaComOpcaoPelasCotasDaLei);
    }

    [Fact(DisplayName = "Contraprova: cota e ação afirmativa com a opção certa em toda cláusula são aceitas")]
    public void Criar_OpcaoCoerenteComANatureza_Aceita()
    {
        Criar("PSIQ_COTA", d => d.ComoCotaReservada() with
        {
            RegraDeAplicabilidade = Clausulas([(OpcaoPelasCotasDaLei.Fato, true), ("PCD", true)], [(OpcaoPelasCotasDaLei.Fato, true), ("QUILOMBOLA", true)]),
        })
            .IsSuccess.Should().BeTrue();
        Criar("PSIQ_AA", d => d.ComoAcaoAfirmativa("DESTINO_UNICO") with
        {
            RemanejamentoDestino = "AC",
            RegraDeAplicabilidade = ComOpcaoPelasCotas(optou: false, ("INDIGENA", true)),
        })
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A reserva para pessoa com deficiência que não cita a opção pelas cotas da lei é aceita")]
    public void Criar_AcPcdSemCitarAOpcao_Aceita() =>
        Criar("AC_PCD", d => d.ComoAcaoAfirmativa("DESTINO_UNICO") with { RemanejamentoDestino = "AC", RegraDeAplicabilidade = RegraDaAcPcd() })
            .IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "A reserva para pessoa com deficiência que exige não ter optado pelas cotas da lei é recusada, e o detalhe explica a exceção")]
    public void Criar_AcPcdExigindoNaoTerOptado_FalhaComODetalheDaExcecao()
    {
        Result<Modalidade> r = Criar("AC_PCD", d => d.ComoAcaoAfirmativa("DESTINO_UNICO") with
        {
            RemanejamentoDestino = "AC",
            RegraDeAplicabilidade = ComOpcaoPelasCotas(optou: false, ("PCD", true), ("CONCORRER_PCD", true)),
        });

        r.Errors.Should().ContainSingle(e => e.Error.Code == ModalidadeErrorCodes.AcaoAfirmativaComOpcaoPelasCotasDaLei
            && e.Error.Message.Contains("não cita OPTOU_PELAS_COTAS_DA_LEI", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A ação afirmativa institucional que não cita a opção pelas cotas da lei continua recusada")]
    public void Criar_AcaoAfirmativaInstitucionalSemCitarAOpcao_Falha()
    {
        Result<Modalidade> r = Criar("PSIQ_AA", d => d.ComoAcaoAfirmativa("DESTINO_UNICO") with
        {
            RemanejamentoDestino = "AC",
            RegraDeAplicabilidade = Clausulas([("INDIGENA", true)]),
        });

        r.Errors.Should().ContainSingle(e => e.Error.Code == ModalidadeErrorCodes.AcaoAfirmativaComOpcaoPelasCotasDaLei
            && e.Error.Message.Contains("exige OPTOU_PELAS_COTAS_DA_LEI falso", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Com o código recusado, a regra da ação afirmativa não é conferida: só o código é recusado")]
    public void Criar_AcaoAfirmativaComCodigoInvalido_RecusaSoOCodigo()
    {
        Result<Modalidade> r = Criar("ac pcd", d => d.ComoAcaoAfirmativa("DESTINO_UNICO") with
        {
            RemanejamentoDestino = "AC",
            RegraDeAplicabilidade = RegraDaAcPcd(),
        });

        r.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModalidadeErrorCodes.CodigoFormatoInvalido);
    }
}
