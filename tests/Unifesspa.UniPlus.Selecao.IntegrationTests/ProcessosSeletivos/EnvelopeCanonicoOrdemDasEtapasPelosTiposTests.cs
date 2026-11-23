namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.EnvelopeDeReferencia;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.NormalizadorDeIdsDoEnvelope;

/// <summary>
/// A ordem declarada das etapas independe da ordem de criação e da entrada da lista;
/// os tipos de decisão de cada etapa são ordenados pelo código.
/// </summary>
public sealed class EnvelopeCanonicoOrdemDasEtapasPelosTiposTests
{
    private const string Classificacao = "CLASSIFICACAO";
    private const string AnaliseDocumental = "ANALISE_DOCUMENTAL";

    [Fact(DisplayName = "Etapas que só diferem pelos tipos: a ordem de criação não muda os bytes")]
    public void EtapasQueSoDiferemPelosTipos_OrdemDeCriacaoInvertida_BytesIguais()
    {
        EtapaProcesso classificacaoCriadaPrimeiro = Etapa(Classificacao);
        EtapaProcesso analiseCriadaDepois = Etapa(AnaliseDocumental);
        EtapaProcesso analiseCriadaPrimeiro = Etapa(AnaliseDocumental);
        EtapaProcesso classificacaoCriadaDepois = Etapa(Classificacao);

        string primeira = Envelope(classificacaoCriadaPrimeiro, analiseCriadaDepois);
        string segunda = Envelope(classificacaoCriadaDepois, analiseCriadaPrimeiro);

        segunda.Should().Be(primeira);
    }

    [Fact(DisplayName = "Etapas que só diferem pelos tipos: a de ANALISE_DOCUMENTAL vem primeiro")]
    public void EtapasQueSoDiferemPelosTipos_AnaliseDocumentalVemPrimeiro()
    {
        string envelope = Envelope(Etapa(Classificacao), Etapa(AnaliseDocumental));

        envelope.IndexOf($"\"{AnaliseDocumental}\"", StringComparison.Ordinal)
            .Should().BePositive()
            .And.BeLessThan(envelope.IndexOf($"\"{Classificacao}\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Etapas que só diferem pelos tipos: a permutação da lista não muda os bytes nem o hash")]
    public void EtapasQueSoDiferemPelosTipos_PermutacaoDaLista_BytesEHashIguais()
    {
        (EtapaProcesso analiseNaLista, EtapaProcesso classificacaoNaLista) = (Etapa(AnaliseDocumental), Etapa(Classificacao));
        (EtapaProcesso analiseNaListaInvertida, EtapaProcesso classificacaoNaListaInvertida) = (Etapa(AnaliseDocumental), Etapa(Classificacao));

        string direta = Envelope(analiseNaLista, classificacaoNaLista);
        string invertida = Envelope(classificacaoNaListaInvertida, analiseNaListaInvertida);

        invertida.Should().Be(direta);
        HashCanonicalComputer.ComputeSha256Hex(Encoding.UTF8.GetBytes(invertida))
            .Should().Be(HashCanonicalComputer.ComputeSha256Hex(Encoding.UTF8.GetBytes(direta)));
    }

    [Fact(DisplayName = "Tipos declarados em ordens diferentes na mesma etapa: bytes iguais")]
    public void TiposDeclaradosEmOrdensDiferentes_BytesIguais()
    {
        string alfabetica = Envelope(Etapa(AnaliseDocumental, Classificacao));
        string invertida = Envelope(Etapa(Classificacao, AnaliseDocumental));

        invertida.Should().Be(alfabetica);
    }

    private static string Envelope(params EtapaProcesso[] etapas) =>
        NormalizarIds(Canonicalizar(etapas).Bytes);

    private static SnapshotCanonico Canonicalizar(params EtapaProcesso[] etapas)
    {
        (ProcessoSeletivo processo, _) = ApoioDoEnvelopeCodec.ProcessoDeInscricao("PS Tipos");
        Result definicao = processo.DefinirEtapas(etapas);
        definicao.IsSuccess.Should().BeTrue(definicao.Error?.Message);
        processo.DefinirOpcoesDeCurso(1).IsSuccess.Should().BeTrue();

        return Canonicalizer.Canonicalizar(EntradaDeReferencia(
            processo, DadosDeReferencia(), valoresSelecionaveis: CatalogoDoConjuntoBasico.ComValoresCongelados()));
    }

    private static EtapaProcesso Etapa(params string[] tipos)
    {
        bool analise = tipos.Contains(AnaliseDocumental, StringComparer.Ordinal);
        EtapaProcesso etapa = EtapaProcesso.Criar(ChaveLocal.Criar(analise ? "etapa_analise_documental" : "etapa_classificacao").Value!,
            new IdentificacaoDaEtapa(
                Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, Ordem: analise ? 1 : 2, FaseCodigo: "INSCRICAO",
                TipoEtapa: TipoEtapaSnapshot.Criar(
                    new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva",
                    admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!),
            new DeclaracaoDaAvaliacaoDaEtapa(
                Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null,
                RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirTiposDeDecisao(tipos).IsSuccess.Should().BeTrue();

        return etapa;
    }
}
