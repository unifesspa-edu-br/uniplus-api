namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DataDeReferenciaDoDocumentoTests
{
    [Theory]
    [InlineData(TipoDaDataDeReferencia.FimDasInscricoes)]
    [InlineData(TipoDaDataDeReferencia.DataDaApresentacao)]
    public void Criar_SemDetalhes_Aceita(TipoDaDataDeReferencia tipo) =>
        DataDeReferenciaDoDocumento.Criar(tipo, null, null).IsSuccess.Should().BeTrue();

    [Fact]
    public void Criar_TipoInvalido_Recusa() =>
        DataDeReferenciaDoDocumento.Criar((TipoDaDataDeReferencia)99, null, null).Error!.Code
            .Should().Be(DataDeReferenciaDoDocumentoErrorCodes.TipoInvalido);

    [Fact]
    public void Criar_DataIncoerente_Recusa() =>
        DataDeReferenciaDoDocumento.Criar(TipoDaDataDeReferencia.DataEspecifica, null, null).Error!.Code
            .Should().Be(DataDeReferenciaDoDocumentoErrorCodes.DataIncoerenteComTipo);

    [Fact]
    public void Criar_FaseIncoerente_Recusa() =>
        DataDeReferenciaDoDocumento.Criar(TipoDaDataDeReferencia.FimDaFase, null, null).Error!.Code
            .Should().Be(DataDeReferenciaDoDocumentoErrorCodes.FaseIncoerenteComTipo);

    [Fact]
    public void DataNoCronograma_Inscricoes_DevolveMaiorTerminoNoFuso()
    {
        FaseCronograma primeira = FaseParaReferenciaBuilder.Criar(true, null, DateTimeOffset.Parse("2026-03-01T01:00:00Z", CultureInfo.InvariantCulture));
        FaseCronograma ultima = FaseParaReferenciaBuilder.Criar(true, null, DateTimeOffset.Parse("2026-03-04T01:00:00Z", CultureInfo.InvariantCulture));
        FaseCronograma outra = FaseParaReferenciaBuilder.Criar(false, null, DateTimeOffset.Parse("2026-03-08T01:00:00Z", CultureInfo.InvariantCulture));
        DataDeReferenciaDoDocumento referencia = DataDeReferenciaDoDocumento.Criar(TipoDaDataDeReferencia.FimDasInscricoes, null, null).Value!;
        TimeZoneInfo fuso = TimeZoneInfo.CreateCustomTimeZone("teste", TimeSpan.FromHours(-3), "teste", "teste");
        referencia.DataNoCronograma([primeira, ultima, outra], fuso).Should().Be(new DateOnly(2026, 3, 3));
    }

    [Fact]
    public void DataNoCronograma_Fase_UsaIdentidadeEExtremo()
    {
        FaseCronograma fase = FaseParaReferenciaBuilder.Criar(false, DateTimeOffset.Parse("2026-03-01T12:00:00Z", CultureInfo.InvariantCulture), null);
        DataDeReferenciaDoDocumento referencia = DataDeReferenciaDoDocumento.Criar(TipoDaDataDeReferencia.InicioDaFase, null, fase.Id).Value!;
        referencia.UsaAFase(fase.Id).Should().BeTrue();
        referencia.UsaAFase(Guid.NewGuid()).Should().BeFalse();
        referencia.DataNoCronograma([fase], TimeZoneInfo.Utc).Should().Be(new DateOnly(2026, 3, 1));
        referencia.DataNoCronograma([], TimeZoneInfo.Utc).Should().BeNull();
    }

    [Fact]
    public void DataNoCronograma_DataEspecifica_DevolveData()
    {
        DateOnly data = new DateOnly(2026, 3, 1);
        DataDeReferenciaDoDocumento referencia = DataDeReferenciaDoDocumento.Criar(TipoDaDataDeReferencia.DataEspecifica, data, null).Value!;
        referencia.DataNoCronograma([], TimeZoneInfo.Utc).Should().Be(data);
    }

    [Fact]
    public void DataNoCronograma_JanelasCongeladas_ResolvemComoAsFases()
    {
        PeriodoDaFaseCongelada inscricao = new(Guid.NewGuid(), true, null, DateTimeOffset.Parse("2026-03-04T01:00:00Z", CultureInfo.InvariantCulture));
        PeriodoDaFaseCongelada recurso = new(Guid.NewGuid(), false, DateTimeOffset.Parse("2026-03-08T01:00:00Z", CultureInfo.InvariantCulture), null);
        TimeZoneInfo fuso = TimeZoneInfo.CreateCustomTimeZone("teste", TimeSpan.FromHours(-3), "teste", "teste");
        DataDeReferenciaDoDocumento fimDasInscricoes = new DataDeReferenciaBuilder().Construir();
        DataDeReferenciaDoDocumento inicioDoRecurso = new DataDeReferenciaBuilder().NoInicioDaFase(recurso.Id).Construir();

        fimDasInscricoes.DataNoCronograma([inscricao, recurso], fuso).Should().Be(new DateOnly(2026, 3, 3));
        inicioDoRecurso.DataNoCronograma([inscricao, recurso], fuso).Should().Be(new DateOnly(2026, 3, 7));
    }
}
