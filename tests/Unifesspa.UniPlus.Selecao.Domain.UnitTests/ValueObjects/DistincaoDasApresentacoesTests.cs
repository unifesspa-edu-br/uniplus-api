namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DistincaoDasApresentacoesTests
{
    [Theory]
    [InlineData(ChaveDistincao.CompetenciaMensal, "2026-02", "2026-01")]
    [InlineData(ChaveDistincao.ExercicioAnual, "2026", "2025")]
    public void AtendidaPor_CalendarioExigeCadaPeriodo_RecusaPeriodoRepetido(ChaveDistincao chave, string recente, string anterior)
    {
        DistincaoDasApresentacoes distincao = DistincaoDasApresentacoes.Declarar(chave, new DateOnly(2026, 2, 1), null, 2).Value!;
        ApresentacaoDocumento primeira = new(Guid.CreateVersion7(), recente);
        ApresentacaoDocumento repetida = new(Guid.CreateVersion7(), recente);

        distincao.AtendidaPor([primeira, repetida], 2).Should().BeFalse();
        distincao.AtendidaPor([primeira, new(Guid.CreateVersion7(), anterior)], 2).Should().BeTrue();
        distincao.ConferivelPelaContagem.Should().BeFalse();
    }

    [Fact]
    public void AtendidaPor_OcorrenciaSemLista_IgnoraChavesVaziasEIdentidadesRepetidas()
    {
        DistincaoDasApresentacoes distincao = DistincaoDasApresentacoes.Declarar(ChaveDistincao.Ocorrencia, null, null, 2).Value!;
        Guid id = Guid.CreateVersion7();

        distincao.AtendidaPor([new(id, "a"), new(id, "b"), new(Guid.CreateVersion7(), " ")], 2).Should().BeFalse();
        distincao.AtendidaPor([new(id, "a"), new(Guid.CreateVersion7(), "b")], 2).Should().BeTrue();
        distincao.ConferivelPelaContagem.Should().BeTrue();
    }

    [Fact]
    public void Reidratar_ListaOriginalMutada_ConservaOcorrencias()
    {
        List<string> ocorrencias = ["a", "b"];
        DistincaoDasApresentacoes distincao = DistincaoDasApresentacoes.Reidratar(ChaveDistincao.Ocorrencia, null, ocorrencias)!;
        ocorrencias.Clear();

        distincao.OcorrenciasEsperadas.Should().Equal("a", "b");
        distincao.OcorrenciasEsperadas.Should().NotBeAssignableTo<List<string>>();
    }

    [Fact]
    public void Reidratar_FolhaSemChaveComMetadados_ConservaValoresSemRevalidar()
    {
        DateOnly referencia = new(2026, 2, 1);
        DistincaoDasApresentacoes distincao = DistincaoDasApresentacoes.Reidratar(null, referencia, ["a"])!;

        distincao.Chave.Should().BeNull();
        distincao.DataReferencia.Should().Be(referencia);
        distincao.OcorrenciasEsperadas.Should().Equal("a");
        distincao.IdentificacoesDasApresentacoesExigidas(1).Should().BeNull();
        distincao.AtendidaPor([new(Guid.CreateVersion7())], 1).Should().BeTrue();
    }
}
