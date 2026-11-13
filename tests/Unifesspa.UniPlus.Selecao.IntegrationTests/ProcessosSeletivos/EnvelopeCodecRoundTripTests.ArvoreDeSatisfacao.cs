namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

public sealed partial class EnvelopeCodecRoundTripTests
{
    /// <summary>
    /// Prova de fidelidade do codec sobre a MESMA exigência documental rica que a golden
    /// fixture do canonicalizador congela
    /// (<see cref="EnvelopeCanonicoGoldenTests.ProcessoDeReferencia"/>), incluindo o bloco
    /// <c>metadadosFatos</c> (Story #919).
    /// </summary>
    /// <remarks>
    /// Sem arquivo congelado — diferente da golden 1.1 acima, que compara contra bytes
    /// FIXOS gravados no repositório: <see cref="EnvelopeCanonicoGoldenTests.ProcessoDeReferencia"/>
    /// usa <c>Guid.CreateVersion7()</c> para a etapa, a fase e a exigência (a suíte de
    /// origem normaliza esses ids antes de comparar — ver <c>NormalizarIds</c>), então um
    /// arquivo com bytes crus nunca seria estável entre regenerações. A prova aqui é de
    /// AUTOCONSISTÊNCIA (mesmo padrão de <see cref="RoundTrip_VersaoDeAbertura"/>): o
    /// primeiro <c>Codificar</c> É o oráculo — decodificar e recodificar tem de reproduzir
    /// EXATAMENTE os mesmos bytes, quaisquer que sejam os Guids sorteados nesta execução.
    /// </remarks>
    [Fact(DisplayName = "Round-trip 1.3 — reidratar e recanonicalizar o processo de referência (exigência documental rica + metadadosFatos) reproduz os bytes")]
    public void RoundTrip13_ProcessoDeReferenciaComExigenciaDocumentalRica()
    {
        ProcessoSeletivo processo = EnvelopeCanonicoGoldenTests.ProcessoDeReferencia();
        EntradaCanonicalizacao entrada = EntradaDeReferencia(processo);
        SnapshotCanonico congelado = new EnvelopeCodec().Codificar(entrada);
        ConferirSchemaCorrente(congelado);
        VersaoConfiguracao v1 = Publicado(processo, entrada, congelado, "user-sub-123");
        EnvelopeReidratado reidratado = Reidratado(v1);

        processo.RestaurarConfiguracaoCongelada(v1, reidratado.Grafo).IsSuccess.Should().BeTrue();

        ConferirQueARecodificacaoReproduzOsBytes(
            Recodificado(processo, v1, reidratado), congelado,
            "uma exigência documental rica (condicaoGatilho, basesLegais, idadeMaximaEmissao, formatoPermitido, " +
            "tamanhoMaximoBytes, metadadosFatos, valoresSelecionaveis)");
    }

    private static EntradaCanonicalizacao EntradaDeReferencia(ProcessoSeletivo processo) =>
        new(
            processo, EnvelopeCanonicoGoldenTests.DadosDeReferencia(), EnvelopeCanonicoGoldenTests.HashFixo,
            FusoInstitucional.ZoneId,
            MetadadosFatosCongelados: EnvelopeCanonicoGoldenTests.MetadadosFatosDeReferencia(),
            ValoresSelecionaveisCongelados: EnvelopeCanonicoGoldenTests.ValoresSelecionaveisDeReferencia(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo),
            FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao);

    // ── Round-trip 1.4 — o bloco novo, arvoreSatisfacao (Story #923) ──

