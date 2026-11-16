namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.EnvelopeDeReferencia;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.FixtureDoEnvelope;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.NormalizadorDeIdsDoEnvelope;

/// <summary>
/// <b>Golden fixture do envelope canônico</b> (ADR-0109 D2/D3).
/// </summary>
/// <remarks>
/// <para>
/// O envelope é o artefato de maior peso jurídico do módulo — a evidência que
/// sustenta o resultado do certame — e era, até esta suíte, <b>o único contrato
/// do repositório sem gate de regressão</b>: <c>SnapshotVigenteDto.Configuracao</c>
/// é um <c>JsonNode</c>, e o schema gerado na baseline OpenAPI é literalmente
/// <c>"JsonNode": {}</c>. Um stub virando objeto rico, uma chave nova, uma troca
/// de <c>null</c> explícito por omissão: <b>nada disso aparece no diff da baseline</b>.
/// </para>
/// <para>
/// A fixture compara <b>byte a byte</b>. Os três canários provam que ela protege
/// de fato — uma fixture que não falha quando o envelope muda não é gate, é
/// decoração.
/// </para>
/// <para>
/// A projeção é pura (ADR-0109 D6), então esta suíte não precisa de banco.
/// </para>
/// </remarks>
public sealed class EnvelopeCanonicoGoldenTests
{
    // ── CA-03 — política: a versão declarada e a forma congelada andam juntas ──

    /// <summary>
    /// O conteúdo da fixture é o payload canônico puro, e a <c>schema_version</c> não aparece
    /// dentro dele — injetá-la ali faria a fixture deixar de ser byte-idêntica ao envelope real.
    /// Com nome de fixture fixo, quem prende a versão à forma é este arquivo: a versão corrente
    /// declarada ao lado das fixtures, comparada à do canonicalizador. Bumpar sem atualizá-lo
    /// quebra aqui, e mudar a forma sem regenerar quebra na comparação byte a byte — os dois
    /// modos de falha continuam cobertos.
    /// </summary>
    [Fact(DisplayName = "SchemaVersionDeclarada_BateComACanonicalizacao — bumpar a versão sem declarar a nova ao lado das fixtures quebra o build")]
    public void SchemaVersionDeclarada_BateComACanonicalizacao()
    {
        string versao = CanonicalizarReferencia().SchemaVersion;
        string caminho = CaminhoDaVersaoDeclarada();

        File.Exists(caminho).Should().BeTrue(
            $"a versão congelada nas golden fixtures é declarada em '{caminho}'");

        File.ReadAllText(caminho).Trim().Should().Be(versao,
            $"a schema_version corrente é '{versao}' e as fixtures ao lado congelam a forma dela. " +
            "Bumpar a versão sem regenerar as fixtures e atualizar esta declaração deixaria o " +
            "envelope declarando uma forma e congelando outra.");
    }

    // ── CA-04 — a fixture compara byte a byte ──

    [Fact(DisplayName = "Envelope_BateGoldenFixture — o envelope de referência é byte-idêntico à fixture congelada")]
    public void Envelope_BateGoldenFixture()
    {
        SnapshotCanonico canonico = CanonicalizarReferencia();
        string atual = NormalizarIds(canonico.Bytes);

        RegenerarSeSolicitado(atual, CaminhoDaFixtureNoFonte());

        atual.Should().Be(LerFixture(),
            "o envelope mudou de forma sem que a golden fixture fosse atualizada. Se a mudança é intencional, " +
            "rode UPDATE_ENVELOPE_FIXTURE=1 e leve o diff da fixture para a revisão — que é todo o ponto dela.");
    }

