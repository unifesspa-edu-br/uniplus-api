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

        Result resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes
            .Should().Equal(congelado.Bytes, "o agregado voltou a ser, byte a byte, o que a versão congelou");
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

        Result resultado = restaurador.Restaurar(processo, versao);

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

        Result resultado = restaurador.Restaurar(processo, versao);

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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Restaurador FAIXA_ETARIA", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
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
            regrasEliminacao: []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

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

        Result resultado = restaurador.Restaurar(processo, versao);

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
        EntradaCanonicalizacao entrada = new(processo, dados, CorpusEnvelope.HashDocumento, MetadadosFatosCongelados: metadadosFatos);
        SnapshotCanonico congelado = new EnvelopeCodecV13().Codificar(entrada);
        congelado.SchemaVersion.Should().Be("1.3", "pré-condição: metadadosFatos só existe a partir da 1.3");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            CorpusEnvelope.HashDocumento, CorpusEnvelope.Ator, TimeProvider.System);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao versao = publicacao.Value!;

        RestauradorDeConfiguracao restaurador = new(new RegistroCodecsEnvelope());

        Result resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        // A prova final: recanonicalizar o agregado reposto reproduz os MESMOS bytes —
        // incluindo o bloco metadadosFatos, que só sobreviveu porque veio inteiro dentro do
        // envelope decodificado (EnvelopeReidratado.MetadadosFatosCongelados), nunca porque
        // foi reconsultado.
        new EnvelopeCodecV13().Codificar(new EntradaCanonicalizacao(processo, dados, CorpusEnvelope.HashDocumento, MetadadosFatosCongelados: metadadosFatos)).Bytes
            .Should().Equal(congelado.Bytes, "o agregado reposto recanonicaliza, byte a byte, o que a versão congelou");
    }

    /// <summary>
    /// Toda versão anterior à 1.4 nunca serializou <c>arvoreSatisfacao</c> — o decoder dela
    /// devolve <c>NosExigencia</c> sempre vazio,
    /// mesmo com <c>documentosExigidos.exigencias</c> populado. Sem
    /// <c>RegistroCodecsEnvelope.SincronizarArvoreComDocumentosExigidos</c>, restaurar uma
    /// versão legada e republicá-la sob o encoder 1.4 emitiria <c>arvoreSatisfacao: []</c>
    /// enquanto a exigência continua viva em <c>documentosExigidos</c> — o resolvedor de
    /// satisfação (que opera sobre a árvore) veria zero obrigações documentais para um
    /// processo que na verdade tem uma exigência.
    /// </summary>
    [Fact(DisplayName = "Story #923: restaurar uma versão 1.3 (sem arvoreSatisfacao) sintetiza a raiz-folha e republica corretamente sob a 1.4")]
    public void Restaurar_VersaoLegadaSemArvoreSerializada_SintetizaRaizFolhaERepublicaSobA14()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        Guid faseId = processo.CronogramaFases.First().Id;

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, tipoDocumentoOrigemId: Guid.CreateVersion7(), tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade", tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        DadosEdital dados = CorpusEnvelope.DadosRicos();
        EntradaCanonicalizacao entrada = new(processo, dados, CorpusEnvelope.HashDocumento);
        SnapshotCanonico congelado = new EnvelopeCodecV13().Codificar(entrada);
        congelado.SchemaVersion.Should().Be("1.3", "pré-condição: a 1.3 nunca serializou arvoreSatisfacao");

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            CorpusEnvelope.HashDocumento, CorpusEnvelope.Ator, TimeProvider.System);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao versao = publicacao.Value!;

        RegistroCodecsEnvelope registro = new();
        RestauradorDeConfiguracao restaurador = new(registro);
        Result resultado = restaurador.Restaurar(processo, versao);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        NoExigencia raizSintetizada = processo.RaizesDeExigencia.Should().ContainSingle(
            "a versão 1.3 tinha uma exigência real, mesmo sem árvore serializada — a reidratação sintetiza a raiz " +
            "achatada pré-Story #920 em vez de deixar a árvore vazia").Which;
        raizSintetizada.Tipo.Should().Be(TipoNo.Folha);
        raizSintetizada.DocumentoExigido.Should().NotBeNull();
        raizSintetizada.DocumentoExigido!.TipoDocumentoCodigo.Should().Be("IDENTIDADE");

        SnapshotCanonico recodificadoSobA14 = new SnapshotPublicacaoCanonicalizer().Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, CorpusEnvelope.HashDocumento));
        recodificadoSobA14.SchemaVersion.Should().Be("1.4");

        JsonArray arvore = EnvelopeCodecRoundTripTests.Envelope(recodificadoSobA14)["arvoreSatisfacao"]!.AsArray();
        arvore.Should().ContainSingle(
            "sem a síntese, republicar este processo legado sob o encoder 1.4 emitiria arvoreSatisfacao vazio " +
            "enquanto documentosExigidos.exigencias continua com a exigência — perda silenciosa de obrigação " +
            "documental para o resolvedor de satisfação, que opera sobre a árvore");
    }

    /// <summary>
    /// A raiz sintetizada por <see cref="NoExigencia.SintetizarRaizesLegadas"/> tem de ter
    /// o mesmo Id em toda restauração da mesma versão — do contrário, cada descarte/
    /// restauração produziria um Id de nó diferente para a MESMA exigência congelada, e uma
    /// republicação subsequente sob a 1.4 bateria o acaso da última restauração no hash
    /// canônico, não o conteúdo real da configuração.
    /// </summary>
    [Fact(DisplayName = "Story #923: sintetizar a raiz-folha de uma versão 1.3 é determinístico — restaurar duas vezes produz o MESMO Id de nó")]
    public void Restaurar_VersaoLegadaSemArvoreSerializada_SinteseDeRaizEDeterministica()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        Guid faseId = processo.CronogramaFases.First().Id;

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId, tipoDocumentoOrigemId: Guid.CreateVersion7(), tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade", tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        DadosEdital dados = CorpusEnvelope.DadosRicos();
        EntradaCanonicalizacao entrada = new(processo, dados, CorpusEnvelope.HashDocumento);
        SnapshotCanonico congelado = new EnvelopeCodecV13().Codificar(entrada);

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            CorpusEnvelope.HashDocumento, CorpusEnvelope.Ator, TimeProvider.System);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        VersaoConfiguracao versao = publicacao.Value!;

        RegistroCodecsEnvelope registro = new();
        RestauradorDeConfiguracao restaurador = new(registro);

        restaurador.Restaurar(processo, versao).IsSuccess.Should().BeTrue();
        Guid idPrimeiraRestauracao = processo.RaizesDeExigencia.Single().Id;

        restaurador.Restaurar(processo, versao).IsSuccess.Should().BeTrue();
        Guid idSegundaRestauracao = processo.RaizesDeExigencia.Single().Id;

        idSegundaRestauracao.Should().Be(idPrimeiraRestauracao,
            "a raiz sintetizada usa o Id do próprio DocumentoExigido, não um Guid aleatório novo a cada chamada — " +
            "duas restaurações da mesma versão legada têm de produzir o MESMO Id de nó");
        idPrimeiraRestauracao.Should().Be(exigencia.Id,
            "a derivação determinística mais simples: a raiz sintetizada reusa o Id do DocumentoExigido como Id " +
            "do nó");
    }

    [Fact(DisplayName = "Uma versão que não reidrata (1.0) faz a restauração falhar sem tocar no agregado")]
    public void VersaoNaoReidratavel_Falha()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao10 = VersaoConfiguracao.Abrir(
            processo.Id,
            bytes,
            schemaVersion: "1.0",
            CorpusEnvelope.Codec.AlgoritmoHash,
            atoCriadorId: CorpusEnvelope.AtoAbertura,
            atoCriadorHash: CorpusEnvelope.HashDocumento,
            atorUsuarioSub: CorpusEnvelope.Ator,
            instante: DateTimeOffset.UnixEpoch);

        byte[] antes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;

        Result resultado = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(processo, versao10);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.VersaoNaoReidratavel);

        CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes
            .Should().Equal(antes, "uma restauração recusada não altera a configuração");
    }
}
