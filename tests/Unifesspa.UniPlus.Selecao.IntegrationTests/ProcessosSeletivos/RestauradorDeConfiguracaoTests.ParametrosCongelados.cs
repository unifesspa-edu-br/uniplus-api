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
/// Os parâmetros que a publicação congela no envelope — metadado de fato, fuso e valores
/// selecionáveis — e a prova de que a restauração os reproduz sem consultar o que está vivo.
/// </summary>
public sealed partial class RestauradorDeConfiguracaoTests
{
    /// <summary>
    /// A prova estrutural do congelamento dos parâmetros na publicação (UNI-REQ-0019, Story #919): <see cref="FatoCandidato"/> não tem
    /// NENHUMA mutação em runtime (seed-governado, append-only, ADR-0111) — não é possível
    /// simular "o catálogo mudou" via um comando real. A prova correta é estrutural: publica
    /// com um metadado de fato conhecido (um <c>Binding</c> X, resolvido por um
    /// <c>IFatoCandidatoReader</c> hipotético no instante da publicação — aqui montado
    /// diretamente, já que o canonicalizador é puro e não injeta o reader), e confirma que
    /// <see cref="RestauradorDeConfiguracao.Restaurar"/> reproduz os MESMOS bytes/hash SEM que
    /// o serviço precise (ou possa) reconsultar o catálogo vivo — o próprio construtor de
    /// <see cref="RestauradorDeConfiguracao"/> só aceita <see cref="IRegistroCodecsEnvelope"/>,
    /// nunca um <c>IFatoCandidatoReader</c> (ver a asserção por reflexão abaixo). Isso prova,
    /// estruturalmente, que a restauração usa o metadado CONGELADO, nunca o catálogo vivo.
    /// </summary>
    [Fact(DisplayName = "Story #919 (congelamento dos parâmetros na publicação, UNI-REQ-0019): Restaurar reproduz o metadado de fato congelado sem reconsultar o catálogo vivo")]
    public void Restaurar_ComMetadadoDeFatoCongelado_ReporEProvarSemReconsultarCatalogoVivo()
    {
        ConferirQueORestauradorNaoConheceOCatalogoVivo();

        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        DefinirLaudoCondicionalAoTea(processo);
        DadosEdital dados = CorpusEnvelope.DadosRicos();
        SnapshotCanonico congelado = new EnvelopeCodec().Codificar(EntradaComMetadadoCongelado(processo, dados, somenteOsValoresDoCorpus: true));
        congelado.SchemaVersion.Should().Be("0.0.42", "pré-condição: o codec corrente emite a forma única");
        VersaoConfiguracao versao = PublicadaPeloAtorDoCorpus(processo, dados, congelado);

        ReporORestauradoNaRaizViva(processo, versao);
        ConferirQueORepostoRecanonicalizaNosBytesCongelados(processo, dados, congelado);
    }

    // Prova estrutural, em vez de comportamental: RestauradorDeConfiguracao não tem
    // como chamar um IFatoCandidatoReader porque ele nunca é injetado — o construtor
    // só conhece IRegistroCodecsEnvelope.
    private static void ConferirQueORestauradorNaoConheceOCatalogoVivo()
    {
        System.Reflection.ConstructorInfo construtor = typeof(RestauradorDeConfiguracao).GetConstructors().Single();
        construtor.GetParameters().Select(static p => p.ParameterType.Name).Should().NotContain(
            "IFatoCandidatoReader",
            "a restauração prova o round-trip com o metadado JÁ CONGELADO no envelope — reconsultar o catálogo vivo " +
            "aqui reintroduziria exatamente o acoplamento que o congelamento dos parâmetros na publicação (UNI-REQ-0019) existe para impedir");
    }

    private static void DefinirLaudoCondicionalAoTea(ProcessoSeletivo processo)
    {
        DocumentoExigido exigencia = LaudoMedicoCondicionalAoTea(processo.CronogramaFases.First().Id);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
    }

