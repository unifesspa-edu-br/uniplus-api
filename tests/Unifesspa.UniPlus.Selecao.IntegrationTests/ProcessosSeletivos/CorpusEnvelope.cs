namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Corpus da reidratação — agregados <b>ricos</b>, com ids fixos e valores
/// deliberadamente <b>não-default</b>.
/// </summary>
/// <remarks>
/// <para>
/// A golden fixture da #842 <b>não serve de oráculo</b> aqui: ela normaliza os ids
/// voláteis para tokens (<c>&lt;&lt;id-1&gt;&gt;</c>, que não é um GUID e não decodifica) e
/// o agregado dela é pobre de propósito — atendimento vazio, sem bônus, sem desempate,
/// sem eliminações. Um decoder que perdesse metade dos campos passaria por ela.
/// </para>
/// <para>
/// <b>Todo valor aqui é não-default por desenho.</b> Um decoder que esquecesse um campo
/// e o deixasse cair no default (<c>null</c>, <c>0</c>, string vazia) produziria bytes
/// distintos dos congelados — e é exatamente isso que o round-trip acusa. Um corpus de
/// valores default tornaria o esquecimento invisível.
/// </para>
/// <para>
/// Os ids são <b>fixos</b> — inclusive os das etapas, via
/// <see cref="EtapaProcesso.Reidratar"/> — porque o <c>etapa.Id</c> é o único id de
/// entidade-filha que entra no envelope (ADR-0110 D2). Os das demais filhas não entram,
/// então a volatilidade delas não vaza para os bytes.
/// </para>
/// </remarks>
internal static class CorpusEnvelope
{
    internal static readonly EnvelopeCodec Codec = new();
    internal static readonly RegistroCodecsEnvelope Registro = new();

    internal const string HashDocumento = "1111111111111111111111111111111111111111111111111111111111111111";

    internal static readonly Guid AtoAbertura = new("01900000-0000-7000-8000-000000000001");
    internal static readonly Guid AtoRetificador = new("01900000-0000-7000-8000-000000000002");

