namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A restauração de predicados com bloco opaco e de gatilhos de exigência ancorados em fase.
/// </summary>
public sealed partial class RestauradorDeConfiguracaoTests
{
    /// <summary>
    /// A válvula de escape de obrigatoriedade (<see cref="Customizado"/>, ADR-0058) carrega
    /// JSON arbitrário — inclusive número decimal legítimo. A forma canônica <b>preserva</b> o
    /// número em vez de exigir inteiro: se o exigisse, este publish lançaria no meio da
    /// canonicalização (500), e um parâmetro <c>{"limite":1.5}</c> perfeitamente válido não
    /// poderia ser congelado. O decoder aceita o bloco opaco, e o round-trip reproduz os bytes.
    /// </summary>
    [Fact(DisplayName = "Predicado customizado com número decimal no bloco opaco publica e reidrata — a forma preserva, não recusa")]
    public void Restaurar_ComPredicadoCustomizadoDecimal_PublicaEReidrata()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        using JsonDocument parametros = JsonDocument.Parse("""{"limite":1.5,"regra":"transitoria"}""");

        // O publish canonicaliza; se a forma exigisse inteiro, estouraria aqui.
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(
            CorpusEnvelope.Entrada(processo, conformidade: new ResultadoConformidade([RegraCustomizada(parametros)], [])));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);
        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre()).IsSuccess.Should().BeTrue();

        RestauradoSemErro(processo, versao, "a restauração do predicado customizado com decimal deveria ter provado o round-trip");
    }

    private static RegraAvaliada RegraCustomizada(JsonDocument parametros) =>
        new(
            RegraId: Guid.CreateVersion7(),
            RegraCodigo: "REGRA-CUSTOMIZADA",
            Categoria: CategoriaObrigatoriedade.Outros,
            TipoProcessoCodigoAvaliado: "SiSU",
            Predicado: new Customizado(parametros.RootElement.Clone()),
            Aprovada: true,
            Motivo: null,
            BaseLegal: "Lei de teste",
            AtoNormativoUrl: null,
            PortariaInterna: null,
            DescricaoHumana: "Válvula de escape com parâmetro decimal",
            VigenciaInicio: new DateOnly(2020, 1, 1),
            VigenciaFim: null,
            Hash: new string('c', 64));

    /// <summary>
    /// Achado de revisão da PR #903 (Story #554, PR #903): a sombra de verificação
    /// (<see cref="RestauradorDeConfiguracao"/>, "prova primeiro, aplica depois") começa
    /// SEM nenhuma fase viva rastreada — <see cref="ProcessoSeletivo.AplicarGrafo"/>
    /// reconcilia o cronograma por Ordem contra a instância viva, e a sombra não tem
    /// nenhuma. Antes da correção, <c>FaseCronograma.Id</c> nunca sobrevivia à
    /// reidratação (só <c>Ordem</c>/<c>FaseCanonicaOrigemId</c> eram congelados), e
    /// qualquer configuração com gatilho <c>FAIXA_ETARIA</c> ancorado a uma fase
    /// (<c>INICIO_FASE</c>/<c>FIM_FASE</c>) fazia <c>ResolverDataReferenciaFatos</c> não
    /// encontrar a fase que a política referencia — a prova de round-trip nunca
    /// completava. A correção (<see cref="FaseCronograma.Reidratar"/>, <c>id</c> congelado
    /// no bloco <c>cronogramaFases</c> da 1.2) resolve.
    /// </summary>
    [Fact(DisplayName = "Story #554 (PR #903): Restaurar sobre uma sombra vazia resolve dataReferenciaFatos com gatilho FAIXA_ETARIA ancorado em FIM_FASE")]
    public void Restaurar_ComGatilhoFaixaEtariaAncoradoEmFimFase_ReporEProvar()
    {
        FaseCronograma fase = ProcessoBaseDeEnvelope.FaseUnica("RESULTADO_FINAL", "RESULTADO_CLASSIFICACAO", permiteComplementacao: false);
        ProcessoSeletivo processo = ProcessoBaseDeEnvelope.Montar("PS Restaurador FAIXA_ETARIA", fase);
        DefinirGatilhoDeMaioridadeAncoradoNoFimDaFase(processo, fase);
        DadosEdital dados = CorpusEnvelope.DadosRicos();
        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(EntradaDoProcessoDeFaixaEtaria(processo, dados));
        VersaoConfiguracao versao = PublicadaPeloAtorDoCorpus(processo, dados, congelado);

        RestauradoSemErro(
            processo, versao,
            "sem o Id da fase congelado no envelope 1.2, ResolverDataReferenciaFatos " +
            "não encontraria a fase que FIM_FASE referencia na sombra vazia, e a prova de round-trip nunca completaria");
    }

    private static void DefinirGatilhoDeMaioridadeAncoradoNoFimDaFase(ProcessoSeletivo processo, FaseCronograma fase)
    {
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(DeclaracaoDeMaioridade(fase), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, fase.Id).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
    }

    private static DocumentoExigido DeclaracaoDeMaioridade(FaseCronograma fase) =>
        DocumentoExigido.Criar(
            fase.Id, tipoDocumentoOrigemId: Guid.CreateVersion7(), tipoDocumentoCodigo: "DECLARACAO_MAIORIDADE",
            tipoDocumentoNome: "Declaração de maioridade", tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [CondicaoMaiorDeIdade()], basesLegais: [BaseLegalDasCotas()], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;

    private static CondicaoGatilho CondicaoMaiorDeIdade() =>
        CondicaoGatilho.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;

    private static DocumentoExigidoBaseLegal BaseLegalDasCotas() =>
        DocumentoExigidoBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;

    private static EntradaCanonicalizacao EntradaDoProcessoDeFaixaEtaria(ProcessoSeletivo processo, DadosEdital dados) =>
        new(
            processo, dados, CorpusEnvelope.HashDocumento, FusoInstitucional.ZoneId,
            CalendarioDiasUteis: CorpusEnvelope.CalendarioRico(), ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao);
}