    private static DocumentoExigido LaudoMedicoCondicionalAoTea(Guid faseId) =>
        DocumentoExigido.Criar(
            faseId, tipoDocumentoOrigemId: Guid.CreateVersion7(), tipoDocumentoCodigo: "LAUDO_MEDICO", tipoDocumentoNome: "Laudo médico",
            tipoDocumentoCategoria: "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal, aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true, consequenciaIndeferimento: null, condicoes: [CondicaoDoTea()], basesLegais: [BaseLegalDaLeiDaPessoaComDeficiencia()],
            idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
            tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;

    private static CondicaoGatilho CondicaoDoTea() =>
        CondicaoGatilho.Criar(0, "TIPO_DEFICIENCIA", Operador.Igual, JsonSerializer.SerializeToElement("TEA")).Value!;

    private static DocumentoExigidoBaseLegal BaseLegalDaLeiDaPessoaComDeficiencia() =>
        DocumentoExigidoBaseLegal.Criar("Lei 13.146/2015", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;

    // O Binding "X" — resolvido do catálogo NO INSTANTE da publicação, congelado por
    // valor. Se a restauração reconsultasse o catálogo vivo, o teste não teria como
    // provar isso; congelando aqui, a única fonte possível para o round-trip é o
    // envelope decodificado.
    private static Dictionary<string, MetadadoFatoCongelado> MetadadoCongeladoDoTipoDeDeficiencia() =>
        new(StringComparer.Ordinal)
        {
            ["TIPO_DEFICIENCIA"] = new MetadadoFatoCongelado(
                Codigo: "TIPO_DEFICIENCIA",
                Dominio: "CATEGORICO",
                Origem: "DECLARADO",
                Cardinalidade: "ESCALAR",
                PontoResolucao: "INSCRICAO",
                Binding: "CAMPO_FORMULARIO:TIPO_DEFICIENCIA",
                ValoresDominio: null,
                ValoresDominioDeclarados: null),
        };

    // Na primeira codificação os valores selecionáveis são os do corpus; na recanonicalização
    // do agregado reposto, completados com os que o catálogo do conjunto básico congela.
    private static EntradaCanonicalizacao EntradaComMetadadoCongelado(
        ProcessoSeletivo processo, DadosEdital dados, bool somenteOsValoresDoCorpus) =>
        new(
            processo, dados, CorpusEnvelope.HashDocumento, FusoInstitucional.ZoneId,
            MetadadosFatosCongelados: MetadadoCongeladoDoTipoDeDeficiencia(),
            ValoresSelecionaveisCongelados: somenteOsValoresDoCorpus
                ? CorpusEnvelope.ValoresSelecionaveisRicos()
                : CatalogoDoConjuntoBasico.ComValoresCongelados(CorpusEnvelope.ValoresSelecionaveisRicos()),
            CalendarioDiasUteis: CorpusEnvelope.CalendarioRico(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao);

    // A prova final: recanonicalizar o agregado reposto reproduz os MESMOS bytes —
    // incluindo o bloco metadadosFatos, que só sobreviveu porque veio inteiro dentro do
    // envelope decodificado (EnvelopeReidratado.MetadadosFatosCongelados), nunca porque
    // foi reconsultado.
    private static void ConferirQueORepostoRecanonicalizaNosBytesCongelados(
        ProcessoSeletivo processo, DadosEdital dados, SnapshotCanonico congelado) =>
        new EnvelopeCodec().Codificar(EntradaComMetadadoCongelado(processo, dados, somenteOsValoresDoCorpus: false)).Bytes
            .Should().Equal(congelado.Bytes, "o agregado reposto recanonicaliza, byte a byte, o que a versão congelou");

    /// <summary>
    /// A razão de o fuso viajar no envelope, e não ser lido da constante na hora de provar.
    /// </summary>
    /// <remarks>
    /// A recanonicalização recalcula <c>documentosExigidos.dataReferenciaFatos</c>, que converte um
    /// instante em dia civil — e o dia civil depende do fuso. Se a prova usasse o fuso vigente em
    /// vez do congelado, bastaria o padrão institucional mudar para a mesma versão passar a produzir
    /// bytes diferentes, e o descarte da retificação — que depende dessa igualdade byte a byte —
    /// deixaria de ser possível. Este teste congela sob um fuso e recanonicaliza sob outro: os
    /// bytes têm de vir do congelado.
    /// </remarks>
    [Fact(DisplayName = "UNI-REQ-0111: a prova byte a byte usa o fuso CONGELADO, não o vigente")]
    public void Restaurar_ComFusoInstitucionalDiferenteDoCongelado_ProvaPeloCongelado()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        DadosEdital dados = CorpusEnvelope.DadosRicos();
        SnapshotCanonico congelado = new EnvelopeCodec().Codificar(EntradaSobUmFusoQueNaoEOInstitucional(processo, dados));
        VersaoConfiguracao versao = PublicadaPeloAtorDoCorpus(processo, dados, congelado);

        Result<GrafoConfiguracao> resultado = new RestauradorDeConfiguracao(new RegistroCodecsEnvelope()).Restaurar(processo, versao);

        // Passa: a recanonicalização leu o fuso de dentro do envelope. Falharia se lesse a
        // constante corrente, porque o bloco de localidade sairia com outra zona.
        resultado.IsSuccess.Should().BeTrue(
            resultado.Error?.Message + " — a prova tem de usar o fuso que a versão congelou, não o institucional de hoje");
    }

    // Congela sob uma zona que NÃO é a institucional corrente — é o que simula o padrão
    // institucional tendo mudado depois desta versão publicada.
    private static EntradaCanonicalizacao EntradaSobUmFusoQueNaoEOInstitucional(ProcessoSeletivo processo, DadosEdital dados)
    {
        const string FusoDaEpoca = "America/Manaus";
        FusoDaEpoca.Should().NotBe(FusoInstitucional.ZoneId, "o teste perde o sentido se as duas coincidirem");
        return new(
            processo, dados, CorpusEnvelope.HashDocumento, FusoDaEpoca,
            ValoresSelecionaveisCongelados: CorpusEnvelope.ValoresSelecionaveisRicos(),
            CalendarioDiasUteis: CorpusEnvelope.CalendarioRico(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao);
    }

    /// <summary>
    /// Issue #1059 (UNI-REQ-0072): a fidelidade do dicionário de valores selecionáveis, com os
    /// <b>dois</b> tipos de categórico coletado — estático (COR_RACA, valores do catálogo) e de
    /// escopo-processo (CONDICAO_ATENDIMENTO, valores da oferta do próprio processo) — na MESMA
    /// versão. A asserção é sobre o <b>conteúdo</b> do dicionário reidratado
    /// (<see cref="EnvelopeReidratado.ValoresSelecionaveisCongelados"/>), não só sobre os bytes:
    /// é o que distingue "o campo sobreviveu" de "os bytes coincidem por acaso" (D5 do plano da
    /// issue — o dicionário é o ponto mais provável de se perder na travessia decoder →
    /// restaurador → encoder).
    /// </summary>
    [Fact(DisplayName = "Restaurar reproduz o dicionário de valores selecionáveis com os dois tipos de categórico (estático e escopo-processo)")]
    public void Restaurar_ComValoresSelecionaveisDosDoisTipos_ReporEProvarConteudoDoDicionario()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        DefinirColetaComCondicaoDeAtendimento(processo);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(EntradaComCondicaoDeAtendimento(processo));
        PublicarComOsBytesJaCodificados(processo, congelado);
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);
        ConferirODicionarioReidratado(Reidratado(versao).ValoresSelecionaveisCongelados);
        ReporORestauradoNaRaizViva(processo, versao);
        CorpusEnvelope.Codec.Codificar(EntradaComCondicaoDeAtendimento(processo)).Bytes
            .Should().Equal(congelado.Bytes, "o agregado reposto recanonicaliza, byte a byte, o que a versão congelou");
    }

    // Substitui a coleta pelo mesmo par {COR_RACA, RENDA} do corpus rico — SEM a
    // pré-condição de RENDA, irrelevante para esta prova — acrescido de
    // CONDICAO_ATENDIMENTO (escopo-processo, SELECAO_MULTIPLA), da edição do ENEM, que a
    // classificação do corpus exige na inscrição, e da opção pelas vagas de escola pública, de
    // que a opção pelas cotas depende.
    private static void DefinirColetaComCondicaoDeAtendimento(ProcessoSeletivo processo) =>
        processo.DefinirItens([
            CampoPessoal("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre),
            CampoPessoal("RENDA", 1, "Faixa de renda familiar", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre),
            CampoPessoal("CONDICAO_ATENDIMENTO", 2, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, Obrigatoriedade.Nunca),
            FormularioDeTeste.CampoDaEdicaoDoEnem(3),
            CampoPessoal(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 4, "Deseja concorrer às vagas de escola pública?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre),
        ], PrecondicaoIfMatch.Curinga, grupos: []).IsSuccess.Should().BeTrue();

    private static FatoColetado CampoPessoal(string codigo, int ordem, string rotulo, TipoRenderizacao tipo, Obrigatoriedade obrigatoriedade) =>
        FatoColetado.Criar(
            codigo, ordem, rotulo, tipo, obrigatoriedade, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
            finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    // Escopo-processo: os códigos batem com a oferta de atendimento do corpus rico
    // (Pcd, LACTANTE), ordenados por CondicaoCodigo — "LACTANTE" antes de "PCD".
    private static Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresComCondicaoDeAtendimento() =>
        new(CorpusEnvelope.ValoresSelecionaveisRicos())
        {
            ["CONDICAO_ATENDIMENTO"] =
            [
                new ValorDominioDeclaradoCongelado("LACTANTE", "Lactante", 0),
                new ValorDominioDeclaradoCongelado(OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência", 1),
            ],
        };

    private static EntradaCanonicalizacao EntradaComCondicaoDeAtendimento(ProcessoSeletivo processo) =>
        new(
            processo, CorpusEnvelope.DadosRicos(), CorpusEnvelope.HashDocumento, FusoInstitucional.ZoneId,
            ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(ValoresComCondicaoDeAtendimento()),
            CalendarioDiasUteis: CorpusEnvelope.CalendarioRico(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao);

    // Publica DIRETAMENTE com os bytes já codificados — CorpusEnvelope.Publicar(..., CorpusEnvelope.ContextoRico())
    // recodificaria com CorpusEnvelope.ValoresSelecionaveisRicos() (sem CONDICAO_ATENDIMENTO)
    // e produziria um envelope diferente do que este teste está provando.
    private static void PublicarComOsBytesJaCodificados(ProcessoSeletivo processo, SnapshotCanonico congelado)
    {
        processo.Publicar(
            CorpusEnvelope.DadosRicos(), congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            CorpusEnvelope.HashDocumento, CorpusEnvelope.Ator, TimeProvider.System, CorpusEnvelope.ContextoRico(),
            FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).IsSuccess.Should().BeTrue();
        processo.ClearDomainEvents();
    }

    private static void ConferirODicionarioReidratado(IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? dicionario)
    {
        dicionario.Should().NotBeNull();
        ConferirOsValoresDeCorRaca(dicionario!);
        ConferirOsValoresDaCondicaoDeAtendimento(dicionario!);

        // As entradas de fatos não-seleção (RENDA continua SELECAO_UNICA no corpus rico — o
        // dicionário completo tem todas) sobrevivem também.
        dicionario.Should().ContainKey("RENDA");
    }

    // A afirmação do CONTEÚDO — não só que os bytes batem no final.
    private static void ConferirOsValoresDeCorRaca(IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> dicionario)
    {
        dicionario.Should().ContainKey("COR_RACA");
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores = dicionario["COR_RACA"];
        valores.Should().NotBeNull("COR_RACA é estático, SELECAO_UNICA");
        valores!.Select(static v => v.Codigo).Should().Equal(["BRANCA", "PRETA", "PARDA"]);
        valores.Select(static v => v.Descricao).Should().Equal([
            "Autodeclaração de cor/raça branca.", "Preta", "Autodeclaração de cor/raça parda.",
        ]);
        valores.Select(static v => v.Orientacao).Should().Equal([
            null, "Quem concorre às vagas para pretos e pardos passa pela heteroidentificação.", null,
        ], "a orientação congelada volta com o valor");
    }

    private static void ConferirOsValoresDaCondicaoDeAtendimento(IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> dicionario)
    {
        dicionario.Should().ContainKey("CONDICAO_ATENDIMENTO");
        dicionario["CONDICAO_ATENDIMENTO"].Should().NotBeNull("CONDICAO_ATENDIMENTO é escopo-processo, SELECAO_MULTIPLA");
        dicionario["CONDICAO_ATENDIMENTO"]!.Select(static v => v.Codigo).Should().Equal(
            ["LACTANTE", OfertaAtendimentoEspecializado.CodigoCondicaoPcd]);
    }
}