    /// <summary>
    /// Ids de etapa <b>fixos</b> — é o que torna a golden fixture determinística (o
    /// <c>etapa.Id</c> é o único id de filha que entra no envelope, ADR-0110 D2).
    /// </summary>
    /// <remarks>
    /// A <paramref name="variante"/> existe só para os testes de <b>persistência</b>: eles
    /// compartilham um Postgres por classe, e dois processos com as mesmas etapas
    /// colidiriam na chave primária de <c>etapas_processo</c>. A variante 0 é a do envelope
    /// congelado — mudá-la mudaria a fixture.
    /// </remarks>
    private static Guid EtapaId(int ordem, int variante) =>
        new($"aaaa000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    private static readonly Guid OfertaMedicina = new("bbbb0000-0000-4000-8000-000000000001");
    private static readonly Guid OfertaDireito = new("bbbb0000-0000-4000-8000-000000000002");

    private static readonly Guid Documento = new("cccc0000-0000-4000-8000-000000000001");
    private static readonly Guid ReferenciaDemografica = new("dddd0000-0000-4000-8000-000000000001");
    private static readonly Guid UnidadeAdministradora = new("eeee0000-0000-4000-8000-000000000001");

    /// <summary>Sub do publicador — evidência forense, não input de negócio.</summary>
    internal const string Ator = "corpus-tests";

    internal static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    /// <summary>
    /// Permuta a ORDEM DE ENTRADA de uma coleção não ordenada, sem mudar o conteúdo (Story
    /// #928, §7.5): o mesmo conjunto de itens, apresentado ao agregado em ordem física
    /// inversa, tem de produzir bytes canônicos idênticos — a projeção ordena tudo por
    /// chave determinística antes de serializar. Compartilhado por toda coleção do corpus
    /// que declara <c>permutar</c>: etapas, distribuição de vagas, critérios de desempate,
    /// cronograma de fases, fatos coletados, regras de derivação, destinos da cascata
    /// (dentro de uma mesma origem) e a árvore de satisfação (raízes e filhos).
    /// </summary>
    private static IReadOnlyList<T> Ordem<T>(IReadOnlyList<T> itens, bool inverter) =>
        inverter ? [.. ((IEnumerable<T>)itens).Reverse()] : itens;

    /// <summary>
    /// O agregado mais rico que o modelo permite: três etapas (uma de cada caráter),
    /// atendimento povoado, bônus com teto, <b>as quatro</b> variantes de desempate,
    /// classificação local com <b>três</b> regras de eliminação — duas delas do
    /// <b>mesmo</b> código — e duas ofertas de curso, uma sob a Lei 12.711 (com
    /// referência demográfica e as nove modalidades federais) e outra institucional.
    /// </summary>
    /// <remarks>
    /// A multiplicidade é deliberada: um decoder que indexasse as regras <b>por código</b>
    /// — em vez de acumulá-las numa lista — perderia a segunda <c>ELIM-NOTA-MINIMA-ETAPA</c>
    /// e o segundo <c>DESEMPATE-MAIOR-NOTA-ETAPA</c> em silêncio. Um corpus com “uma de
    /// cada variante” não pegaria isso.
    /// </remarks>
    /// <param name="variante">Ver <see cref="EtapaId"/> — só distingue processos no mesmo Postgres entre testes de persistência.</param>
    /// <param name="permutar">Inverte a ordem de ENTRADA das coleções não ordenadas — nunca o conteúdo.</param>
    /// <param name="comArvoreSatisfacao">
    /// Opt-in: acrescenta a árvore de satisfação de documentos exigidos (<see cref="ArvoreSatisfacaoRica"/>).
    /// Fica fora por padrão porque as golden fixtures (<c>envelope-0.0.7-rico.json</c>) e os testes de
    /// round-trip congelam a forma de HOJE do corpus rico — sem árvore, <c>documentosExigidos.exigencias</c>
    /// e <c>arvoreSatisfacao</c> vazios. Populá-la incondicionalmente mudaria os bytes desse envelope de
    /// referência para todo consumidor de <see cref="ProcessoRico"/>, não só quem testa a permutação.
    /// </param>
    internal static ProcessoSeletivo ProcessoRico(int variante = 0, bool permutar = false, bool comArvoreSatisfacao = false)
    {
        Guid objetiva = EtapaId(1, variante);
        Guid redacao = EtapaId(2, variante);
        Guid entrevista = EtapaId(3, variante);

        // SiSU é baseado em ENEM — é o que admite ELIM-CORTE-REDACAO e ELIM-ZERO-EM-AREA.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Rico 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeAdministradora,
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);

        processo.DefinirEtapas(Ordem([
            EtapaProcesso.Reidratar(objetiva, "Prova Objetiva", CaraterEtapa.Ambas, peso: 3.5000m, notaMinima: 40.0000m, ordem: 1),
            EtapaProcesso.Reidratar(redacao, "Redação", CaraterEtapa.Classificatoria, peso: 2.2500m, notaMinima: null, ordem: 2),
            EtapaProcesso.Reidratar(entrevista, "Entrevista", CaraterEtapa.Eliminatoria, peso: null, notaMinima: 60.0000m, ordem: 3),
        ], permutar), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar(
            condicoes: Ordem<OfertaCondicao>([
                OfertaCondicao.Criar(new Guid("eeee0000-0000-4000-8000-000000000001"), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência"),
                OfertaCondicao.Criar(new Guid("eeee0000-0000-4000-8000-000000000002"), "LACTANTE", "Lactante"),
            ], permutar),
            recursos: Ordem<OfertaRecurso>([
                OfertaRecurso.Criar(new Guid("ffff0000-0000-4000-8000-000000000001"), "Ledor"),
                OfertaRecurso.Criar(new Guid("ffff0000-0000-4000-8000-000000000002"), "Prova ampliada"),
            ], permutar),
            tiposDeficiencia: Ordem<OfertaTipoDeficiencia>([
                OfertaTipoDeficiencia.Criar(new Guid("1111aaaa-0000-4000-8000-000000000001"), "Deficiência visual"),
                OfertaTipoDeficiencia.Criar(new Guid("1111aaaa-0000-4000-8000-000000000002"), "Deficiência auditiva"),
            ], permutar)).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas(Ordem([DistribuicaoLei12711(), DistribuicaoInstitucional()], permutar), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirBonusRegional(ConfiguracaoBonusRegional.Criar(
            Regra(RegraBonusCodigo.Multiplicativo, 'b'),
            fator: 1.2000m,
            teto: 95.5000m,
            municipioConvenio: "Marabá",
            baseLegal: "Res. Unifesspa 414/2020").Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // As QUATRO variantes de args — e DUAS do mesmo código (MAIOR-NOTA-ETAPA em
        // ordens distintas), que um decoder indexado por código colapsaria em uma.
        processo.DefinirCriteriosDesempate(Ordem([
            CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.Idoso, 'c'), new ArgsDesempateIdoso(60)).Value!,
            CriterioDesempate.Criar(2, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'), new ArgsDesempateMaiorNotaEtapa(objetiva)).Value!,
            CriterioDesempate.Criar(3, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'), new ArgsDesempateMaiorNotaEtapa(redacao)).Value!,
            CriterioDesempate.Criar(4, Regra(CriterioDesempateCodigo.PredicadoFato, 'e'), new ArgsDesempatePredicadoFato(
                CondicaoDnf.Criar("escola_publica", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)).Value!,
            CriterioDesempate.Criar(5, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!,
        ], permutar), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoArredondarCima, '2'),
            casasArredondamento: 3,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, '3'),
            nOpcoesAlocacao: 2,
            regrasEliminacao: [
                // DUAS do mesmo código, args distintos — o PS Convênios exige exatamente isso.
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, '4'), new ArgsElimNotaMinimaEtapa(objetiva, 45.0000m)).Value!,
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, '4'), new ArgsElimNotaMinimaEtapa(redacao, 30.5000m)).Value!,
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimCorteRedacao, '5'), new ArgsElimCorteRedacao(400.0000m)).Value!,
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimZeroEmArea, '6'), new ArgsElimZeroEmArea()).Value!,
            ],
            baseadoEmEnem: true).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases(
            Ordem([FaseInscricao(variante), FaseResultadoPreliminarComRecurso(variante, permutar)], permutar), [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        // Coleta de fatos + derivação de MODALIDADE (Story #928, §7.4): COR_RACA é coletado sem
        // gate; RENDA é gatado por COR_RACA (pré-condição); MODALIDADE é derivado — âncora AC e a
        // regra que contribui LB_PPI quando COR_RACA=PRETA E RENDA=ATE_1_SM. Exercita, no mesmo
        // snapshot, as arestas de produção, pré-condição e derivação, com predicado DNF de duas
        // condições numa cláusula. Ambos os códigos contribuídos (AC, LB_PPI) são ofertados.
        processo.DefinirFatosColetados(Ordem([
            FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, obrigatorio: true, null).Value!,
            FatoColetado.Criar("RENDA", 1, "Faixa de renda familiar", TipoRenderizacao.SelecaoUnica, obrigatorio: false, [
                CondicaoPrecondicaoFato.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!,
            ]).Value!,
        ], permutar), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Formulário de inscrição (Story #559): título e termo de aceite não-default — o
        // decoder tem de reconstruir os dois escalares a partir do bloco "formulario", que deixou
        // de ser stub nesta Story.
        processo.DefinirFormulario(
            "Formulário de Inscrição — PS Rico 2026",
            "Declaro que as informações prestadas são verdadeiras, sob pena de eliminação do certame.",
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirRegrasDerivacao(Ordem([
            ConfiguracaoDerivacaoFato.Criar("MODALIDADE", Ordem([
                RegraDerivacaoConfigurada.Criar(0, "AC", null).Value!,
                RegraDerivacaoConfigurada.Criar(1, "LB_PPI", Ordem([
                    CondicaoRegraDerivacao.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!,
                    CondicaoRegraDerivacao.Criar(0, "RENDA", Operador.Igual, JsonSerializer.SerializeToElement("ATE_1_SM")).Value!,
                ], permutar)).Value!,
            ], permutar)).Value!,
            // Segundo fato derivado (não MODALIDADE) — a lista EXTERNA de configurações de derivação é
            // ordenada por codigoFato na projeção; sem dois itens, a permutação da lista externa não
            // seria exercitada. Cita COR_RACA (coletado), sem ciclo; domínio próprio, não o de MODALIDADE.
            ConfiguracaoDerivacaoFato.Criar("REGIME_INGRESSO", [
                RegraDerivacaoConfigurada.Criar(0, "AMPLA", null).Value!,
                RegraDerivacaoConfigurada.Criar(1, "COTISTA", [
                    CondicaoRegraDerivacao.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!,
                ]).Value!,
            ]).Value!,
        ], permutar), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Cascata de remanejamento (Story #575): as 8 modalidades federais de
        // DistribuicaoLei12711 já são SegueCascata (INV-12) — sem uma cascata que as cubra,
        // Publicar() recusa com ProcessoSeletivo.CascataOrigemAusente (PendenciaDaCascata).
        // A matriz legal completa (8×7, fallback AC) é o que mantém este corpus PUBLICÁVEL.
        processo.DefinirCascataRemanejamento(CascataLegalCompleta(permutar), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Árvore de satisfação de documentos exigidos (Story #920) — opt-in (ver o parâmetro
        // comArvoreSatisfacao acima): incluí-la incondicionalmente mudaria o envelope de
        // referência que as golden fixtures e os testes de round-trip congelam.
        if (comArvoreSatisfacao)
        {
            processo.DefinirDocumentosExigidos(ArvoreSatisfacaoRica(variante, permutar), PrecondicaoIfMatch.Ausente)
                .IsSuccess.Should().BeTrue();
        }

        return processo;
    }

    /// <summary>
    /// A matriz legal completa (8×7), fallback AC — mesma forma semeada em
    /// REMANEJ-CASCATA-LEI-12711 v1. <paramref name="permutar"/> inverte a ordem de ENTRADA dos
    /// destinos DENTRO de cada origem — nunca a ordem das origens em si: cada origem aparece uma
    /// única vez na cascata, então permutar a sequência de origens não exercitaria a ordenação de
    /// <c>cascataRemanejamento…destinos</c> — só a dos destinos de uma MESMA origem prova isso.
    /// </summary>
    private static ConfiguracaoCascataRemanejamento CascataLegalCompleta(bool permutar = false)
    {
        IReadOnlyList<string> origens = ModalidadesFederaisLei12711.Codigos;
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in origens)
        {
            string[] destinosDaOrigem = [.. origens.Where(o => o != origem)];
            List<DestinoRemanejamento> destinosDestaOrigem = [];
            for (int i = 0; i < destinosDaOrigem.Length; i++)
            {
                destinosDestaOrigem.Add(DestinoRemanejamento.Criar(origem, i + 1, destinosDaOrigem[i]).Value!);
            }

            destinos.AddRange(Ordem(destinosDestaOrigem, permutar));
        }

        return ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, '1'), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    /// <summary>
    /// A árvore de satisfação exercitada pela prova de permutação (Story #928, §7.5, issue
    /// #1087): DUAS raízes — uma folha solteira (<c>HISTORICO_ESCOLAR</c>, exigência "solteira",
    /// sem grupo) e um grupo <c>OU</c> com DOIS filhos (comprovação de renda por declaração de
    /// imposto de renda OU extrato bancário). Duas raízes de um filho cada deixariam a coleção
    /// <c>filhos</c> trivial — qualquer ordenação produziria o mesmo resultado, e a permutação
    /// não provaria nada sobre a ordenação de <c>arvoreSatisfacao[].filhos</c>.
    /// </summary>
    /// <remarks>
    /// Ids <b>fixos</b>, via <c>Reidratar</c> — mesma razão de <see cref="EtapaId"/>: tanto
    /// <c>DocumentoExigido.Id</c> quanto <c>NoExigencia.Id</c> entram no envelope
    /// (<c>exigenciaId</c>/<c>id</c>). <c>Criar</c> sorteia um Guid v7 novo a cada chamada — duas
    /// montagens desta árvore (uma para o processo direto, outra para o permutado) produziriam
    /// ids distintos e os bytes nunca bateriam, mesmo sem nenhuma diferença de ORDEM.
    /// </remarks>
    private static IReadOnlyList<NoExigencia> ArvoreSatisfacaoRica(int variante, bool permutar)
    {
        Guid faseInscricaoId = FaseInscricao(variante).Id;

        DocumentoExigido historicoEscolar = DocumentoExigidoRico(
            DocumentoExigidoIdFixo(1, variante), faseInscricaoId, TipoDocumentoOrigemIdFixo(1), "HISTORICO_ESCOLAR",
            "Histórico escolar do ensino médio", "ACADEMICO", obrigatorio: true);
        DocumentoExigido declaracaoImpostoRenda = DocumentoExigidoRico(
            DocumentoExigidoIdFixo(2, variante), faseInscricaoId, TipoDocumentoOrigemIdFixo(2), "DECLARACAO_IMPOSTO_RENDA",
            "Declaração de Imposto de Renda", "RENDA", obrigatorio: false);
        DocumentoExigido extratoBancario = DocumentoExigidoRico(
            DocumentoExigidoIdFixo(3, variante), faseInscricaoId, TipoDocumentoOrigemIdFixo(3), "EXTRATO_BANCARIO",
            "Extrato bancário dos últimos três meses", "RENDA", obrigatorio: false);

        NoExigencia raizSolteira = NoExigencia.Reidratar(
            NoExigenciaIdFixo(1, variante), TipoNo.Folha, ordem: 0,
            documentoExigidoId: historicoEscolar.Id, documentoExigido: historicoEscolar,
            quantidadeMinima: NoExigencia.QuantidadeMinimaPadrao, consequencia: null, chaveDistincao: null,
            dataReferencia: null, ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [], filhos: []);
        NoExigencia filhoDeclaracao = NoExigencia.Reidratar(
            NoExigenciaIdFixo(2, variante), TipoNo.Folha, ordem: 0,
            documentoExigidoId: declaracaoImpostoRenda.Id, documentoExigido: declaracaoImpostoRenda,
            quantidadeMinima: NoExigencia.QuantidadeMinimaPadrao, consequencia: null, chaveDistincao: null,
            dataReferencia: null, ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [], filhos: []);
        NoExigencia filhoExtrato = NoExigencia.Reidratar(
            NoExigenciaIdFixo(3, variante), TipoNo.Folha, ordem: 1,
            documentoExigidoId: extratoBancario.Id, documentoExigido: extratoBancario,
            quantidadeMinima: NoExigencia.QuantidadeMinimaPadrao, consequencia: null, chaveDistincao: null,
            dataReferencia: null, ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [], filhos: []);
        NoExigencia grupoRenda = NoExigencia.Reidratar(
            NoExigenciaIdFixo(4, variante), TipoNo.GrupoOu, ordem: 1,
            documentoExigidoId: null, documentoExigido: null,
            quantidadeMinima: 1, consequencia: null, chaveDistincao: null, dataReferencia: null,
            ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [],
            filhos: Ordem([filhoDeclaracao, filhoExtrato], permutar));

        return Ordem([raizSolteira, grupoRenda], permutar);
    }

    private static Guid TipoDocumentoOrigemIdFixo(int indice) =>
        new($"77771111-0000-4000-8000-00000000000{indice:x}");

    private static Guid DocumentoExigidoIdFixo(int ordem, int variante) =>
        new($"7777200{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    private static Guid NoExigenciaIdFixo(int ordem, int variante) =>
        new($"7777300{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    private static DocumentoExigido DocumentoExigidoRico(
        Guid id, Guid exigidoNaFaseId, Guid tipoDocumentoOrigemId, string tipoDocumentoCodigo,
        string tipoDocumentoNome, string tipoDocumentoCategoria, bool obrigatorio) =>
        DocumentoExigido.Reidratar(
            id,
            exigidoNaFaseId,
            tipoDocumentoOrigemId: tipoDocumentoOrigemId,
            tipoDocumentoCodigo: tipoDocumentoCodigo,
            tipoDocumentoNome: tipoDocumentoNome,
            tipoDocumentoCategoria: tipoDocumentoCategoria,
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: obrigatorio,
            consequenciaIndeferimento: null,
            grupoSatisfacaoId: null,
            condicoes: [],
            basesLegais: [],
            idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!,
            tamanhoMaximoBytes: null);

    /// <summary>Fase 1: coleta inscrição, sem ato produzido — a origem é InscricaoPropria.</summary>
    private static FaseCronograma FaseInscricao(int variante = 0) => FaseCronograma.Reidratar(
        id: new Guid($"6666aaaa-000{variante:x}-4000-8000-000000000001"),
        ordem: 1,
        faseCanonicaOrigemId: new Guid("4444dddd-0000-4000-8000-000000000001"),
        codigo: "INSCRICAO",
        donoInstitucional: "CRCA",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: false,
        permiteComplementacao: true,
        produzResultado: false,
        resultadoDefinitivo: false,
        coletaInscricao: true,
        inicio: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 3, 20, 23, 59, 59, TimeSpan.Zero),
        atoProduzidoCodigo: null,
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null);

    /// <summary>
    /// Fase 2: agrupa as três etapas, produz o resultado preliminar (efeito irreversível),
    /// exige duas bancas e admite recurso — os DOIS pares de suspensividade exercitados: a
    /// 1ª instância com valor (5 dias corridos), a 2ª <b>nula</b> (não bloqueia — o caso
    /// normal do Ingresso via judicial). É o ramo mais rico do bloco <c>cronogramaFases</c>.
    /// </summary>
    /// <param name="variante">Ver <see cref="EtapaId"/> — só distingue processos no mesmo Postgres entre testes de persistência.</param>
    /// <param name="permutar">Inverte a ordem de ENTRADA das bancas requeridas desta fase — nunca o conteúdo.</param>
    private static FaseCronograma FaseResultadoPreliminarComRecurso(int variante = 0, bool permutar = false) => FaseCronograma.Reidratar(
        id: new Guid($"6666aaaa-000{variante:x}-4000-8000-000000000002"),
        ordem: 2,
        faseCanonicaOrigemId: new Guid("4444dddd-0000-4000-8000-000000000002"),
        codigo: "RESULTADO_PRELIMINAR",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: false,
        produzResultado: true,
        resultadoDefinitivo: false,
        coletaInscricao: false,
        inicio: new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 3, 25, 18, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo: "RESULTADO_PRELIMINAR",
        atoProduzidoEfeitoIrreversivel: true,
        bancasRequeridas: Ordem<BancaRequerida>([
            BancaRequerida.Criar(new Guid("5555eeee-0000-4000-8000-000000000001"), "BANCA_ANALISE_DOCUMENTAL"),
            BancaRequerida.Criar(new Guid("5555eeee-0000-4000-8000-000000000002"), "BANCA_HETEROIDENTIFICACAO"),
        ], permutar),
        regraRecurso: RegraRecursoFase.Criar(
            Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, '9'),
            new ArgsRegraPrazoRecurso(
                PrazoValor: 48.0000m,
                PrazoUnidade: UnidadePrazo.Horas,
                AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
                SuspensividadePrimeiraInstanciaValor: 5.0000m,
                SuspensividadePrimeiraInstanciaUnidade: UnidadePrazo.Dias,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null)).Value!);

    /// <summary>
    /// Oferta sob a Lei 12.711: exige a referência demográfica (INV-5) e as 8 modalidades
    /// federais + AC (INV-6). É o ramo com <c>referenciaDemografica</c> preenchida.
    /// </summary>
    private static ConfiguracaoDistribuicaoVagas DistribuicaoLei12711()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            ModalidadeSelecionada.Criar(
                new Guid("2222bbbb-0000-4000-8000-000000000001"), "AC", "Ampla concorrência",
                NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                criteriosCumulativos: [], acaoQuandoIndeferido: null,
                baseLegal: "Lei 12.711/2012 art. 1º").Value!,
        ];

        // As 8 federais. Todas cota reservada ⇒ INV-12 obriga SEGUE_CASCATA.
        (string Codigo, char Semente)[] federais =
        [
            ("LB_PPI", 'g'), ("LB_Q", 'h'), ("LB_PCD", 'i'), ("LB_EP", 'j'),
            ("LI_PPI", 'k'), ("LI_Q", 'l'), ("LI_PCD", 'm'), ("LI_EP", 'n'),
        ];

        int sufixo = 2;
        foreach ((string codigo, char _) in federais)
        {
            modalidades.Add(ModalidadeSelecionada.Criar(
                new Guid($"2222bbbb-0000-4000-8000-00000000000{sufixo:x}"),
                codigo,
                $"Reserva {codigo}",
                NaturezaLegalModalidade.CotaReservada,
                ComposicaoVagasModalidade.DentroDoVr,
                null,
                RegraRemanejamentoModalidade.SegueCascata,
                null,
                null,
                null,
                // Dois critérios FORA da ordem alfabética de propósito (issue #1067): o
                // encoder ordena este array pela chave de conteúdo — é um conjunto, sem
                // posição própria entre os critérios — então o envelope sai com
                // "egresso_escola_publica" antes de "renda_per_capita_ate_1sm",
                // independente da ordem declarada aqui. Um decoder que preservasse esta
                // ordem de ENTRADA em vez de ler a canônica já gravada produziria bytes
                // distintos dos congelados.
                criteriosCumulativos: codigo.StartsWith("LB", StringComparison.Ordinal)
                    ? ["renda_per_capita_ate_1sm", "egresso_escola_publica"]
                    : ["egresso_escola_publica"],
                acaoQuandoIndeferido: "RECLASSIFICAR_AC",
                baseLegal: "Lei 12.711/2012, alterada pela Lei 14.723/2023").Value!);
            sufixo++;
        }

        return ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: OfertaMedicina,
            voBase: 60,
            pr: 0.7500m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Lei12711, '7'),
            regraAjuste: Regra(RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico, '0'),
            referenciaDemografica: ReferenciaReservaDemograficaSnapshot.Criar(
                ReferenciaDemografica,
                censoReferencia: "Censo IBGE 2022",
                ppiPercentual: 78.55m,
                quilombolaPercentual: 1.20m,
                pcdPercentual: 8.40m,
                baseLegal: "Lei 12.711/2012 art. 3º").Value!,
            modalidades: modalidades).Value!;
    }

    /// <summary>
    /// Oferta institucional: quadro fixo, <b>sem</b> referência demográfica — e com as
    /// variantes de remanejamento que a federal não usa (<c>DESTINO_UNICO</c> e
    /// <c>CRUZADO</c>, com par e fallback) e a composição <c>RETIRA_DE</c>.
    /// </summary>
    private static ConfiguracaoDistribuicaoVagas DistribuicaoInstitucional() =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: OfertaDireito,
            voBase: 40,
            pr: 0.5000m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, '8'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000001"), "AC", null,
                    NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                    RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                    [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 15).Value!,
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000002"), "V", "PcD em ampla concorrência",
                    NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, null,
                    RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null,
                    ["laudo_medico"], "RECLASSIFICAR_AC", "Lei 13.146/2015", quantidadeDeclarada: 5).Value!,
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000003"), "IND", "Indígena",
                    NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe, "AC",
                    RegraRemanejamentoModalidade.Cruzado, null, "QUI", "AC",
                    ["autodeclaracao", "documento_funai"], "RECLASSIFICAR_REGRA_EDITAL", "Res. Unifesspa 326/2019", quantidadeDeclarada: 10).Value!,
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000004"), "QUI", "Quilombola",
                    NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe, "AC",
                    RegraRemanejamentoModalidade.Cruzado, null, "IND", "AC",
                    ["autodeclaracao"], "RECLASSIFICAR_REGRA_EDITAL", "Res. Unifesspa 326/2019", quantidadeDeclarada: 10).Value!,
            ]).Value!;

    internal static DadosEdital DadosRicos() => DadosEdital.Criar(
        numero: "042/2026",
        periodoInscricaoInicio: new DateOnly(2026, 3, 2),
        periodoInscricaoFim: new DateOnly(2026, 4, 15),
        documentoEditalId: Documento).Value!;

    /// <summary>
    /// Os valores selecionáveis dos dois fatos coletados de <see cref="ProcessoRico"/>
    /// (COR_RACA, RENDA — ambos <c>SELECAO_UNICA</c>, UNI-REQ-0072): totalidade exigida pelo
    /// encoder (D1-ter) desde que qualquer processo colete um fato de seleção. Ordens não-zero
    /// e fora de ordem de código de propósito — a ordenação canônica (Ordem, depois Codigo) é
    /// responsabilidade do ENCODER, não de quem monta este dicionário.
    /// </summary>
    /// <param name="permutarValores">
    /// Inverte a ordem de inserção de cada lista sem mudar o conteúdo — a prova de que os
    /// bytes finais dependem só do <c>Ordem</c>/<c>Codigo</c> de cada valor, nunca da ordem em
    /// que a lista chegou ao encoder.
    /// </param>
    internal static IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveisRicos(
        bool permutarValores = false)
    {
        List<ValorDominioDeclaradoCongelado> corRaca =
        [
            new ValorDominioDeclaradoCongelado("BRANCA", "Autodeclaração de cor/raça branca.", 0),
            new ValorDominioDeclaradoCongelado("PRETA", "Autodeclaração de cor/raça preta.", 1),
            new ValorDominioDeclaradoCongelado("PARDA", "Autodeclaração de cor/raça parda.", 2),
        ];
        List<ValorDominioDeclaradoCongelado> renda =
        [
            new ValorDominioDeclaradoCongelado("ATE_1_SM", "Renda familiar per capita de até 1 salário mínimo.", 0),
            new ValorDominioDeclaradoCongelado("ACIMA_1_SM", "Renda familiar per capita acima de 1 salário mínimo.", 1),
        ];

        if (permutarValores)
        {
            corRaca.Reverse();
            renda.Reverse();
        }

        return new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
        {
            ["COR_RACA"] = corRaca,
            ["RENDA"] = renda,
        };
    }

    internal static EntradaCanonicalizacao Entrada(
        ProcessoSeletivo processo,
        RetificacaoInfo? retificacao = null,
        ResultadoConformidade? conformidade = null,
        bool permutarValoresSelecionaveis = false) =>
        new(
            processo, DadosRicos(), HashDocumento, retificacao, conformidade,
            ValoresSelecionaveisCongelados: ValoresSelecionaveisRicos(permutarValoresSelecionaveis));

    /// <summary>
    /// Uma <see cref="VersaoConfiguracao"/> montada com <b>ids de ato fixos</b>, para que
    /// o envelope da versão N &gt; 1 — que carrega o 18º bloco com o id do ato retificado —
    /// seja determinístico. Passar por <c>Publicar</c>/<c>Retificar</c> geraria Guid v7 a
    /// partir do relógio, e a golden fixture rica não teria como existir.
    /// </summary>
    /// <remarks>
    /// O <paramref name="ato"/> é parametrizável pela mesma razão que o id da etapa: os
    /// testes de persistência compartilham um Postgres, e
    /// <c>ux_versoes_configuracao_ato_criador</c> garante que um ato cria <b>no máximo
    /// uma</b> versão. O default é o ato fixo do envelope congelado.
    /// </remarks>
    internal static VersaoConfiguracao VersaoDeAbertura(ProcessoSeletivo processo, byte[] bytes, Guid? ato = null) =>
        VersaoConfiguracao.Abrir(
            processo.Id,
            bytes,
            Codec.SchemaVersion,
            Codec.AlgoritmoHash,
            atoCriadorId: ato ?? AtoAbertura,
            atoCriadorHash: HashDocumento,
            atorUsuarioSub: Ator,
            instante: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    internal static VersaoConfiguracao VersaoDeRetificacao(VersaoConfiguracao anterior, byte[] bytes) =>
        VersaoConfiguracao.Suceder(
            anterior,
            bytes,
            Codec.SchemaVersion,
            Codec.AlgoritmoHash,
            atoCriadorId: AtoRetificador,
            atoCriadorHash: HashDocumento,
            atoCriadorRetificaId: anterior.AtoCriadorId,
            atorUsuarioSub: Ator,
            instante: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// Publica o processo para que ele fique em <see cref="StatusProcesso.Publicado"/> —
    /// o estado que <see cref="ProcessoSeletivo.RestaurarConfiguracaoCongelada"/> exige,
    /// porque só um processo publicado tem versão congelada a restaurar.
    /// </summary>
    internal static void Publicar(ProcessoSeletivo processo)
    {
        SnapshotCanonico snapshot = Codec.Codificar(Entrada(processo));
        processo.Publicar(
            DadosRicos(),
            snapshot.Bytes,
            snapshot.SchemaVersion,
            snapshot.AlgoritmoHash,
            HashDocumento,
            Ator,
            TimeProvider.System).IsSuccess.Should().BeTrue();
        processo.ClearDomainEvents();
    }

    /// <summary>
    /// A sessão editorial que <b>altera os dados</b> da primeira etapa <b>preservando o
    /// Id</b> — o cenário em que a reconciliação de fato acontece (a mesma linha, dados
    /// novos). Sem ele, o teste de <c>CreatedAt</c> passaria até com uma reposição que não
    /// fizesse nada.
    /// </summary>
    internal static GrafoConfiguracao GrafoComEtapaAlterada(int variante)
    {
        ProcessoSeletivo rico = ProcessoRico(variante);
        EtapaProcesso primeira = rico.Etapas.First();

        return new GrafoConfiguracao(
            etapas: [EtapaProcesso.Reidratar(
                primeira.Id,
                "Etapa Descaracterizada",
                CaraterEtapa.Classificatoria,
                peso: 9.9999m,
                notaMinima: null,
                ordem: 7)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [DistribuicaoInstitucional()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: ConfiguracaoClassificacao.Criar(
                regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
                regraArredondamento: null,
                casasArredondamento: null,
                regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, '3'),
                nOpcoesAlocacao: 1,
                regrasEliminacao: [], baseadoEmEnem: false).Value!,
            cronogramaFases: [FaseCronogramaConforme(variante)],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null,
            fatosColetados: [],
            regrasDerivacao: []);
    }

    /// <summary>Um grafo mínimo e conforme — a "sessão editorial" que o descarte terá de desfazer.</summary>
    internal static GrafoConfiguracao GrafoPobre(int variante = 0) => new(
        etapas: [EtapaProcesso.Reidratar(new Guid($"9999fff{variante:x}-0000-4000-8000-000000000001"), "Etapa Única", CaraterEtapa.Classificatoria, 1.0000m, null, 1)],
        ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
        distribuicaoVagas: [DistribuicaoInstitucional()],
        bonusRegional: null,
        criteriosDesempate: [],
        classificacao: ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, '3'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [], baseadoEmEnem: false).Value!,
        cronogramaFases: [FaseCronogramaConforme(variante)],
        documentosExigidos: [],
        nosExigencia: [],
        referenciaTemporalFatos: null,
        fatosColetados: [],
        regrasDerivacao: []);

    /// <summary>Fase mínima que satisfaz o bicondicional fase×etapa (uma etapa acompanha os dois grafos acima).</summary>
    private static FaseCronograma FaseCronogramaConforme(int variante) => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: new Guid($"9999eee{variante:x}-0000-4000-8000-000000000001"),
        codigo: "RESULTADO_FINAL",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: false,
        produzResultado: true,
        resultadoDefinitivo: true,
        coletaInscricao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo: "RESULTADO_FINAL",
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;
}
