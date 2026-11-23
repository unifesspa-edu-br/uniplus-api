namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using Xunit;

/// <summary>
/// As bancas da fase e as da etapa saem na ordem do tipo e da posição dentro dele, e o que o processo publica depende dessa
/// ordem. Carregá-las em outra ordem não muda a resposta da consulta nem os bytes do documento congelado.
/// </summary>
public sealed class BancasCarregadasForaDeOrdemTests
{
    private const string FaseComVariasBancas = "RESULTADO_PRELIMINAR";

    private static (ProcessoSeletivo NaOrdemDeChegada, ProcessoSeletivo ForaDeOrdem) DoisProcessos()
    {
        ProcessoSeletivo naOrdemDeChegada = CorpusEnvelopeProcessoRico.ProcessoRico(permutar: false);
        ProcessoSeletivo foraDeOrdem = CorpusEnvelopeProcessoRico.ProcessoRico(permutar: true);

        CorpusEnvelopeCronograma.BancasRequeridasDaFaseDeRecurso(permutar: false).Select(static b => b.Id).Should().NotEqual(
            CorpusEnvelopeCronograma.BancasRequeridasDaFaseDeRecurso(permutar: true).Select(static b => b.Id),
            "pré-condição: o segundo processo recebe as bancas requeridas da fase em outra ordem");
        return (naOrdemDeChegada, foraDeOrdem);
    }

    private static byte[] Congelado(ProcessoSeletivo processo) =>
        CorpusEnvelope.Canonicalizador.Canonicalizar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes;

    [Fact(DisplayName = "A fase projeta as bancas requeridas na mesma ordem, qualquer que seja a ordem em que foram carregadas")]
    public void ProjetarFase_BancasRequeridasForaDeOrdem_ProjetaNaMesmaOrdem()
    {
        (ProcessoSeletivo naOrdemDeChegada, ProcessoSeletivo foraDeOrdem) = DoisProcessos();

        IReadOnlyList<BancaRequeridaDto> esperadas = ProjecaoDaFaseDoCronograma.ProjetarCronograma(naOrdemDeChegada)
            .Single(f => f.Codigo == FaseComVariasBancas).BancasRequeridas;
        IReadOnlyList<BancaRequeridaDto> projetadas = ProjecaoDaFaseDoCronograma.ProjetarCronograma(foraDeOrdem)
            .Single(f => f.Codigo == FaseComVariasBancas).BancasRequeridas;

        esperadas.Should().HaveCountGreaterThan(1);
        projetadas.Should().BeEquivalentTo(
            esperadas, opcoes => opcoes.WithStrictOrdering().For(static banca => banca.CandidatosAtendidos).Exclude(static categoria => categoria.Id));
    }

    [Fact(DisplayName = "A etapa projeta as bancas na mesma ordem, qualquer que seja a ordem em que foram carregadas")]
    public void ProjetarEtapas_BancasForaDeOrdem_ProjetaNaMesmaOrdem()
    {
        (ProcessoSeletivo naOrdemDeChegada, ProcessoSeletivo foraDeOrdem) = DoisProcessos();

        IEnumerable<BancaDaEtapaDto> esperadas = ProjecaoDaEtapaDoProcesso.ProjetarEtapas(naOrdemDeChegada).SelectMany(static e => e.Bancas);
        IEnumerable<BancaDaEtapaDto> projetadas = ProjecaoDaEtapaDoProcesso.ProjetarEtapas(foraDeOrdem).SelectMany(static e => e.Bancas);

        esperadas.Should().HaveCountGreaterThan(1);
        projetadas.Should().BeEquivalentTo(esperadas, opcoes => opcoes.WithStrictOrdering());
    }

    [Fact(DisplayName = "O documento congelado tem os mesmos bytes, qualquer que seja a ordem em que as bancas foram carregadas")]
    public void Congelar_BancasForaDeOrdem_GeraOsMesmosBytes()
    {
        (ProcessoSeletivo naOrdemDeChegada, ProcessoSeletivo foraDeOrdem) = DoisProcessos();

        Congelado(foraDeOrdem).Should().Equal(Congelado(naOrdemDeChegada));
    }
}
