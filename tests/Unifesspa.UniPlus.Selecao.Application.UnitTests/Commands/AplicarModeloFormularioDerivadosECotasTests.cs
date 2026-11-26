namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaAplicacaoDoModeloDeFormulario;

/// <summary>
/// As regras de derivação que a cópia do modelo traz do catálogo: o derivado citado recebe a regra padrão,
/// o já configurado fica como está, o sem regra é recusado, e o que depende da oferta de vagas pede a
/// distribuição antes.
/// </summary>
public sealed class AplicarModeloFormularioDerivadosECotasTests
{
    private readonly CenarioDaAplicacaoDoModeloDeFormulario _cenario = new();

    [Fact(DisplayName = "O derivado que a cópia cita recebe as regras padrão do catálogo; o já configurado fica como está")]
    public async Task Handle_DerivadoCitado_CopiaOuMantem()
    {
        FatoColetadoInput exibido = ItemQueCita(Item("CERTIFICADO", 1), "PERFIL", "ALGUM_DE", new[] { "A" });

        Result<AplicacaoDeModeloDto> copia = await _cenario.AplicarAsync(Modelo(DaInscricao, [Item("QUILOMBOLA", 0), exibido]));
        Result<AplicacaoDeModeloDto> reaplicacao = await _cenario.AplicarAsync(Modelo(DaInscricao, [Item("QUILOMBOLA", 0), exibido]));

        copia.IsSuccess.Should().BeTrue(copia.Error?.Message);
        copia.Value!.DerivacoesCopiadas.Should().Equal("PERFIL");
        reaplicacao.Value!.DerivacoesMantidas.Should().Equal("PERFIL");
        _cenario.Processo.RegrasDerivacao.Should().ContainSingle().Which.CodigoFato.Should().Be("PERFIL");
    }

    [Fact(DisplayName = "O derivado booleano citado é copiado com as regras padrão, que não contribuem código")]
    public async Task Handle_DerivadoBooleanoCitado_CopiaAsRegrasPadrao()
    {
        FatoColetadoInput porFlag = ItemQueCita(Item("CERTIFICADO", 1), "FLAG", "IGUAL", true);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(DaInscricao, [Item("QUILOMBOLA", 0), porFlag]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _cenario.Processo.RegrasDerivacao.Should().ContainSingle().Which.Booleano.Should().BeTrue();
    }

    [Fact(DisplayName = "Derivado sem regra padrão nem configuração é recusado")]
    public async Task Handle_DerivadoSemRegra_Recusa()
    {
        FatoColetadoInput porSemRegra = ItemQueCita(Item("CERTIFICADO", 1, "ESCOLHA_UNICA"), "SEM_REGRA", "ALGUM_DE", new[] { "X" });

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(DaInscricao, [Item("QUILOMBOLA", 0), porSemRegra]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(AplicacaoDeModeloErrorCodes.DerivadoSemRegra);
    }

    [Fact(DisplayName = "Os valores desativados no catálogo citados nas regras são recusados juntos, como vínculo novo")]
    public async Task Handle_ValoresDesativadosNasRegras_RecusaTodos()
    {
        FatoColetadoInput porAmarela = ItemQueCita(Item("QUILOMBOLA", 1), "COR_RACA", "IGUAL", "AMARELA");
        FatoColetadoInput porIndigena = ItemQueCita(Item("CERTIFICADO", 2), "COR_RACA", "IGUAL", "INDIGENA");

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(DeOutraFaseObrigatoria, [Item("COR_RACA", 0, "ESCOLHA_UNICA"), porAmarela, porIndigena]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(VinculoCatalogoErrorCodes.ValorDesativado, VinculoCatalogoErrorCodes.ValorDesativado);
    }

    [Fact(DisplayName = "A modalidade citada pela regra padrão de um derivado também pede a distribuição de vagas antes")]
    public async Task Handle_ModalidadePelaRegraPadrao_PedeADistribuicao()
    {
        FatoColetadoInput porCotista = ItemQueCita(Item("QUILOMBOLA", 1), "COTISTA", "ALGUM_DE", new[] { "SIM" });

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(DaInscricao, [Item("CERTIFICADO", 0), porCotista]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(AplicacaoDeModeloErrorCodes.SemDistribuicaoDeVagas);
    }

    [Fact(DisplayName = "Modelo que cita a modalidade num processo sem distribuição de vagas pede a distribuição antes")]
    public async Task Handle_ModalidadeSemDistribuicaoDeVagas_PedeADistribuicao()
    {
        FatoColetadoInput porModalidade = ItemQueCita(Item("QUILOMBOLA", 1), "MODALIDADE", "ALGUM_DE", new[] { "AC" });

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(DaInscricao, [Item("CERTIFICADO", 0), porModalidade]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(AplicacaoDeModeloErrorCodes.SemDistribuicaoDeVagas);
    }

    [Fact(DisplayName = "Modelo que cita a opção pelas cotas num processo sem cota da lei ofertada é recusado, sem copiar a opção")]
    public async Task Handle_OpcaoPelasCotasSemCotaOfertada_Recusa()
    {
        _cenario.Ofertar(Ofertada("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula));
        FatoColetadoInput porOpcao = ItemQueCita(Item("QUILOMBOLA", 1), OpcaoPelasCotasDaLei.Fato, "IGUAL", true);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(DaInscricao, [Item("CERTIFICADO", 0), porOpcao]));

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(AplicacaoDeModeloErrorCodes.SemCotaDaLeiOfertada);
        _cenario.Processo.RegrasDerivacao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com cota da lei ofertada, a opção pelas cotas vem da oferta e o que ela cita recebe a regra padrão")]
    public async Task Handle_OpcaoPelasCotasComCotaOfertada_CopiaODeQueElaDepende()
    {
        _cenario.Ofertar(
            Ofertada("AC", NaturezaLegalModalidade.Ampla, PredicadoDnf.SemClausula),
            Ofertada("LI_EP", NaturezaLegalModalidade.CotaReservada, Quando(OpcaoPelasCotasDaLei.Fato, true)));
        FatoColetadoInput porOpcao = ItemQueCita(Item("QUILOMBOLA", 2), OpcaoPelasCotasDaLei.Fato, "IGUAL", true);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(
            DaInscricao, [Item("CERTIFICADO", 0), Item(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 1), porOpcao]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.DerivacoesMantidas.Should().Contain(OpcaoPelasCotasDaLei.Fato);
        resultado.Value.DerivacoesCopiadas.Should().Equal(OrigemEscolar.FatoEgresso);
        _cenario.Processo.RegrasDerivacao.Select(static r => r.CodigoFato).Should().Equal(OrigemEscolar.FatoEgresso);
    }
}