    /// <summary>
    /// Story #575: o bloco <c>cascataRemanejamento</c> saiu de stub (<c>nao_construido</c>) para
    /// bloco real com dois estados — <c>Envelope_BateGoldenFixture</c> acima já cobre a ausência
    /// (<c>{"presente":false}</c>, a forma do processo de referência); esta fixture congela a
    /// presença (<c>{"presente":true,...}</c>, matriz legal completa) byte a byte.
    /// </summary>
    [Fact(DisplayName = "Envelope_ComCascata_BateGoldenFixture — o envelope com cascata presente é byte-idêntico à fixture congelada")]
    public void Envelope_ComCascata_BateGoldenFixture()
    {
        SnapshotCanonico canonico = CanonicalizarReferenciaComCascata();
        string atual = NormalizarIds(canonico.Bytes);

        RegenerarSeSolicitado(atual, CaminhoDaFixtureCascataNoFonte());

        atual.Should().Be(LerFixtureCascata(),
            "o envelope com cascata presente mudou de forma sem que a golden fixture fosse atualizada.");
    }

    // ── CA-04 (canários) — a fixture PROTEGE de fato ──

    [Fact(DisplayName = "Canario_ChaveNova — acrescentar uma chave ao envelope faz a fixture falhar")]
    public void Canario_ChaveNova()
    {
        JsonObject adulterado = EnvelopeComoObjeto();
        adulterado["blocoIntruso"] = "x";

        BytesNormalizados(adulterado).Should().NotBe(LerFixture(),
            "uma chave nova no envelope TEM de fazer a fixture falhar — se não faz, a fixture não é um gate");
    }

    [Fact(DisplayName = "Canario_StubViraObjeto — um stub virando objeto rico faz a fixture falhar")]
    public void Canario_StubViraObjeto()
    {
        JsonObject adulterado = EnvelopeComoObjeto();
        adulterado["documentosExigidos"] = new JsonObject { ["exigencias"] = new JsonArray() };

        BytesNormalizados(adulterado).Should().NotBe(LerFixture(),
            "um stub que vira conteúdo real é mudança de FORMA — a fixture tem de acusar");
    }

    // `casasArredondamento` é `null` EXPLÍCITO no envelope de referência (classificação importada
    // não arredonda localmente). Omiti-la é exatamente a mudança que o item 4 da ADR-0100
    // prescreveria — e que o caminho do snapshot NÃO faz.
    [Fact(DisplayName = "Canario_NullVirandoOmissao — trocar null explícito por omissão faz a fixture falhar")]
    public void Canario_NullVirandoOmissao()
    {
        JsonObject adulterado = EnvelopeComoObjeto();

        adulterado["classificacao"]!.AsObject().Should().ContainKey("casasArredondamento",
            "pré-condição do canário: a chave tem de existir como null explícito para que removê-la seja um teste");
        adulterado["classificacao"]!.AsObject().Remove("casasArredondamento");

        BytesNormalizados(adulterado).Should().NotBe(LerFixture(),
            "o envelope preserva `null` explícito (ADR-0109 D4). Omitir a chave muda os bytes — e o hash. " +
            "É por isso que a emenda ao item 4 da ADR-0100 é restrita ao caminho de hash de ENTIDADE.");
    }

    // ── CA-05 — a ordem de INSERÇÃO não muda o envelope (chave de conteúdo) ──

    // DOIS processos com a MESMA configuração, mas com as regras de eliminação CRIADAS em ordem
    // inversa — logo com Guids v7 em ordem inversa. É isto que a ordenação por `Id` NÃO resolvia:
    // os Ids crescem com a ordem de criação, então o array sairia [corte, zero] num processo e
    // [zero, corte] no outro — bytes distintos para configurações equivalentes. Reusar as mesmas
    // entidades nos dois lados faria o teste passar com a implementação ANTIGA, e um teste que
    // não falha sem o fix não testa nada.
    [Fact(DisplayName = "Envelope_IndependeDaOrdemDeCriacao — duas configurações equivalentes produzem bytes idênticos")]
    public void Envelope_IndependeDaOrdemDeCriacao()
    {
        byte[] ordemA = CanonicalizarComEliminacoes(corteRedacaoPrimeiro: true);
        byte[] ordemB = CanonicalizarComEliminacoes(corteRedacaoPrimeiro: false);

        NormalizarIds(ordemB).Should().Be(NormalizarIds(ordemA),
            "duas configurações EQUIVALENTES têm de produzir o mesmo envelope. Ordenar por `Id` (Guid v7) fazia a " +
            "identidade técnica da linha vazar para dentro do hash. A ordenação é pela chave de CONTEÚDO (ADR-0109 D9).");
    }

