namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ReferenciaTemporalFatos"/> (Story #554, issue #892, PR #896) —
/// coerência tudo-ou-nada por variante (N-I01) entre <see cref="ReferenciaTipo"/>,
/// <see cref="ReferenciaTemporalFatos.Data"/> e <see cref="ReferenciaTemporalFatos.FaseId"/>.
/// </summary>
public sealed class ReferenciaTemporalFatosTests
{
    private static readonly DateOnly Data = new(2026, 3, 1);

    private static readonly Guid FaseId = Guid.CreateVersion7();

    [Fact(DisplayName = "Tipo Nenhuma é sempre recusado")]
    public void Criar_TipoNenhuma_Recusa()
    {
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(ReferenciaTipo.Nenhuma, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaTemporalFatos.TipoObrigatorio");
    }

    [Fact(DisplayName = "FIM_INSCRICAO sem data nem fase é aceito (contraprova)")]
    public void Criar_FimInscricaoSemParametros_Aceita()
    {
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimInscricao, null, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Data.Should().BeNull();
        resultado.Value.FaseId.Should().BeNull();
    }

    [Theory(DisplayName = "FIM_INSCRICAO com data e/ou fase é recusado")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Criar_FimInscricaoComParametroIndevido_Recusa(bool comData, bool comFase)
    {
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(
            ReferenciaTipo.FimInscricao, comData ? Data : null, comFase ? FaseId : null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().BeOneOf("ReferenciaTemporalFatos.DataIncoerenteComTipo", "ReferenciaTemporalFatos.FaseIncoerenteComTipo");
    }

    [Theory(DisplayName = "INICIO_FASE/FIM_FASE exigem a fase e proíbem a data")]
    [InlineData(ReferenciaTipo.InicioFase)]
    [InlineData(ReferenciaTipo.FimFase)]
    public void Criar_FaseSemFaseId_Recusa(ReferenciaTipo tipo)
    {
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(tipo, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaTemporalFatos.FaseIncoerenteComTipo");
    }

    [Theory(DisplayName = "INICIO_FASE/FIM_FASE com fase e sem data são aceitos (contraprova)")]
    [InlineData(ReferenciaTipo.InicioFase)]
    [InlineData(ReferenciaTipo.FimFase)]
    public void Criar_FaseComFaseIdSemData_Aceita(ReferenciaTipo tipo)
    {
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(tipo, null, FaseId);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.FaseId.Should().Be(FaseId);
        resultado.Value.Data.Should().BeNull();
    }

    [Theory(DisplayName = "INICIO_FASE/FIM_FASE com data (além da fase) são recusados")]
    [InlineData(ReferenciaTipo.InicioFase)]
    [InlineData(ReferenciaTipo.FimFase)]
    public void Criar_FaseComDataIndevida_Recusa(ReferenciaTipo tipo)
    {
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(tipo, Data, FaseId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaTemporalFatos.DataIncoerenteComTipo");
    }

    [Fact(DisplayName = "DATA_ESPECIFICA exige a data e proíbe a fase")]
    public void Criar_DataEspecificaSemData_Recusa()
    {
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaTemporalFatos.DataIncoerenteComTipo");
    }

    [Fact(DisplayName = "DATA_ESPECIFICA com fase (além da data) é recusada")]
    public void Criar_DataEspecificaComFaseIndevida_Recusa()
    {
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, Data, FaseId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaTemporalFatos.FaseIncoerenteComTipo");
    }

    [Fact(DisplayName = "DATA_ESPECIFICA com data e sem fase é aceita (contraprova)")]
    public void Criar_DataEspecificaComDataSemFase_Aceita()
    {
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, Data, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Data.Should().Be(Data);
        resultado.Value.FaseId.Should().BeNull();
    }
}
