namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="IdadeMaximaEmissao"/> (Story #554, PR-d, issue #893) — CA-08:
/// coerência tudo-nulo OU completo, <c>DATA_SUBMISSAO</c> aceita (diferente de
/// <see cref="ReferenciaTemporalFatos"/>, PR-b), âncoras de fase/data.
/// </summary>
public sealed class IdadeMaximaEmissaoTests
{
    private static readonly Guid FaseId = Guid.CreateVersion7();

    private static readonly DateOnly Data = new(2026, 3, 1);

    [Fact(DisplayName = "CA-08: tudo-nulo é aceito — regra ausente é estado válido")]
    public void Criar_TudoNulo_Aceita()
    {
        Result<IdadeMaximaEmissao?> resultado = IdadeMaximaEmissao.Criar(null, null, null, null, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact(DisplayName = "CA-08: completo com DATA_SUBMISSAO é aceito (contraprova da exclusão da PR-b)")]
    public void Criar_CompletoComDataSubmissao_Aceita()
    {
        Result<IdadeMaximaEmissao?> resultado = IdadeMaximaEmissao.Criar(
            90, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.DataSubmissao, null, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBeNull();
        resultado.Value!.Valor.Should().Be(90);
        resultado.Value.Unidade.Should().Be(UnidadeIdade.Dias);
        resultado.Value.ReferenciaTipo.Should().Be(ReferenciaTipoIdadeEmissao.DataSubmissao);
    }

    [Theory(DisplayName = "CA-08: parcial (algum dos 3 primeiros campos ausente) é recusado")]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    public void Criar_Parcial_Falha(bool comValor, bool comUnidade, bool comReferenciaTipo)
    {
        Result<IdadeMaximaEmissao?> resultado = IdadeMaximaEmissao.Criar(
            comValor ? 90 : null,
            comUnidade ? UnidadeIdade.Dias : null,
            comReferenciaTipo ? ReferenciaTipoIdadeEmissao.DataSubmissao : null,
            null,
            null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.CamposIncompletos");
    }

    [Fact(DisplayName = "Valor zero é recusado")]
    public void Criar_ValorZero_Recusa()
    {
        Result<IdadeMaximaEmissao?> resultado = IdadeMaximaEmissao.Criar(
            0, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.DataSubmissao, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.ValorInvalido");
    }

    [Fact(DisplayName = "Valor negativo é recusado")]
    public void Criar_ValorNegativo_Recusa()
    {
        Result<IdadeMaximaEmissao?> resultado = IdadeMaximaEmissao.Criar(
            -1, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.DataSubmissao, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.ValorInvalido");
    }

    [Fact(DisplayName = "Data ou fase presentes com os 3 primeiros campos ausentes é recusado")]
    public void Criar_DataOuFaseComTudoAusente_Recusa()
    {
        IdadeMaximaEmissao.Criar(null, null, null, Data, null).IsFailure.Should().BeTrue();
        IdadeMaximaEmissao.Criar(null, null, null, Data, null).Error!.Code.Should().Be("IdadeMaximaEmissao.CamposIncoerentesComAusencia");

        IdadeMaximaEmissao.Criar(null, null, null, null, FaseId).IsFailure.Should().BeTrue();
        IdadeMaximaEmissao.Criar(null, null, null, null, FaseId).Error!.Code.Should().Be("IdadeMaximaEmissao.CamposIncoerentesComAusencia");
    }

    [Theory(DisplayName = "CA-08: âncoras de fase exigem ReferenciaFaseId")]
    [InlineData(ReferenciaTipoIdadeEmissao.InicioFase)]
    [InlineData(ReferenciaTipoIdadeEmissao.FimFase)]
    public void AncoraFaseSemExtremo_Falha(ReferenciaTipoIdadeEmissao tipo)
    {
        Result<IdadeMaximaEmissao?> resultado = IdadeMaximaEmissao.Criar(90, UnidadeIdade.Dias, tipo, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseIncoerenteComTipo");
    }

    [Theory(DisplayName = "Âncoras de fase com ReferenciaFaseId presente e sem Data são aceitas (contraprova)")]
    [InlineData(ReferenciaTipoIdadeEmissao.InicioFase)]
    [InlineData(ReferenciaTipoIdadeEmissao.FimFase)]
    public void AncoraFaseComFaseId_Aceita(ReferenciaTipoIdadeEmissao tipo)
    {
        Result<IdadeMaximaEmissao?> resultado = IdadeMaximaEmissao.Criar(90, UnidadeIdade.Dias, tipo, null, FaseId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.ReferenciaFaseId.Should().Be(FaseId);
        resultado.Value.Data.Should().BeNull();
    }

    [Theory(DisplayName = "Âncoras de fase com Data (além da fase) são recusadas")]
    [InlineData(ReferenciaTipoIdadeEmissao.InicioFase)]
    [InlineData(ReferenciaTipoIdadeEmissao.FimFase)]
    public void AncoraFaseComDataIndevida_Recusa(ReferenciaTipoIdadeEmissao tipo)
    {
        Result<IdadeMaximaEmissao?> resultado = IdadeMaximaEmissao.Criar(90, UnidadeIdade.Dias, tipo, Data, FaseId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.DataIncoerenteComTipo");
    }

    [Fact(DisplayName = "DATA_ESPECIFICA exige Data e proíbe fase âncora")]
    public void DataEspecificaSemData_Recusa()
    {
        Result<IdadeMaximaEmissao?> resultado = IdadeMaximaEmissao.Criar(
            90, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.DataEspecifica, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.DataIncoerenteComTipo");
    }

    [Fact(DisplayName = "DATA_ESPECIFICA com Data e sem fase é aceita (contraprova)")]
    public void DataEspecificaComData_Aceita()
    {
        Result<IdadeMaximaEmissao?> resultado = IdadeMaximaEmissao.Criar(
            90, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.DataEspecifica, Data, null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Data.Should().Be(Data);
    }

    [Fact(DisplayName = "DATA_ESPECIFICA com fase (além da data) é recusada")]
    public void DataEspecificaComFaseIndevida_Recusa()
    {
        Result<IdadeMaximaEmissao?> resultado = IdadeMaximaEmissao.Criar(
            90, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.DataEspecifica, Data, FaseId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseIncoerenteComTipo");
    }

    [Theory(DisplayName = "FIM_INSCRICAO/DATA_SUBMISSAO não usam Data nem fase — ambos presentes são recusados")]
    [InlineData(ReferenciaTipoIdadeEmissao.FimInscricao)]
    [InlineData(ReferenciaTipoIdadeEmissao.DataSubmissao)]
    public void FimInscricaoOuDataSubmissao_ComCampoIndevido_Recusa(ReferenciaTipoIdadeEmissao tipo)
    {
        IdadeMaximaEmissao.Criar(90, UnidadeIdade.Dias, tipo, Data, null).IsFailure.Should().BeTrue();
        IdadeMaximaEmissao.Criar(90, UnidadeIdade.Dias, tipo, null, FaseId).IsFailure.Should().BeTrue();
    }

    [Theory(DisplayName = "FIM_INSCRICAO/DATA_SUBMISSAO sem Data nem fase são aceitos (contraprova)")]
    [InlineData(ReferenciaTipoIdadeEmissao.FimInscricao)]
    [InlineData(ReferenciaTipoIdadeEmissao.DataSubmissao)]
    public void FimInscricaoOuDataSubmissao_SemCamposOpcionais_Aceita(ReferenciaTipoIdadeEmissao tipo) =>
        IdadeMaximaEmissao.Criar(90, UnidadeIdade.Dias, tipo, null, null).IsSuccess.Should().BeTrue();
}
