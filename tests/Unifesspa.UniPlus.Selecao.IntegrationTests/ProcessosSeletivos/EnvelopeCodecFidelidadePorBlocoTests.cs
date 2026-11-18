namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class EnvelopeCodecFidelidadePorBlocoTests
{
    // Identidade técnica e vínculos internos são repostos; não são o conteúdo congelado.
    private static readonly string[] MetadadosInternos =
    [
        "Id", "CreatedAt", "UpdatedAt", "DomainEvents", "ProcessoSeletivoId", "EtapaProcessoId", "FaseCronogramaId",
        "AtendimentoEspecializadoId", "ConfiguracaoDistribuicaoVagasId", "ConfiguracaoBonusRegionalId", "ConfiguracaoClassificacaoId",
        "ConfiguracaoCascataRemanejamentoId", "GrupoPesoAreaEnemCongeladoId", "BancaRequeridaId", "NoExigenciaId",
        "GrupoColetadoId", "FormularioProcessoId", "FatoColetadoId", "ConfiguracaoDerivacaoFatoId", "RegraDerivacaoConfiguradaId",
    ];

    [Fact]
    public void Reidratar_Cronograma_ConservaDeclaracao()
    {
        (ConfiguracaoDeclarada d, ConfiguracaoPublicada g) = Ler();

        Conferir(d.Cronograma, new { g.Cronograma.Localidade, g.Cronograma.AlgoritmoContagemPrazo, g.Cronograma.CronogramaFases });
        g.Cronograma.CronogramaFases.Select(static f => f.Codigo).Should().Equal(d.Cronograma.CronogramaFases.OrderBy(static f => f.Ordem).Select(static f => f.Codigo));
        g.IdentificadorLegivel.Should().Be(d.Identidade.IdentificadorLegivel);
    }

    [Fact]
    public void Reidratar_Avaliacao_ConservaDeclaracao()
    {
        (ConfiguracaoDeclarada d, ConfiguracaoPublicada g) = Ler();

        Conferir(d.Avaliacao, new { g.Avaliacao.Etapas, g.Avaliacao.CriteriosDesempate, g.Avaliacao.Classificacao });
        g.Avaliacao.Etapas.Select(static e => e.Ordem).Should().Equal(d.Avaliacao.Etapas.OrderBy(static e => e.Ordem).Select(static e => e.Ordem));
    }

    [Fact]
    public void Reidratar_Vagas_ConservaDeclaracao()
    {
        (ConfiguracaoDeclarada d, ConfiguracaoPublicada g) = Ler();

        Conferir(new { d.Vagas.DistribuicaoVagas, d.Vagas.NumeroDeOpcoesDeCurso, d.Vagas.BonusRegional, d.Vagas.Cascata, d.Vagas.AtendimentoEspecializado },
            new { g.Vagas.DistribuicaoVagas, g.Vagas.NumeroDeOpcoesDeCurso, g.Vagas.BonusRegional, Cascata = g.Vagas.Cascata, g.Vagas.AtendimentoEspecializado });
    }

    [Fact]
    public void Reidratar_Exigencias_ConservaDeclaracaoEIdentidadesCongeladas()
    {
        (ConfiguracaoDeclarada d, ConfiguracaoPublicada g) = Ler();

        Conferir(d.Exigencias, new { g.Exigencias.DocumentosExigidos, g.Exigencias.NosExigencia });
        g.Exigencias.NosExigencia.Select(static n => (n.Id, n.DocumentoExigidoId, n.NoPaiId)).Should()
            .BeEquivalentTo(d.Exigencias.NosExigencia.Select(static n => (n.Id, n.DocumentoExigidoId, n.NoPaiId)));
        g.Exigencias.DocumentosExigidos.Select(static e => e.Id).Should().BeEquivalentTo(d.Exigencias.DocumentosExigidos.Select(static e => e.Id));
    }

    [Fact]
    public void Reidratar_Coleta_ConservaCamposGruposEFormularios()
    {
        (ConfiguracaoDeclarada d, ConfiguracaoPublicada g) = Ler();

        Conferir(new { Campos = d.Coleta.Itens, d.Coleta.GruposColetados, d.Coleta.Formularios, d.Coleta.TermosExigidos },
            new { Campos = g.Coleta.Itens, g.Coleta.GruposColetados, g.Coleta.Formularios, g.Coleta.TermosExigidos });
        Conferir(new { d.Coleta.OpcoesDeclaradas, d.Coleta.RegrasDerivacao, d.Coleta.ReferenciaTemporalFatos },
            new { g.Coleta.OpcoesDeclaradas, g.Coleta.RegrasDerivacao, g.Coleta.ReferenciaTemporalFatos });
        g.Coleta.Itens.Select(static f => f.FatoCodigo).Should().Equal(d.Coleta.Itens.OrderBy(static f => f.Ordem).Select(static f => f.FatoCodigo));
    }

    [Fact]
    public void Reidratar_DivulgacaoETaxa_ConservaDeclaracao()
    {
        (ConfiguracaoDeclarada d, ConfiguracaoPublicada g) = Ler();

        Conferir(d.DivulgacaoETaxa, new { g.DivulgacaoETaxa.ConfiguracaoDivulgacao, g.DivulgacaoETaxa.ConfiguracaoTaxaInscricao });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void DerivacoesEfetivas_ComOuSemOferta_ConservaRegras(int indice)
    {
        (ConfiguracaoDeclarada d, ConfiguracaoPublicada g) = Ler();
        Guid[] ofertas = indice < 0 ? [] : [d.Vagas.DistribuicaoVagas[indice].OfertaCursoOrigemId];
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicoes = DerivacoesDaOferta.DistribuicaoDasOfertas(d.Vagas.DistribuicaoVagas, ofertas);

        Conferir(DerivacoesDaOferta.Efetivas(d.Coleta.RegrasDerivacao, distribuicoes), g.DerivacoesEfetivas(ofertas));
    }

    private static (ConfiguracaoDeclarada Declarada, ConfiguracaoPublicada Lida) Ler()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico(comArvoreSatisfacao: true);
        SnapshotCanonico documento = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        ConfiguracaoPublicada lida = ApoioDoEnvelopeCodec.Reidratado(CorpusEnvelopeEntrada.VersaoDeAbertura(processo, documento.Bytes)).Configuracao;
        return (processo.Declaracao(), lida);
    }

    private static void Conferir(object esperado, object lido) =>
        lido.Should().BeEquivalentTo(esperado, opcoes => opcoes.ExcludingMembersNamed(MetadadosInternos).AllowingInfiniteRecursion()
            .Using<JsonElement>(comparacao => JsonElement.DeepEquals(comparacao.Subject, comparacao.Expectation).Should().BeTrue()).WhenTypeIs<JsonElement>()
            .Using<string>(comparacao => (comparacao.Subject?.Normalize()).Should().Be(comparacao.Expectation?.Normalize())).WhenTypeIs<string>());
}
