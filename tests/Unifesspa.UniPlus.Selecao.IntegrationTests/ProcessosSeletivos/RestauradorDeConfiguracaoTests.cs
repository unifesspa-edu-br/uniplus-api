namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// A prova de round-trip como <b>guard de produção</b> — não só como teste (ADR-0110).
/// </summary>
/// <remarks>
/// <para>
/// O <c>RestaurarConfiguracaoCongelada</c> do agregado valida que a versão é <b>do
/// processo</b>, mas não tem como saber que o grafo veio <b>daquela</b> versão — o Domain
/// não canonicaliza (ADR-0042). É aqui, onde o codec e o agregado coexistem, que a
/// reposição é <b>autenticada</b>: recanonicaliza-se o que foi reposto e exige-se que
/// reproduza os bytes congelados.
/// </para>
/// <para>
/// Sem esta prova, um decoder com um campo a menos repõe uma configuração empobrecida e
/// <b>ninguém fica sabendo</b> — o certame publicado passa a divergir do documento que o
/// publicou. Com ela, o descarte falha alto.
/// </para>
/// </remarks>
public sealed class RestauradorDeConfiguracaoTests
{
    [Fact(DisplayName = "Restaurar decodifica, repõe e PROVA — e o agregado reposto recanonicaliza nos bytes congelados")]
    public void Restaurar_ReporEProvar()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        // A sessão editorial descaracterizou a configuração viva — é o que o descarte desfaz.
        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre()).IsSuccess.Should().BeTrue();

        RestauradorDeConfiguracao restaurador = new(CorpusEnvelope.Registro);

        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        // A prova é a do restaurador; a APLICAÇÃO na raiz viva é do descarte (Story #986): limpa as
        // coleções mutáveis e repõe o grafo provado — as demais dimensões reconciliam por reuse.
        processo.LimparColetaEDerivacaoParaRestauracao();
        processo.RestaurarConfiguracaoCongelada(versao, resultado.Value!).IsSuccess.Should().BeTrue();

        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes
            .Should().Equal(congelado.Bytes, "o agregado voltou a ser, byte a byte, o que a versão congelou");
    }

    /// <summary>
    /// Story #575: o corpus rico (<see cref="CorpusEnvelope.ProcessoRico"/>) tem a cascata
    /// de remanejamento configurada (as 8 federais de <c>DistribuicaoLei12711</c> são
    /// <c>SegueCascata</c>, INV-12), então <see cref="Restaurar_ReporEProvar"/> já a exercita
    /// no round-trip de descarte — mas só implicitamente, via a comparação de bytes. Este
    /// teste é a prova EXPLÍCITA: a cascata reposta na raiz VIVA é a mesma que a versão
    /// congelou, não uma sombra vazia que só coincide nos bytes agregados.
    /// </summary>
    [Fact(DisplayName = "Restaurar repõe a cascata de remanejamento na raiz viva — não só nos bytes recanonicalizados")]
    public void Restaurar_ComCascataCongelada_RepoeNaRaizViva()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        processo.Cascata.Should().NotBeNull("pré-condição: o corpus rico tem a cascata configurada");
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        // A sessão editorial descaracterizou a configuração viva — GrafoPobre não tem
        // cascata nenhuma. É o que o descarte tem de desfazer.
        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre()).IsSuccess.Should().BeTrue();
        processo.Cascata.Should().BeNull("pré-condição: a sessão editorial removeu a cascata da raiz viva");

        RestauradorDeConfiguracao restaurador = new(CorpusEnvelope.Registro);
        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        processo.LimparColetaEDerivacaoParaRestauracao();
        processo.RestaurarConfiguracaoCongelada(versao, resultado.Value!).IsSuccess.Should().BeTrue();

        processo.Cascata.Should().NotBeNull(
            "a cascata voltou à raiz VIVA, não só aos bytes recanonicalizados — é isso que o comando de " +
            "definição de cascata (e qualquer leitura seguinte da configuração viva) vai enxergar");
        processo.Cascata!.FallbackCodigo.Should().Be("AC");
        processo.Cascata.Destinos.Should().HaveCount(56, "a matriz legal completa do corpus rico tem 8 origens × 7 destinos");
    }

    /// <summary>
    /// Issue #849: <c>SombraParaVerificacao()</c> copia <c>UnidadeAdministradoraOrigemId</c>/
    /// <c>UnidadeAdministradora</c> da raiz viva — sem essa cópia, a recanonicalização do bloco
    /// <c>identidadesUnidade</c> a partir da sombra (vazia) nunca bateria com o congelado, e TODA
    /// restauração de TODO processo criado com Unidade falharia com
    /// <see cref="RestauradorDeConfiguracao.RoundTripDivergente"/>. Este teste prova o round-trip
    /// de ponta a ponta e que a raiz viva preserva a identidade da Unidade — diferente da cascata,
    /// não há "GrafoPobre" que a zere: ela nunca faz parte do <see cref="GrafoConfiguracao"/>
    /// (imutável desde a criação, sem operação de re-bind).
    /// </summary>
    [Fact(DisplayName = "Restaurar prova o round-trip com Unidade administradora — a sombra reproduz a identidade congelada (issue #849)")]
    public void Restaurar_ComUnidadeAdministradora_ProvaRoundTrip()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        Guid unidadeOrigemId = processo.UnidadeAdministradoraOrigemId;
        string siglaOriginal = processo.UnidadeAdministradora.Sigla;
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre()).IsSuccess.Should().BeTrue();

        RestauradorDeConfiguracao restaurador = new(CorpusEnvelope.Registro);
        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(
            $"se SombraParaVerificacao() não copiasse a Unidade administradora, a recanonicalização de " +
            $"'identidadesUnidade' nunca bateria com o congelado — {resultado.Error?.Message}");

        processo.LimparColetaEDerivacaoParaRestauracao();
        processo.RestaurarConfiguracaoCongelada(versao, resultado.Value!).IsSuccess.Should().BeTrue();

        processo.UnidadeAdministradoraOrigemId.Should().Be(unidadeOrigemId,
            "a identidade da Unidade administradora na raiz viva nunca sai de sincronia — é imutável desde a criação");
        processo.UnidadeAdministradora.Sigla.Should().Be(siglaOriginal);
    }

    /// <summary>
    /// Regressão: <c>Restaurar</c> montava a <see cref="EntradaCanonicalizacao"/> da prova
    /// SEM repassar <see cref="EnvelopeReidratado.Conformidade"/> — o canonicalizador recebia
    /// <see langword="null"/> e emitia <c>obrigatoriedades: []</c>, divergindo dos bytes
    /// congelados sempre que a versão carregasse regras legais avaliadas (não vazio). Este
    /// teste falha sem o campo repassado em <c>RestauradorDeConfiguracao.cs</c>.
    /// </summary>
    [Fact(DisplayName = "Restaurar repassa Conformidade adiante — a prova não diverge quando a versão congelou obrigatoriedades legais")]
    public void Restaurar_ComConformidadeCongelada_ReporEProvar()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();

        RegraAvaliada regra = new(
            RegraId: Guid.CreateVersion7(),
            RegraCodigo: "REGRA-RESTAURADOR",
            Categoria: CategoriaObrigatoriedade.Outros,
            TipoProcessoCodigoAvaliado: "SiSU",
            Predicado: new EtapaObrigatoria("Prova Objetiva"),
            Aprovada: true,
            Motivo: null,
            BaseLegal: "Lei de teste",
            AtoNormativoUrl: null,
            PortariaInterna: null,
            DescricaoHumana: "Regra de teste do restaurador",
            VigenciaInicio: new DateOnly(2020, 1, 1),
            VigenciaFim: null,
            Hash: new string('r', 64));
        ResultadoConformidade conformidade = new([regra], []);

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(
            CorpusEnvelope.Entrada(processo, conformidade: conformidade));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre()).IsSuccess.Should().BeTrue();

        RestauradorDeConfiguracao restaurador = new(CorpusEnvelope.Registro);

        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

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
        RegraAvaliada regra = new(
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
        ResultadoConformidade conformidade = new([regra], []);

        // O publish canonicaliza; se a forma exigisse inteiro, estouraria aqui.
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(
            CorpusEnvelope.Entrada(processo, conformidade: conformidade));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);
        processo.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre()).IsSuccess.Should().BeTrue();

        Result<GrafoConfiguracao> resultado = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    /// <summary>
    /// O teste decisivo desta classe: um codec que <b>perde um campo</b> não passa. Sem o
    /// guard, a restauração devolveria <c>Success</c> e a configuração empobrecida seria
    /// gravada.
    /// </summary>
    [Fact(DisplayName = "Um decoder que PERDE um campo faz a restauração FALHAR — não grava configuração empobrecida")]
    public void DecoderQuePerdeCampo_Falha()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        // Um registro cujo decoder devolve um grafo VÁLIDO, mas empobrecido — exatamente o
        // que um campo esquecido produziria. O agregado o aceita (é conforme); só a prova
        // de round-trip o rejeita.
        IRegistroCodecsEnvelope registroDefeituoso = Substitute.For<IRegistroCodecsEnvelope>();
        registroDefeituoso.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
            CorpusEnvelope.GrafoPobre(),
            CorpusEnvelope.DadosRicos(),
            CorpusEnvelope.HashDocumento,
            retificacao: null,
            conformidade: null)));
        registroDefeituoso
            .Recodificar(Arg.Any<string>(), Arg.Any<EntradaCanonicalizacao>())
            .Returns(call => CorpusEnvelope.Registro.Recodificar(
                call.Arg<string>(), call.Arg<EntradaCanonicalizacao>()));

        RestauradorDeConfiguracao restaurador = new(registroDefeituoso);

        byte[] antesDaTentativa = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;

        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);

        resultado.IsFailure.Should().BeTrue(
            "a configuração reposta não recanonicaliza nos bytes congelados — algo se perdeu. Aceitar isto faria o " +
            "certame publicado divergir do documento que o publicou, e nada acusaria.");
        resultado.Error!.Code.Should().Be(RestauradorDeConfiguracao.RoundTripDivergente);

        // A parte que importa: o agregado NÃO FOI TOCADO. Provar depois de repor deixaria a
        // raiz tracked empobrecida quando a prova falhasse, e bastaria um SaveChanges adiante
        // no mesmo escopo para gravar o estrago — a atomicidade dependeria de o handler
        // lembrar de não salvar. A prova roda sobre uma sombra destacada, antes.
        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes
            .Should().Equal(antesDaTentativa,
                "uma prova que falha não pode deixar resíduo no agregado — se ela repusesse primeiro e provasse " +
                "depois, este assert falharia, e o campo perdido estaria a um SaveChanges de ser persistido");
    }

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Restaurador FAIXA_ETARIA", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)).Value!,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", new string('c', 64)).Value!,
            nOpcoesAlocacao: 1,
            regrasEliminacao: [], baseadoEmEnem: false).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma fase = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "DECLARACAO_MAIORIDADE",
            tipoDocumentoNome: "Declaração de maioridade",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [condicao], basesLegais: [baseLegal], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, fase.Id).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        SnapshotPublicacaoCanonicalizer canonicalizer = new();
        DadosEdital dados = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: Guid.CreateVersion7()).Value!;
        string hashFixo = new('a', 64);
        SnapshotCanonico congelado = canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dados, hashFixo));

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, hashFixo, "user-sub-123", TimeProvider.System);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao versao = publicacao.Value!;

        RestauradorDeConfiguracao restaurador = new(new RegistroCodecsEnvelope());

        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(
            resultado.Error?.Message ?? "sem o Id da fase congelado no envelope 1.2, ResolverDataReferenciaFatos " +
            "não encontraria a fase que FIM_FASE referencia na sombra vazia, e a prova de round-trip nunca completaria");
    }

    /// <summary>
    /// A prova estrutural do RN08 (Story #919): <see cref="FatoCandidato"/> não tem
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
    [Fact(DisplayName = "Story #919 (RN08): Restaurar reproduz o metadado de fato congelado sem reconsultar o catálogo vivo")]
    public void Restaurar_ComMetadadoDeFatoCongelado_ReporEProvarSemReconsultarCatalogoVivo()
    {
        // Prova estrutural, em vez de comportamental: RestauradorDeConfiguracao não tem
        // como chamar um IFatoCandidatoReader porque ele nunca é injetado — o construtor
        // só conhece IRegistroCodecsEnvelope.
        System.Reflection.ConstructorInfo construtor = typeof(RestauradorDeConfiguracao).GetConstructors().Single();
        construtor.GetParameters().Select(static p => p.ParameterType.Name).Should().NotContain(
            "IFatoCandidatoReader",
            "a restauração prova o round-trip com o metadado JÁ CONGELADO no envelope — reconsultar o catálogo vivo " +
            "aqui reintroduziria exatamente o acoplamento que RN08 existe para impedir");

        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        Guid faseId = processo.CronogramaFases.First().Id;

        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "TIPO_DEFICIENCIA", Operador.Igual, JsonSerializer.SerializeToElement("TEA")).Value!;
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 13.146/2015", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "LAUDO_MEDICO",
            tipoDocumentoNome: "Laudo médico",
            tipoDocumentoCategoria: "SAUDE",
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [condicao],
            basesLegais: [baseLegal],
            idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
            tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        // O Binding "X" — resolvido do catálogo NO INSTANTE da publicação, congelado por
        // valor. Se a restauração reconsultasse o catálogo vivo, o teste não teria como
        // provar isso; congelando aqui, a única fonte possível para o round-trip é o
        // envelope decodificado.
        IReadOnlyDictionary<string, MetadadoFatoCongelado> metadadosFatos = new Dictionary<string, MetadadoFatoCongelado>(StringComparer.Ordinal)
        {
            ["TIPO_DEFICIENCIA"] = new MetadadoFatoCongelado(
                Codigo: "TIPO_DEFICIENCIA",
                Dominio: "CATEGORICO",
                Origem: "DECLARADO",
                Cardinalidade: "ESCALAR",
                PontoResolucao: "INSCRICAO",
                Binding: "CAMPO_INSCRICAO:TIPO_DEFICIENCIA",
                ValoresDominio: null,
                ValoresDominioDeclarados: null),
        };

        // Story #923 (bump 1.4): o canonicalizador VIVO passou a emitir 1.4 — a prova aqui é
        // sobre `metadadosFatos` (chave inalterada desde a 1.3), não sobre `arvoreSatisfacao`
        // (nova na 1.4), então a fonte é o codec 1.3 CONGELADO, não o vivo.
        DadosEdital dados = CorpusEnvelope.DadosRicos();
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis =
            CorpusEnvelope.ValoresSelecionaveisRicos();
        EntradaCanonicalizacao entrada = new(
            processo, dados, CorpusEnvelope.HashDocumento,
            MetadadosFatosCongelados: metadadosFatos,
            ValoresSelecionaveisCongelados: valoresSelecionaveis);
        SnapshotCanonico congelado = new EnvelopeCodec().Codificar(entrada);
        congelado.SchemaVersion.Should().Be("0.0.9", "pré-condição: o codec corrente emite a forma única");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            CorpusEnvelope.HashDocumento, CorpusEnvelope.Ator, TimeProvider.System);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao versao = publicacao.Value!;

        RestauradorDeConfiguracao restaurador = new(new RegistroCodecsEnvelope());

        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        // A APLICAÇÃO na raiz viva é do descarte (Story #986): limpa as coleções mutáveis e repõe o
        // grafo provado.
        processo.LimparColetaEDerivacaoParaRestauracao();
        processo.RestaurarConfiguracaoCongelada(versao, resultado.Value!).IsSuccess.Should().BeTrue();

        // A prova final: recanonicalizar o agregado reposto reproduz os MESMOS bytes —
        // incluindo o bloco metadadosFatos, que só sobreviveu porque veio inteiro dentro do
        // envelope decodificado (EnvelopeReidratado.MetadadosFatosCongelados), nunca porque
        // foi reconsultado.
        new EnvelopeCodec().Codificar(new EntradaCanonicalizacao(
            processo, dados, CorpusEnvelope.HashDocumento,
            MetadadosFatosCongelados: metadadosFatos,
            ValoresSelecionaveisCongelados: valoresSelecionaveis)).Bytes
            .Should().Equal(congelado.Bytes, "o agregado reposto recanonicaliza, byte a byte, o que a versão congelou");
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

        // Substitui a coleta pelo mesmo par {COR_RACA, RENDA} do corpus rico — SEM a
        // pré-condição de RENDA, irrelevante para esta prova — acrescido de
        // CONDICAO_ATENDIMENTO (escopo-processo, SELECAO_MULTIPLA).
        processo.DefinirFatosColetados([
            FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, obrigatorio: true, null).Value!,
            FatoColetado.Criar("RENDA", 1, "Faixa de renda familiar", TipoRenderizacao.SelecaoUnica, obrigatorio: false, null).Value!,
            FatoColetado.Criar("CONDICAO_ATENDIMENTO", 2, "Condição de atendimento", TipoRenderizacao.SelecaoMultipla, obrigatorio: false, null).Value!,
        ], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis =
            new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(CorpusEnvelope.ValoresSelecionaveisRicos())
            {
                // Escopo-processo: os códigos batem com a oferta de atendimento do corpus rico
                // (Pcd, LACTANTE), ordenados por CondicaoCodigo — "LACTANTE" antes de "PCD".
                ["CONDICAO_ATENDIMENTO"] =
                [
                    new ValorDominioDeclaradoCongelado("LACTANTE", "Lactante", 0),
                    new ValorDominioDeclaradoCongelado(OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência", 1),
                ],
            };

        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(
            new EntradaCanonicalizacao(processo, CorpusEnvelope.DadosRicos(), CorpusEnvelope.HashDocumento,
                ValoresSelecionaveisCongelados: valoresSelecionaveis));

        // Publica DIRETAMENTE com os bytes já codificados acima — CorpusEnvelope.Publicar(...)
        // recodificaria com CorpusEnvelope.ValoresSelecionaveisRicos() (sem CONDICAO_ATENDIMENTO)
        // e produziria um envelope diferente do que este teste está provando.
        processo.Publicar(
            CorpusEnvelope.DadosRicos(), congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            CorpusEnvelope.HashDocumento, CorpusEnvelope.Ator, TimeProvider.System).IsSuccess.Should().BeTrue();
        processo.ClearDomainEvents();

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? dicionarioReidratado =
            reidratado.Value!.ValoresSelecionaveisCongelados;
        dicionarioReidratado.Should().NotBeNull();

        // A afirmação do CONTEÚDO — não só que os bytes batem no final.
        dicionarioReidratado.Should().ContainKey("COR_RACA");
        dicionarioReidratado!["COR_RACA"].Should().NotBeNull("COR_RACA é estático, SELECAO_UNICA");
        dicionarioReidratado["COR_RACA"]!.Select(static v => v.Codigo).Should().Equal(["BRANCA", "PRETA", "PARDA"]);
        dicionarioReidratado["COR_RACA"]!.Select(static v => v.Descricao).Should().Equal([
            "Autodeclaração de cor/raça branca.", "Autodeclaração de cor/raça preta.", "Autodeclaração de cor/raça parda.",
        ]);

        dicionarioReidratado.Should().ContainKey("CONDICAO_ATENDIMENTO");
        dicionarioReidratado["CONDICAO_ATENDIMENTO"].Should().NotBeNull("CONDICAO_ATENDIMENTO é escopo-processo, SELECAO_MULTIPLA");
        dicionarioReidratado["CONDICAO_ATENDIMENTO"]!.Select(static v => v.Codigo).Should().Equal(
            ["LACTANTE", OfertaAtendimentoEspecializado.CodigoCondicaoPcd]);

        // As entradas de fatos não-seleção (RENDA continua SELECAO_UNICA no corpus rico — o
        // dicionário completo tem todas) sobrevivem também.
        dicionarioReidratado.Should().ContainKey("RENDA");

        // A prova final: repor e recanonicalizar reproduz os MESMOS bytes.
        RestauradorDeConfiguracao restaurador = new(CorpusEnvelope.Registro);
        Result<GrafoConfiguracao> resultado = restaurador.Restaurar(processo, versao);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        processo.LimparColetaEDerivacaoParaRestauracao();
        processo.RestaurarConfiguracaoCongelada(versao, resultado.Value!).IsSuccess.Should().BeTrue();

        CorpusEnvelope.Codec.Codificar(new EntradaCanonicalizacao(
                processo, CorpusEnvelope.DadosRicos(), CorpusEnvelope.HashDocumento,
                ValoresSelecionaveisCongelados: valoresSelecionaveis)).Bytes
            .Should().Equal(congelado.Bytes, "o agregado reposto recanonicaliza, byte a byte, o que a versão congelou");
    }

    [Fact(DisplayName = "Uma versão de schema desconhecido faz a restauração falhar sem tocar no agregado")]
    public void VersaoNaoReidratavel_Falha()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao10 = VersaoConfiguracao.Abrir(
            processo.Id,
            bytes,
            schemaVersion: "9.9",
            CorpusEnvelope.Codec.AlgoritmoHash,
            atoCriadorId: CorpusEnvelope.AtoAbertura,
            atoCriadorHash: CorpusEnvelope.HashDocumento,
            atorUsuarioSub: CorpusEnvelope.Ator,
            instante: DateTimeOffset.UnixEpoch);

        byte[] antes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;

        Result<GrafoConfiguracao> resultado = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(processo, versao10);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.VersaoDesconhecida);

        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes
            .Should().Equal(antes, "uma restauração recusada não altera a configuração");
    }
}