    /// <summary>
    /// Prova de fidelidade do bloco <c>arvoreSatisfacao</c> (Story #923, bump 1.4): uma
    /// árvore com grupo <c>OU</c> + base legal própria, uma folha com cardinalidade
    /// qualificada (<see cref="ChaveDistincao.CompetenciaMensal"/>) e uma folha
    /// <c>repetePorEntidade</c> — as três dimensões que a árvore acumulou nas Stories
    /// #920/#921/#922 e que o snapshot canônico só passa a congelar agora. Sem golden
    /// fixture fixa (mesma razão de <see cref="RoundTrip13_ProcessoDeReferenciaComExigenciaDocumentalRica"/>
    /// — os ids são sorteados); a prova é de AUTOCONSISTÊNCIA, e uma asserção direta sobre
    /// a árvore recomposta fecha o que a comparação de bytes por si só não deixa óbvio.
    /// </summary>
    [Fact(DisplayName = "Round-trip 1.4 — árvore com grupo OU, folha com cardinalidade qualificada e folha repetePorEntidade reproduz os bytes")]
    public void RoundTrip14_ArvoreComGrupoCardinalidadeERepeticao()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoDeInscricao("PS Árvore 1.4");
        DocumentoExigido comprovanteRenda = ComprovanteDeRendaComModelo(fase);
        DefinirArvoreDeSatisfacao(processo, fase, comprovanteRenda);
        EntradaCanonicalizacao entrada = EntradaDaArvore(processo);
        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(entrada);
        ConferirSchemaCorrente(congelado);
        VersaoConfiguracao v1 = Publicado(processo, entrada, congelado, "user-sub-arvore");
        EnvelopeReidratado reidratado = Reidratado(v1);
        ConferirQueOModeloDaExigenciaSobreviveuAoDecoder(reidratado, comprovanteRenda);
        processo.RestaurarConfiguracaoCongelada(v1, reidratado.Grafo).IsSuccess.Should().BeTrue();
        ConferirQueARecodificacaoReproduzOsBytes(
            Recodificado(processo, v1, reidratado), congelado, "uma árvore com grupo OU, cardinalidade qualificada e repetição por entidade");
        ConferirArvoreRecarregada(processo);
    }

    private static void DefinirArvoreDeSatisfacao(ProcessoSeletivo processo, FaseCronograma fase, DocumentoExigido comprovanteRenda)
    {
        DefinirGrupoDeComposicaoFamiliar(processo);
        processo.DefinirDocumentosExigidos([RaizGrupoOuDeRgERenda(fase, comprovanteRenda)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
    }

    private static EntradaCanonicalizacao EntradaDaArvore(ProcessoSeletivo processo) =>
        new(
            processo, DadosDeEditalDoEnvelope(), "2222222222222222222222222222222222222222222222222222222222222222", FusoInstitucional.ZoneId,
            ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao);

    // obrigatorio: false — DocumentoExigido.DeterminaResultado() (obrigatória OU
    // consequência declarada) fica falso nas duas folhas, então a checagem de base
    // legal de AvaliarConformidade não exige NoExigenciaBaseLegal delas: só a
    // consequência ELIMINA do GRUPO determina resultado aqui, e a base legal própria
    // dele (a do grupo) já cobre isso.
    private static DocumentoExigido DocumentoDeRgSemObrigatoriedade(FaseCronograma fase) =>
        DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null,
            FormatosPermitidos.Criar(true, null).Value!, null, finalidade: FinalidadeFormulario.Inscricao).Value!;

    // A declaração de renda vem com o modelo editável que o candidato preenche — o decoder tem
    // de remontá-lo, senão a recanonicalização o perderia.
    private static DocumentoExigido ComprovanteDeRendaComModelo(FaseCronograma fase) =>
        DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "COMPROVANTE_RENDA", "Comprovante de renda", "SOCIOECONOMICO", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null,
            FormatosPermitidos.Criar(true, null).Value!, null,
            modelo: new ModeloDaExigencia(Guid.CreateVersion7(), "Declaração de renda.docx", FormatoDeDocumento.Docx, new string('e', 64)),
            finalidade: FinalidadeFormulario.Inscricao).Value!;

    private static void DefinirGrupoDeComposicaoFamiliar(ProcessoSeletivo processo) =>
        processo.DefinirItens([], grupos:
        [
            GrupoColetado.Criar(
                "COMPOSICAO_FAMILIAR", 0, FormularioDeTeste.Secao, "Composição familiar", 1, 10, null, Obrigatoriedade.Sempre,
                [FatoColetado.Criar("SEM_RENDA", 0, "Sem renda", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!]).Value!,
        ]).IsSuccess.Should().BeTrue();

    private static NoExigencia RaizGrupoOuDeRgERenda(FaseCronograma fase, DocumentoExigido comprovanteRenda)
    {
        NoExigencia folhaRg = NoExigencia.CriarFolha(
            DocumentoDeRgSemObrigatoriedade(fase), 0, quantidadeMinima: 3, chaveDistincao: ChaveDistincao.CompetenciaMensal,
            dataReferencia: new DateOnly(2026, 3, 31)).Value!;
        NoExigencia folhaRenda = NoExigencia.CriarFolha(
            comprovanteRenda, 1, repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;
        NoExigenciaBaseLegal baseLegalDoGrupo = NoExigenciaBaseLegal.Criar(
            "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, "Norma interna").Value!;
        return NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "ELIMINA", [baseLegalDoGrupo], [folhaRg, folhaRenda]).Value!;
    }

    // A restauração reaproveita a exigência viva de mesmo Id, então a recanonicalização não
    // enxerga o que o decoder perde numa exigência: o modelo é conferido no grafo.
    private static void ConferirQueOModeloDaExigenciaSobreviveuAoDecoder(
        EnvelopeReidratado reidratado, DocumentoExigido comprovanteRenda) =>
        reidratado.Grafo.DocumentosExigidos.Single(static d => d.TipoDocumentoCodigo == "COMPROVANTE_RENDA").Modelo
            .Should().Be(comprovanteRenda.Modelo);

    private static void ConferirArvoreRecarregada(ProcessoSeletivo processo)
    {
        NoExigencia raiz = processo.RaizesDeExigencia.Should().ContainSingle().Which;
        raiz.Tipo.Should().Be(TipoNo.GrupoOu);
        raiz.Consequencia.Should().Be("ELIMINA");
        raiz.BasesLegais.Should().ContainSingle().Which.Referencia.Should().Be("Res. Unifesspa 532/2021, art. 12");
        raiz.Filhos.Should().HaveCount(2);
        ConferirFolhaDeRgRecarregada(raiz.Filhos.Should().ContainSingle(static f => f.ChaveDistincao != null).Which);
        ConferirFolhaDeRendaRecarregada(raiz.Filhos.Should().ContainSingle(static f => f.RepetePorEntidade != null).Which);
    }

    private static void ConferirFolhaDeRgRecarregada(NoExigencia folha)
    {
        folha.QuantidadeMinima.Should().Be(3);
        folha.ChaveDistincao.Should().Be(ChaveDistincao.CompetenciaMensal);
        folha.DataReferencia.Should().Be(new DateOnly(2026, 3, 31));
        folha.DocumentoExigido.Should().NotBeNull();
        folha.DocumentoExigido!.TipoDocumentoCodigo.Should().Be("RG");
    }

    private static void ConferirFolhaDeRendaRecarregada(NoExigencia folha)
    {
        folha.RepetePorEntidade.Should().Be("COMPOSICAO_FAMILIAR");
        folha.DocumentoExigido.Should().NotBeNull();
        folha.DocumentoExigido!.TipoDocumentoCodigo.Should().Be("COMPROVANTE_RENDA");
    }

    // ── issue #1067 — os seis conjuntos ordenados por conteúdo, povoados no mesmo envelope ──
}