    // ── CA-06 — determinismo ──

    [Fact(DisplayName = "Envelope_EDeterministico — canonicalizar duas vezes o mesmo agregado produz bytes idênticos")]
    public void Envelope_EDeterministico()
    {
        ProcessoSeletivo processo = ProcessoDeReferencia();
        DadosEdital dados = DadosDeReferencia();
        IReadOnlyDictionary<string, MetadadoFatoCongelado> metadados = MetadadosFatosDeReferencia();
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis = ValoresSelecionaveisDeReferencia();

        byte[] primeira = Canonicalizer.Canonicalizar(EntradaDeReferencia(processo, dados, metadados, valoresSelecionaveis)).Bytes;
        byte[] segunda = Canonicalizer.Canonicalizar(EntradaDeReferencia(processo, dados, metadados, valoresSelecionaveis)).Bytes;

        segunda.Should().Equal(primeira, "a projeção é pura — mesma entrada, mesmos bytes");
    }

    /// <summary>
    /// Processo com cálculo local e duas regras de eliminação sem chave de negócio natural
    /// (cardinalidade múltipla). O parâmetro inverte a ORDEM DE CRIAÇÃO das entidades — e, com
    /// ela, a ordem dos Guid v7. A configuração resultante é a mesma; só a identidade técnica
    /// das linhas difere.
    /// </summary>
    private static byte[] CanonicalizarComEliminacoes(bool corteRedacaoPrimeiro)
    {
        ProcessoSeletivo processo = ProcessoSiSuComEtapaEVagas();
        Result<ConfiguracaoClassificacao> classificacao = ClassificacaoComEliminacoes(corteRedacaoPrimeiro);
        classificacao.IsSuccess.Should().BeTrue(classificacao.Error?.Message);

        processo.DefinirClassificacao(classificacao.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeCurso(1, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return Canonicalizer.Canonicalizar(EntradaDeReferencia(
            processo, DadosDeReferencia(), valoresSelecionaveis: CatalogoDoConjuntoBasico.ComValoresCongelados())).Bytes;
    }

    // SiSU é baseado em ENEM — é o que admite as duas regras de eliminação.
    private static ProcessoSeletivo ProcessoSiSuComEtapaEVagas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Ordem", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([DistribuicaoDeReferencia()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    // As duas regras (ELIM-CORTE-REDACAO e ELIM-ZERO-EM-AREA) não referenciam etapa nos seus args:
    // o teste isola a única variável que quer variar, a ordem de criação. O cálculo local exige
    // regra de precisão declarada (INV-B8).
    private static Result<ConfiguracaoClassificacao> ClassificacaoComEliminacoes(bool corteRedacaoPrimeiro)
    {
        RegraEliminacao primeira = corteRedacaoPrimeiro ? CriarCorteNaRedacao() : CriarZeroEmArea();
        RegraEliminacao segunda = corteRedacaoPrimeiro ? CriarZeroEmArea() : CriarCorteNaRedacao();

        return ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, "b"),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "d"),
            casasArredondamento: 2,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "c"),
            regrasEliminacao: [primeira, segunda], baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao,
            quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo(inverterOrdem: !corteRedacaoPrimeiro), escalaNotaFinal: 100m);
    }

    private static RegraEliminacao CriarCorteNaRedacao() => RegraEliminacao.Criar(
        Regra(RegraEliminacaoCodigo.ElimCorteEmArea, "e"),
        new ArgsElimCorteEmArea("REDACAO", 400m)).Value!;

    private static RegraEliminacao CriarZeroEmArea() => RegraEliminacao.Criar(
        Regra(RegraEliminacaoCodigo.ElimZeroEmArea, "f"),
        new ArgsElimZeroEmArea()).Value!;
}
