namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ConfiguracaoDerivacaoFato.Criar"/> (Story #927, ADR-0125) — código
/// do fato, presença de regras e unicidade de ordem — e de
/// <see cref="ConfiguracaoDerivacaoFato.ValidarFormaBasica"/> (a checagem pura, sem regras
/// resolvidas, que o handler roda antes do vocabulário).
/// </summary>
public sealed class ConfiguracaoDerivacaoFatoTests
{
    private static RegraDerivacaoConfigurada Regra(int ordem, string contribui) =>
        RegraDerivacaoConfigurada.Criar(ordem, contribui, null).Value!;

    [Fact(DisplayName = "Criar com código de fato e ao menos uma regra é aceito")]
    public void Criar_ComRegra_Aceita()
    {
        Result<ConfiguracaoDerivacaoFato> resultado = ConfiguracaoDerivacaoFato.Criar("MODALIDADE", [Regra(0, "AC")]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Regras.Should().ContainSingle();
    }

    [Fact(DisplayName = "Criar com código de fato vazio falha")]
    public void Criar_CodigoFatoVazio_Recusa()
    {
        Result<ConfiguracaoDerivacaoFato> resultado = ConfiguracaoDerivacaoFato.Criar("", [Regra(0, "AC")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoObrigatorio);
    }

    [Fact(DisplayName = "Criar sem regra alguma falha")]
    public void Criar_SemRegras_Recusa()
    {
        Result<ConfiguracaoDerivacaoFato> resultado = ConfiguracaoDerivacaoFato.Criar("MODALIDADE", []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConfiguracaoDerivacaoFatoErrorCodes.SemRegras);
    }

    [Fact(DisplayName = "Criar com ordens duplicadas entre regras falha")]
    public void Criar_OrdemDuplicada_Recusa()
    {
        Result<ConfiguracaoDerivacaoFato> resultado = ConfiguracaoDerivacaoFato.Criar(
            "MODALIDADE", [Regra(0, "AC"), Regra(0, "L1")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConfiguracaoDerivacaoFatoErrorCodes.OrdemRegraDuplicada);
    }

    [Fact(DisplayName = "ADR-0125: código de fato vazio e sem regras acumulam no mesmo lote")]
    public void Criar_CodigoFatoVazioESemRegras_AcumulaAsDuasViolacoes()
    {
        Result<ConfiguracaoDerivacaoFato> resultado = ConfiguracaoDerivacaoFato.Criar("", []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoObrigatorio,
            ConfiguracaoDerivacaoFatoErrorCodes.SemRegras,
        ]);
    }

    [Fact(DisplayName = "ValidarFormaBasica sem violações retorna lote vazio")]
    public void ValidarFormaBasica_SemViolacoes_Vazio()
    {
        List<FieldError> erros = ConfiguracaoDerivacaoFato.ValidarFormaBasica("MODALIDADE", [(0, "AC")]);

        erros.Should().BeEmpty();
    }

    [Fact(DisplayName = "ValidarFormaBasica detecta ordens duplicadas sem precisar de regras resolvidas")]
    public void ValidarFormaBasica_OrdensDuplicadas_Detecta()
    {
        List<FieldError> erros = ConfiguracaoDerivacaoFato.ValidarFormaBasica("MODALIDADE", [(0, "AC"), (0, "LB_PPI")]);

        erros.Select(e => e.Error.Code).Should().BeEquivalentTo([ConfiguracaoDerivacaoFatoErrorCodes.OrdemRegraDuplicada]);
    }

    [Fact(DisplayName = "ValidarFormaBasica recusa regras com e sem código na mesma derivação")]
    public void ValidarFormaBasica_RegrasComESemCodigo_Recusa()
    {
        List<FieldError> erros = ConfiguracaoDerivacaoFato.ValidarFormaBasica("EGRESSO_ESCOLA_PUBLICA", [(0, null), (1, "SIM")]);

        erros.Select(e => e.Error.Code).Should().BeEquivalentTo([ConfiguracaoDerivacaoFatoErrorCodes.RegrasComESemCodigo]);
    }

    [Fact(DisplayName = "A derivação sem código resolve como booleano: falso quando nenhuma regra ativa")]
    public void ParaRegrasDerivacao_RegrasSemCodigo_ResolveBooleano()
    {
        ConfiguracaoDerivacaoFato config = ConfiguracaoDerivacaoFato.Criar(
            "EGRESSO_ESCOLA_PUBLICA",
            [RegraDerivacaoConfigurada.Criar(0, null, [CondicaoRegraDerivacao.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("INDIGENA")).Value!]).Value!]).Value!;

        RegrasDerivacaoFato regras = config.ParaRegrasDerivacao([]).Value!;
        ResultadoDerivacao resultado = MotorDerivacao.Derivar(
            regras, new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["COR_RACA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement("PRETA")) });

        resultado.ValorBooleano.Should().BeFalse();
    }
}
