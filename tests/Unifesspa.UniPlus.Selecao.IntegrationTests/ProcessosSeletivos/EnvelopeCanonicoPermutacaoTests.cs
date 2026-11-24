namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// <b>Invariância à permutação</b> (Story #928, §7.5; issue #1087): o envelope canônico depende só
/// do <b>conteúdo</b> da configuração, nunca da ordem em que as coleções não ordenadas chegam ao
/// agregado. Permutar a ordem de entrada de etapas, do atendimento especializado (condições, recursos e
/// tipos de deficiência), distribuição de vagas e ofertas, critérios de desempate, cronograma de
/// fases (inclusive as bancas requeridas de uma fase), fatos coletados, regras de derivação (a
/// lista externa, as regras internas de uma configuração e as condições dentro de uma regra),
/// destinos da cascata de remanejamento (dentro de uma mesma origem) e as regras de comprovação e os itens dos grupos — sem mudar nenhum valor — SHALL produzir os mesmos bytes
/// canônicos e o mesmo hash.
/// </summary>
/// <remarks>
/// <para>
/// A projeção ordena cada coleção por uma chave determinística (a <c>Ordem</c> onde é semântica, a
/// identidade de negócio onde existe, a chave de conteúdo onde não há chave natural), então duas
/// entradas equivalentes convergem para os mesmos bytes. O determinismo do grafo conjunto e da
/// identidade canônica é provado no grão do value object (<c>GrafoDependenciaConjuntaTests</c>,
/// <c>IdCanonicoTests</c>); aqui a prova é sobre os <b>bytes do envelope inteiro</b>.
/// </para>
/// <para>
/// <c>atendimentoEspecializado</c> (condições/recursos/tipos de deficiência) e
/// <c>cronogramaFases.fases[].bancasRequeridas</c> ordenam pela IDENTIDADE DE ORIGEM do item
/// (<c>CondicaoOrigemId</c>/<c>RecursoOrigemId</c>/<c>TipoDeficienciaOrigemId</c>)
/// — a segunda camada da política de ordenação, não a chave de conteúdo (ADR-0109 D9, a terceira e
/// última camada, reservada a coleções sem identidade de negócio nenhuma). As duas entram nesta
/// prova (a pré-condição correspondente confere que a permutação de fato inverte a ordem de
/// entrada de cada uma).
/// </para>
/// <para>
/// FORA desta prova, por decisão: <c>modalidades[].regraDeAplicabilidade</c> e
/// <c>classificacao.regrasEliminacao</c> — as duas coleções que não têm identidade de negócio nem
/// de origem entre os elementos, e por isso o encoder ordena pela CHAVE DE CONTEÚDO do próprio
/// item (ADR-0109 D9). <c>classificacao.regrasEliminacao</c> tem prova própria —
/// <c>EnvelopeCanonicoGoldenTests.Envelope_IndependeDaOrdemDeCriacao</c> varia a ordem de CRIAÇÃO
/// de duas regras de eliminação (e, com ela, a ordem dos Guid v7) e prova que o envelope resultante
/// não muda. As cláusulas e condições de um predicado têm prova própria em
/// <c>PredicadoDnfOrdenacaoCanonicaTests</c>.
/// </para>
/// <para>
/// <c>documentosExigidos.exigencias</c> também fica FORA, por um motivo distinto dos dois acima: a
/// chave PRIMÁRIA de ordenação é de negócio parcial — fase mais tipo de documento, ver
/// <c>SerializacaoDasExigencias.SerializarExigencias</c> — não a identidade de origem nem a
/// chave de conteúdo; a chave de conteúdo só desempata o caso raro de duas exigências idênticas na
/// mesma fase e no mesmo tipo. Como a chave primária independe de qualquer identidade técnica da
/// linha, a ordem de entrada não tem como vazar para o envelope — não há permutação a provar aqui.
/// </para>
/// <para>
/// <c>valoresSelecionaveis[]</c> tem prova própria, em
/// <see cref="PermutacaoDeValoresSelecionaveis_ProduzMesmosBytes"/>.
/// </para>
/// </remarks>
public sealed class EnvelopeCanonicoPermutacaoTests
{
    [Fact(DisplayName = "Permutar a ordem de entrada das coleções produz bytes canônicos e hash idênticos")]
    public void Permutacao_ProduzMesmosBytesEHash()
    {
        ProcessoSeletivo direto = ExemploDeEnvelopeProcessoRico.ProcessoRico(comRegrasDeComprovacao: true);
        ProcessoSeletivo permutado = ExemploDeEnvelopeProcessoRico.ProcessoRico(permutar: true, comRegrasDeComprovacao: true);

        ConferirQueAPermutacaoInverteACadaColecao(direto, permutado);

        SnapshotCanonico bytesDireto = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(direto));
        SnapshotCanonico bytesPermutado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(permutado));

        ConferirBytesEHashIdenticos(bytesDireto, bytesPermutado);
    }

    /// <summary>
    /// Issue #1059 (UNI-REQ-0072): a mesma invariância vale para as LISTAS de valores
    /// selecionáveis dentro do dicionário — permutar a ordem de entrada de cada lista, sem mudar
    /// o conteúdo, não pode mudar um único byte. O encoder ordena por <c>Ordem</c>/<c>Codigo</c>
    /// (D2); esta é a prova de que ele não depende da ordem em que o resolvedor os entregou.
    /// </summary>
    [Fact(DisplayName = "Permutar a ordem de entrada de valoresSelecionaveis produz bytes canônicos idênticos")]
    public void PermutacaoDeValoresSelecionaveis_ProduzMesmosBytes()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();

        ConferirQueAPermutacaoInverteAListaDeCorRaca();

        SnapshotCanonico bytesDireto = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo, permutarValoresSelecionaveis: false));
        SnapshotCanonico bytesPermutado = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo, permutarValoresSelecionaveis: true));

        bytesPermutado.Bytes.Should().Equal(bytesDireto.Bytes,
            "valoresSelecionaveis[] é ordenado pelo encoder por Ordem/Codigo (D2) — a ordem de entrada da lista " +
            "recebida do resolvedor não pode vazar para os bytes");
    }

    [Fact(DisplayName = "Recanonicalizar a mesma configuração sem alteração reproduz o mesmo hash")]
    public void Recanonicalizacao_SemAlteracao_MesmoHash()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();

        byte[] primeira = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo)).Bytes;
        byte[] segunda = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo)).Bytes;

        segunda.Should().Equal(primeira, "a projeção é pura — recanonicalizar sem alteração é estável byte a byte");
    }

    /// <summary>
    /// Pré-condição POR COLEÇÃO: sem provar que a permutação alterou DE FATO a ordem de entrada
    /// DAQUELA coleção especificamente, uma coleção que o <c>permutar</c> não conseguisse inverter
    /// (por exemplo, porque o agregado a reordena na entrada) passaria por vacuidade — a
    /// comparação de bytes ao final compararia um envelope com ele mesmo, sob outro nome, e a
    /// ausência de cobertura ficaria invisível.
    /// </summary>
    private static void ConferirQueAPermutacaoInverteACadaColecao(ProcessoSeletivo direto, ProcessoSeletivo permutado)
    {
        ConferirInversaoDasEtapasEDosProdutos(direto, permutado);
        ConferirInversaoDoAtendimentoEspecializado(direto, permutado);
        ConferirInversao(direto, permutado, "da distribuição de vagas por oferta",
            static p => p.DistribuicaoVagas.Select(static d => d.OfertaCursoOrigemId));
        ConferirInversao(direto, permutado, "dos critérios de desempate",
            static p => p.CriteriosDesempate.Select(static c => c.Ordem));
        ConferirInversaoDoCronograma(direto, permutado);
        ConferirInversaoDosFatosEDaDerivacao(direto, permutado);
        ConferirInversaoDosDestinosDaCascata(direto, permutado);
        ConferirInversaoDasRegrasDeComprovacao(direto, permutado);
    }

    private static void ConferirInversao<T>(
        ProcessoSeletivo direto, ProcessoSeletivo permutado, string descricaoDaColecao, Func<ProcessoSeletivo, IEnumerable<T>> colecao) =>
        colecao(direto).Should().NotEqual(
            colecao(permutado),
            $"pré-condição: a permutação tem de inverter a ordem de entrada {descricaoDaColecao}");

    /// <summary>
    /// Nos produtos da etapa, a mesma matéria aparece nos dois papéis, e é o único par em que o
    /// código do ato não fixa a posição sozinho. Sem a pré-condição dos produtos, tirar o desempate por
    /// papel do encoder deixaria a comparação de bytes ao final sem nada a provar aqui.
    /// </summary>
    private static void ConferirInversaoDasEtapasEDosProdutos(ProcessoSeletivo direto, ProcessoSeletivo permutado)
    {
        ConferirInversao(direto, permutado, "das etapas",
            static p => p.Etapas.Select(static e => e.Nome));
        ConferirInversao(direto, permutado, "dos produtos da etapa",
            static p => p.Etapas.SelectMany(static e => e.Produtos).Select(static x => x.Id));
    }

    /// <summary>
    /// O atendimento especializado não tem chave <c>Ordem</c> — o encoder ordena as três listas pela
    /// IDENTIDADE DE ORIGEM do item (CondicaoOrigemId/RecursoOrigemId/TipoDeficienciaOrigemId),
    /// não pela chave de conteúdo. Sem estas pré-condições, remover o OrderBy de
    /// SerializarAtendimento no encoder deixaria a comparação de bytes ao final vazia para as
    /// três listas.
    /// </summary>
    private static void ConferirInversaoDoAtendimentoEspecializado(ProcessoSeletivo direto, ProcessoSeletivo permutado)
    {
        ConferirInversao(direto, permutado, "das condições do atendimento especializado",
            static p => p.AtendimentoEspecializado!.Condicoes.Select(static c => c.CondicaoOrigemId));
        ConferirInversao(direto, permutado, "dos recursos do atendimento especializado",
            static p => p.AtendimentoEspecializado!.Recursos.Select(static r => r.RecursoOrigemId));
        ConferirInversao(direto, permutado, "dos tipos de deficiência do atendimento especializado",
            static p => p.AtendimentoEspecializado!.TiposDeficiencia.Select(static t => t.TipoDeficienciaOrigemId));
    }

    /// <summary>
    /// As bancas requeridas não têm chave <c>Ordem</c>: o agregado as ordena na SAÍDA, no getter, e o encoder escreve essa ordem.
    /// RESULTADO_PRELIMINAR é a única fase do exemplo com mais de uma banca; a pré-condição confere que a ordem de CHEGADA
    /// foi invertida pela permutação, sem o que a comparação de bytes ao final ficaria vazia para esta lista.
    /// </summary>
    private static void ConferirInversaoDoCronograma(ProcessoSeletivo direto, ProcessoSeletivo permutado)
    {
        ConferirInversao(direto, permutado, "das fases do cronograma",
            static p => p.CronogramaFases.Select(static f => f.Codigo));
        ExemploDeEnvelopeCronograma.BancasRequeridasDaFaseDeRecurso(permutar: false).Select(static b => b.Id)
            .Should().NotEqual(ExemploDeEnvelopeCronograma.BancasRequeridasDaFaseDeRecurso(permutar: true).Select(static b => b.Id),
                "pré-condição: a permutação tem de inverter a ordem de entrada das bancas requeridas da fase RESULTADO_PRELIMINAR");
    }

    /// <summary>
    /// A lista EXTERNA de configurações de derivação é a primeira conferida, mas o <c>permutar</c>
    /// também inverte, DENTRO da configuração de REGIME_INGRESSO, a lista de regras (AMPLA/COTISTA)
    /// e, dentro da regra COTISTA, a lista de condições aninhadas (COR_RACA/RENDA). Um agregado que
    /// normalizasse a ordem de QUALQUER uma na entrada (por exemplo, reordenando por
    /// <c>Ordem</c>/<c>Clausula</c> num factory futuro) passaria pela comparação de bytes ao final
    /// por vacuidade, sem que a lacuna aparecesse.
    /// </summary>
    private static void ConferirInversaoDosFatosEDaDerivacao(ProcessoSeletivo direto, ProcessoSeletivo permutado)
    {
        ConferirInversao(direto, permutado, "dos fatos coletados",
            static p => p.FatosColetados.Select(static f => f.FatoCodigo));
        ConferirInversao(direto, permutado, "da lista de configurações de derivação",
            static p => p.RegrasDerivacao.Select(static c => c.CodigoFato));
        ConferirInversao(direto, permutado, "das regras dentro da configuração de derivação",
            static p => DerivacaoDoRegimeDeIngresso(p).Regras.Select(static r => r.Contribui));
        ConferirInversao(direto, permutado, "das condições aninhadas da regra que contribui COTISTA",
            static p => RegraCotista(p).Condicoes.Select(static c => c.Fato));
    }

    private static ConfiguracaoDerivacaoFato DerivacaoDoRegimeDeIngresso(ProcessoSeletivo processo) =>
        processo.RegrasDerivacao.Single(static c => c.CodigoFato == "REGIME_INGRESSO");

    private static RegraDerivacaoConfigurada RegraCotista(ProcessoSeletivo processo) =>
        DerivacaoDoRegimeDeIngresso(processo).Regras.Single(static r => r.Contribui == "COTISTA");

    /// <summary>
    /// A origem fixada é a primeira das 8 federais — permutar embaralha os DESTINOS dentro dela;
    /// inverter a sequência de ORIGENS não provaria nada (cada origem aparece uma única vez na
    /// cascata, então a ordem entre origens nunca alimenta SerializarCascataRemanejamento).
    /// </summary>
    private static void ConferirInversaoDosDestinosDaCascata(ProcessoSeletivo direto, ProcessoSeletivo permutado)
    {
        string origemCascataDeTeste = ModalidadesFederaisLei12711.Codigos[0];

        ConferirInversao(direto, permutado, $"dos destinos da cascata dentro da origem '{origemCascataDeTeste}'",
            p => p.Cascata!.Destinos.Where(d => d.ModalidadeOrigemCodigo == origemCascataDeTeste).Select(static d => d.ModalidadeDestinoCodigo));
    }

    private static void ConferirInversaoDasRegrasDeComprovacao(ProcessoSeletivo direto, ProcessoSeletivo permutado)
    {
        ConferirInversao(direto, permutado, "das regras sem grupo",
            static p => p.RegrasDeComprovacaoSemGrupo.Select(static r => r.Forma));
        ConferirInversao(direto, permutado, "dos itens do grupo de comprovação",
            static p => GrupoDaComprovacao(p).Itens.Select(static f => f.DocumentoExigido!.TipoDocumentoCodigo));
    }

    private static RegraDeComprovacao GrupoDaComprovacao(ProcessoSeletivo processo) =>
        processo.RegrasDeComprovacaoSemGrupo.Single(static r => r.Itens.Count > 0);

    private static void ConferirQueAPermutacaoInverteAListaDeCorRaca()
    {
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> direto =
            ExemploDeEnvelopeDadosDoEdital.ValoresSelecionaveisRicos(permutarValores: false);
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> permutado =
            ExemploDeEnvelopeDadosDoEdital.ValoresSelecionaveisRicos(permutarValores: true);

        direto["COR_RACA"]!.Select(static v => v.Codigo).Should().NotEqual(
            permutado["COR_RACA"]!.Select(static v => v.Codigo),
            "pré-condição: a permutação tem de inverter a ordem de entrada da lista de valores selecionáveis");
    }

    private static void ConferirBytesEHashIdenticos(SnapshotCanonico bytesDireto, SnapshotCanonico bytesPermutado)
    {
        bytesPermutado.Bytes.Should().Equal(bytesDireto.Bytes,
            "o envelope depende só do conteúdo — permutar a ordem de entrada de etapas, atendimento especializado, " +
            "distribuição/ofertas, critérios de desempate, cronograma de fases (inclusive as bancas requeridas " +
            "de uma fase), fatos coletados, regras de derivação (a lista externa, as regras internas de uma " +
            "configuração e as condições dentro de uma regra), destinos da cascata e as regras de comprovação " +
            "não muda um único byte");
        PerfilCanonicoV1.Instancia.HashHex(bytesPermutado.Bytes)
            .Should().Be(PerfilCanonicoV1.Instancia.HashHex(bytesDireto.Bytes),
                "bytes idênticos ⟹ hash idêntico");
    }
}
