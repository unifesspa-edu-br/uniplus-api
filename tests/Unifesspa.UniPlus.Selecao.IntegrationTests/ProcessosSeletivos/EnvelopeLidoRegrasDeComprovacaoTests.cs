namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

public sealed class EnvelopeLidoRegrasDeComprovacaoTests
{
    [Fact(DisplayName = "Round-trip 1.3 — reidratar e recanonicalizar o processo de referência (exigência documental rica + metadadosFatos) reproduz os bytes")]
    public void Reidratar_ProcessoDeReferencia_ReproduzOsBytes()
    {
        ProcessoSeletivo processo = EnvelopeDeReferencia.ProcessoDeReferencia(comBancasExigidasNaPublicacao: true);
        EntradaCanonicalizacao entrada = EntradaDeReferencia(processo);
        SnapshotCanonico congelado = new EnvelopeCodec().Codificar(entrada);
        ConferirSchemaCorrente(congelado);
        VersaoConfiguracao v1 = Publicado(processo, entrada, congelado, "user-conta-123");
        FidelidadeDoEnvelopeDeTeste.Conferir(processo, v1, congelado);
    }

    private static EntradaCanonicalizacao EntradaDeReferencia(ProcessoSeletivo processo) =>
        new(
            processo, EnvelopeDeReferencia.DadosDeReferencia(), EnvelopeDeReferencia.HashFixo,
            FusoInstitucional.ZoneId,
            null, null, new DadosDeCatalogoDaConfiguracaoPublicada(
            MetadadosFatosCongelados: EnvelopeDeReferencia.MetadadosFatosDeReferencia(),
            ValoresSelecionaveisCongelados: EnvelopeDeReferencia.ValoresSelecionaveisDeReferencia(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo),
            FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));
    [Fact(DisplayName = "As regras de quantidade mínima, cardinalidade e repetição preservam o envelope")]
    public void Reidratar_ArvoreComGrupoCardinalidadeERepeticao_ReproduzOsBytes()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoDeInscricao("PS Árvore 1.4");
        DocumentoExigido comprovanteRenda = ComprovanteDeRendaComModelo(fase);
        DefinirRegrasDeComprovacao(processo, fase, comprovanteRenda);
        EntradaCanonicalizacao entrada = EntradaDaArvore(processo);
        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(entrada);
        ConferirSchemaCorrente(congelado);
        VersaoConfiguracao v1 = Publicado(processo, entrada, congelado, "user-conta-arvore");
        EnvelopeReidratado reidratado = FidelidadeDoEnvelopeDeTeste.Conferir(processo, v1, congelado);
        ConferirQueOModeloDaExigenciaSobreviveuAoDecoder(reidratado, comprovanteRenda);
        ConferirArvoreRecarregada(ProcessoDoGrafoLidoDeTeste.Criar(processo, reidratado.Configuracao));
    }

    private static void DefinirRegrasDeComprovacao(ProcessoSeletivo processo, FaseCronograma fase, DocumentoExigido comprovanteRenda)
    {
        DefinirGrupoDeComposicaoFamiliar(processo);
        processo.DefinirDocumentosExigidos([RaizGrupoOuDeRgERenda(fase, comprovanteRenda)])
            .IsSuccess.Should().BeTrue();
    }

    private static EntradaCanonicalizacao EntradaDaArvore(ProcessoSeletivo processo) =>
        new(
            processo, DadosDeEditalDoEnvelope(), "2222222222222222222222222222222222222222222222222222222222222222", FusoInstitucional.ZoneId,
            null, null, new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));
    private static DocumentoExigido DocumentoDeRgSemObrigatoriedade(FaseCronograma fase) =>
        DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null,
            FormatosPermitidos.Criar(true, null).Value!, null, finalidade: FinalidadeFormulario.Inscricao).Value!;
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
            GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 0)
                .ComOcorrencias(1, 10)
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .ComCampos(FatoColetadoDeTeste.Coletando("SEM_RENDA", 0).ComRotulo("Sem renda").ComObrigatoriedade(Obrigatoriedade.Sempre).Construir())
                .Construir(),
        ]).IsSuccess.Should().BeTrue();

    private static RegraDeComprovacao RaizGrupoOuDeRgERenda(FaseCronograma fase, DocumentoExigido comprovanteRenda)
    {
        RegraDeComprovacao folhaRg = RegraDeComprovacao.CriarRegraDeDocumento(
            DocumentoDeRgSemObrigatoriedade(fase), 0, quantidadeMinima: 3, chaveDistincao: ChaveDistincao.CompetenciaMensal,
            dataReferencia: new DateOnly(2026, 3, 31)).Value!;
        RegraDeComprovacao folhaRenda = RegraDeComprovacao.CriarRegraDeDocumento(
            comprovanteRenda, 1, grupoDoFormularioParaRepeticao: "COMPOSICAO_FAMILIAR").Value!;
        BaseLegalDaComprovacao baseLegalDoGrupo = BaseLegalDaComprovacao.Criar(
            "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, "Norma interna").Value!;
        return RegraDeComprovacao.CriarGrupo(
            FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 1, "ELIMINA", [baseLegalDoGrupo], [folhaRg, folhaRenda]).Value!;
    }
    private static void ConferirQueOModeloDaExigenciaSobreviveuAoDecoder(
        EnvelopeReidratado reidratado, DocumentoExigido comprovanteRenda) =>
        reidratado.Configuracao.Exigencias.DocumentosExigidos.Single(static d => d.TipoDocumentoCodigo == "COMPROVANTE_RENDA").Modelo
            .Should().Be(comprovanteRenda.Modelo);

    private static void ConferirArvoreRecarregada(ProcessoSeletivo processo)
    {
        RegraDeComprovacao raiz = processo.RegrasDeComprovacaoSemGrupo.Should().ContainSingle().Which;
        raiz.Forma.Should().Be(FormaDeComprovacao.QuantidadeMinimaDeItens);
        raiz.Consequencia.Should().Be("ELIMINA");
        raiz.BasesLegais.Should().ContainSingle().Which.Referencia.Should().Be("Res. Unifesspa 532/2021, art. 12");
        raiz.Itens.Should().HaveCount(2);
        ConferirFolhaDeRgRecarregada(raiz.Itens.Should().ContainSingle(static f => f.Distincao != null).Which);
        ConferirFolhaDeRendaRecarregada(raiz.Itens.Should().ContainSingle(static f => f.GrupoDoFormularioParaRepeticao != null).Which);
    }

    private static void ConferirFolhaDeRgRecarregada(RegraDeComprovacao folha)
    {
        folha.QuantidadeMinima.Should().Be(3);
        folha.Distincao!.Chave.Should().Be(ChaveDistincao.CompetenciaMensal);
        folha.Distincao!.DataReferencia.Should().Be(new DateOnly(2026, 3, 31));
        folha.DocumentoExigido.Should().NotBeNull();
        folha.DocumentoExigido!.TipoDocumentoCodigo.Should().Be("RG");
    }

    private static void ConferirFolhaDeRendaRecarregada(RegraDeComprovacao folha)
    {
        folha.GrupoDoFormularioParaRepeticao.Should().Be("COMPOSICAO_FAMILIAR");
        folha.DocumentoExigido.Should().NotBeNull();
        folha.DocumentoExigido!.TipoDocumentoCodigo.Should().Be("COMPROVANTE_RENDA");
    }
}
