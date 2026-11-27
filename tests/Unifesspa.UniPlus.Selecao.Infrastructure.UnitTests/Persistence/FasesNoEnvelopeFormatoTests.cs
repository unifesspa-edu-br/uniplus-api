namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Xunit;

public sealed class FasesNoEnvelopeFormatoTests
{
    [Theory(DisplayName = "Todos os pontos de leitura recusam estrutura inválida no envelope")]
    [InlineData("{\"cronogramaFases\":{\"fases\":[]}}")]
    [InlineData("{\"etapas\":null,\"cronogramaFases\":{\"fases\":[]}}")]
    [InlineData("{\"etapas\":1,\"cronogramaFases\":{\"fases\":[]}}")]
    [InlineData("{\"etapas\":[],\"cronogramaFases\":{}}")]
    [InlineData("{\"etapas\":[],\"cronogramaFases\":{\"fases\":null}}")]
    [InlineData("{\"etapas\":[],\"cronogramaFases\":{\"fases\":\"inválido\"}}")]
    [InlineData("{\"etapas\":[]}")]
    [InlineData("{\"etapas\":[],\"cronogramaFases\":null}")]
    [InlineData("{\"etapas\":[],\"cronogramaFases\":1}")]
    [InlineData("{\"etapas\":[1],\"cronogramaFases\":{\"fases\":[]}}")]
    [InlineData("{\"etapas\":[],\"cronogramaFases\":{\"fases\":[1]}}")]
    [InlineData("{\"etapas\":[{\"nome\":\"Etapa\"}],\"cronogramaFases\":{\"fases\":[]}}")]
    [InlineData("{\"etapas\":[],\"cronogramaFases\":{\"fases\":[{}]}}")]
    [InlineData("{\"etapas\":[{\"id\":\"11111111-1111-4111-8111-111111111111\",\"nome\":\"  \"}],\"cronogramaFases\":{\"fases\":[]}}")]
    [InlineData("{\"etapas\":[{\"id\":\"11111111-1111-4111-8111-111111111111\",\"nome\":\"Etapa\",\"faseCodigo\":1}],\"cronogramaFases\":{\"fases\":[]}}")]
    [InlineData("{\"etapas\":[],\"cronogramaFases\":{\"fases\":[{\"id\":\"11111111-1111-4111-8111-111111111111\",\"tipoFaseOrigemId\":\"22222222-2222-4222-8222-222222222222\",\"codigo\":\" \"}]}}")]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    public void Leituras_EnvelopeInvalido_LancamExcecao(string envelope)
    {
        Guid id = Guid.Parse("11111111-1111-4111-8111-111111111111");
        Action das = () => FasesNoEnvelope.Das(envelope);
        Action daFase = () => FasesNoEnvelope.DaFase(envelope, id);
        Action daEtapa = () => FasesNoEnvelope.DaEtapa(envelope, id);

        das.Should().Throw<PayloadForaDoPerfilCanonicoException>();
        daFase.Should().Throw<PayloadForaDoPerfilCanonicoException>();
        daEtapa.Should().Throw<PayloadForaDoPerfilCanonicoException>();
    }

    [Fact(DisplayName = "Listas vazias são válidas nos três pontos de leitura")]
    public void Leituras_ListasVazias_RetornamAusencia()
    {
        const string envelope = """{"etapas":[],"cronogramaFases":{"fases":[]}}""";
        Guid id = Guid.Parse("11111111-1111-4111-8111-111111111111");

        FasesNoEnvelope.Das(envelope).Should().BeEmpty();
        FasesNoEnvelope.DaFase(envelope, id).Should().BeNull();
        FasesNoEnvelope.DaEtapa(envelope, id).Should().BeNull();
    }

    [Fact(DisplayName = "Texto nulo indica ausência de versão vigente")]
    public void Leituras_TextoNulo_RetornamAusencia()
    {
        Guid id = Guid.Parse("11111111-1111-4111-8111-111111111111");

        FasesNoEnvelope.Das(null).Should().BeEmpty();
        FasesNoEnvelope.DaFase(null, id).Should().BeNull();
        FasesNoEnvelope.DaEtapa(null, id).Should().BeNull();
    }

    [Fact(DisplayName = "A unidade é encontrada pela fase e pela etapa associada")]
    public void Leituras_FaseEEtapaValidas_RetornamUnidade()
    {
        const string envelope = """
            {"etapas":[{"id":"11111111-1111-4111-8111-111111111111","nome":"Etapa","faseCodigo":"AVALIACAO"}],
             "cronogramaFases":{"fases":[{"id":"22222222-2222-4222-8222-222222222222",
             "tipoFaseOrigemId":"33333333-3333-4333-8333-333333333333","codigo":"AVALIACAO","tipoFaseNome":"Avaliação","coletaInscricao":false,
             "unidadeResponsavel":{"origemId":"44444444-4444-4444-8444-444444444444","codigo":"U","sigla":"U","nome":"Unidade"}}]}}
            """;
        Guid fase = Guid.Parse("22222222-2222-4222-8222-222222222222");
        Guid etapa = Guid.Parse("11111111-1111-4111-8111-111111111111");
        Guid unidade = Guid.Parse("44444444-4444-4444-8444-444444444444");

        FasesNoEnvelope.DaFase(envelope, fase)!.UnidadeId.Should().Be(unidade);
        FasesNoEnvelope.DaEtapa(envelope, etapa)!.UnidadeId.Should().Be(unidade);
    }
}
