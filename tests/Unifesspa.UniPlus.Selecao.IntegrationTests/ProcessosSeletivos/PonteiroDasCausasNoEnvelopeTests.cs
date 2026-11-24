namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Chaves;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDaPoliticaDeOrdenacao;

/// <summary>
/// O ponteiro que a causa entrega ao cliente aponta, no documento congelado, o mesmo item que a causa cita: a posição que o domínio
/// calcula pela ordem canônica é a posição que o envelope grava, mesmo quando as fases e as etapas foram declaradas fora de ordem.
/// </summary>
public sealed class PonteiroDasCausasNoEnvelopeTests
{
    [Fact(DisplayName = "O ponteiro da fase tem a posição que a fase ocupa no envelope congelado, com as fases declaradas fora de ordem")]
    public void PonteiroDaFase_TemAPosicaoDaFaseNoEnvelope()
    {
        FaseCronograma[] fases = [Fase("RECURSO", 3), Fase("INSCRICAO", 1), Fase("RESULTADO", 2)];
        ProcessoSeletivo processo = Montar(cronogramaFases: fases, etapas: [Etapa("Prova", "INSCRICAO", 1)]);
        JsonArray fasesDoEnvelope = Envelope(processo)["cronogramaFases"]!["fases"]!.AsArray();

        foreach (FaseCronograma fase in OrdemCanonicaDaConfiguracao.Fases(processo.CronogramaFases))
        {
            int posicao = OrdemCanonicaDaConfiguracao.Fases(processo.CronogramaFases).ToList().IndexOf(fase);
            string ponteiro = PonteiroNoDocumento.Converter($"fases[{posicao}].unidadeResponsavel", OrigemNoDocumento.Identidade)!;

            ponteiro.Should().Be($"/cronograma/fases/{posicao}/unidadeResponsavelCodigo");
            fasesDoEnvelope[posicao]!["tipoFaseOrigemId"]!.GetValue<Guid>().Should().Be(fase.FaseCanonicaOrigemId);
        }
    }

    [Fact(DisplayName = "O ponteiro da etapa tem a posição que a etapa ocupa no envelope congelado, com as etapas declaradas fora de ordem")]
    public void PonteiroDaEtapa_TemAPosicaoDaEtapaNoEnvelope()
    {
        EtapaProcesso[] etapas = [Etapa("Redação", "INSCRICAO", 3), Etapa("Análise documental", "INSCRICAO", 1), Etapa("Prova", "INSCRICAO", 2)];
        ProcessoSeletivo processo = Montar(etapas: etapas, cronogramaFases: [Fase("INSCRICAO", 1)]);
        JsonArray etapasDoEnvelope = Envelope(processo)["etapas"]!.AsArray();

        foreach (EtapaProcesso etapa in etapas)
        {
            int posicao = OrdemCanonicaDaConfiguracao.Etapas(processo.Etapas).ToList().IndexOf(etapa);

            PonteiroNoDocumento.Converter($"etapas[{posicao}].id", OrigemNoDocumento.Identidade).Should().Be($"/etapas/{posicao}/chave");
            etapasDoEnvelope[posicao]!["nome"]!.GetValue<string>().Should().Be(etapa.Nome);
        }
    }

    private static JsonObject Envelope(ProcessoSeletivo processo) =>
        ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)));

    private static FaseCronograma Fase(string codigo, int ordem) => FaseCronograma.Criar(FormacaoDeChaveLocal.Formar("fase", codigo, new ChavesEmUso()),
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: true, ColetaInscricao: true, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            ordem, new DateTimeOffset(2026, 1, ordem, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, ordem, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static EtapaProcesso Etapa(string nome, string faseCodigo, int ordem) => EtapaProcesso.Criar(FormacaoDeChaveLocal.Formar("etapa", nome, new ChavesEmUso()),
        new IdentificacaoDaEtapa(
            Nome: nome, Carater: CaraterEtapa.Classificatoria,
            TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            Ordem: ordem, FaseCodigo: faseCodigo),
        new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
}
