namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class EtapaNosParametrosGravadosTests
{
    [Fact]
    public void Parametros_ComEtapa_GravamEtapaIdEVoltamSemPerda()
    {
        Guid etapaId = Guid.NewGuid();
        Conferir(typeof(CriterioDesempate), new ParametrosDesempateMaiorNotaEtapa(etapaId), etapaId);
        Conferir(typeof(RegraEliminacao), new ParametrosEliminacaoPorNotaMinimaNaEtapa(etapaId, 5), etapaId);
        Conferir(typeof(RegraEliminacao), new ParametrosEliminacaoPorAusenciaNaEtapa(etapaId), etapaId);
    }

    private static void Conferir(Type entidade, object parametros, Guid etapaId)
    {
        ValueConverter conversor = ModeloDaSelecao.Propriedade(entidade, "Parametros").GetValueConverter()!;
        string gravado = (string)conversor.ConvertToProvider(parametros)!;
        using JsonDocument documento = JsonDocument.Parse(gravado);
        documento.RootElement.GetProperty("etapaId").GetGuid().Should().Be(etapaId);
        documento.RootElement.TryGetProperty("etapaRef", out _).Should().BeFalse();
        conversor.ConvertFromProvider(gravado).Should().Be(parametros);
    }
}
