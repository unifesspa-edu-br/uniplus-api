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
/// agregado. Permutar a ordem de entrada de etapas, da oferta de atendimento (condições, recursos e
/// tipos de deficiência), distribuição de vagas e ofertas, critérios de desempate, cronograma de
/// fases (inclusive as bancas requeridas de uma fase), fatos coletados, regras de derivação (a
/// lista externa, as regras internas de uma configuração e as condições aninhadas de uma regra),
/// destinos da cascata de remanejamento (dentro de uma mesma origem) e a árvore de satisfação (as
/// raízes e os filhos de um grupo) — sem mudar nenhum valor — SHALL produzir os mesmos bytes
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
/// <c>ofertaAtendimento</c> (condições/recursos/tipos de deficiência) e
/// <c>cronogramaFases.fases[].bancasRequeridas</c> ordenam pela IDENTIDADE DE ORIGEM do item
/// (<c>CondicaoOrigemId</c>/<c>RecursoOrigemId</c>/<c>TipoDeficienciaOrigemId</c>/<c>TipoBancaOrigemId</c>)
/// — a segunda camada da política de ordenação, não a chave de conteúdo (ADR-0109 D9, a terceira e
/// última camada, reservada a coleções sem identidade de negócio nenhuma). As duas entram nesta
/// prova (a pré-condição correspondente confere que a permutação de fato inverte a ordem de
/// entrada de cada uma).
/// </para>
/// <para>
/// FORA desta prova, por decisão: <c>modalidades[].criteriosCumulativos</c> e
/// <c>classificacao.regrasEliminacao</c> — as duas coleções que não têm identidade de negócio nem
/// de origem entre os elementos, e por isso o encoder ordena pela CHAVE DE CONTEÚDO do próprio
/// item (ADR-0109 D9). <c>classificacao.regrasEliminacao</c> tem prova própria —
/// <c>EnvelopeCanonicoGoldenTests.Envelope_IndependeDaOrdemDeCriacao</c> varia a ordem de CRIAÇÃO
/// de duas regras de eliminação (e, com ela, a ordem dos Guid v7) e prova que o envelope resultante
/// não muda. <c>modalidades[].criteriosCumulativos</c> tem prova própria em
/// <c>OrdenacaoDeConjuntosCanonicosTests.CriteriosCumulativos_OrdenaPelaChaveDeConteudoEmBytesUtf8</c>,
/// que compara os bytes de duas entradas com o mesmo conteúdo em ordens de entrada diferentes.
/// </para>
/// <para>
/// <c>documentosExigidos.exigencias</c> também fica FORA, por um motivo distinto dos dois acima: a
/// chave PRIMÁRIA de ordenação é de negócio parcial — fase mais tipo de documento, ver
/// <c>SnapshotPublicacaoCanonicalizer.SerializarExigencias</c> — não a identidade de origem nem a
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
        ProcessoSeletivo direto = CorpusEnvelope.ProcessoRico(comArvoreSatisfacao: true);
        ProcessoSeletivo permutado = CorpusEnvelope.ProcessoRico(permutar: true, comArvoreSatisfacao: true);

        // Pré-condição POR COLEÇÃO: sem provar que a permutação alterou DE FATO a ordem de entrada
        // DAQUELA coleção especificamente, uma coleção que o `permutar` não conseguisse inverter
        // (por exemplo, porque o agregado a reordena na entrada) passaria por vacuidade — a
        // comparação de bytes ao final compararia um envelope com ele mesmo, sob outro nome, e a
        // ausência de cobertura ficaria invisível (issue #1087).
        direto.Etapas.Select(static e => e.Nome)
            .Should().NotEqual(permutado.Etapas.Select(static e => e.Nome),
                "pré-condição: a permutação tem de inverter a ordem de entrada das etapas");

        // A oferta de atendimento não tem chave `Ordem` — o encoder ordena as três listas pela
        // IDENTIDADE DE ORIGEM do item (CondicaoOrigemId/RecursoOrigemId/TipoDeficienciaOrigemId),
        // não pela chave de conteúdo. Sem esta pré-condição, remover o OrderBy de
        // SerializarAtendimento no encoder deixaria a comparação de bytes ao final vazia para as
        // três listas (issue #1087).
        direto.OfertaAtendimento!.Condicoes.Select(static c => c.CondicaoOrigemId)
            .Should().NotEqual(permutado.OfertaAtendimento!.Condicoes.Select(static c => c.CondicaoOrigemId),
                "pré-condição: a permutação tem de inverter a ordem de entrada das condições da oferta de atendimento");
        direto.OfertaAtendimento!.Recursos.Select(static r => r.RecursoOrigemId)
            .Should().NotEqual(permutado.OfertaAtendimento!.Recursos.Select(static r => r.RecursoOrigemId),
                "pré-condição: a permutação tem de inverter a ordem de entrada dos recursos da oferta de atendimento");
        direto.OfertaAtendimento!.TiposDeficiencia.Select(static t => t.TipoDeficienciaOrigemId)
            .Should().NotEqual(permutado.OfertaAtendimento!.TiposDeficiencia.Select(static t => t.TipoDeficienciaOrigemId),
                "pré-condição: a permutação tem de inverter a ordem de entrada dos tipos de deficiência da oferta de atendimento");

        direto.DistribuicaoVagas.Select(static d => d.OfertaCursoOrigemId)
            .Should().NotEqual(permutado.DistribuicaoVagas.Select(static d => d.OfertaCursoOrigemId),
                "pré-condição: a permutação tem de inverter a ordem de entrada da distribuição de vagas por oferta");

        direto.CriteriosDesempate.Select(static c => c.Ordem)
            .Should().NotEqual(permutado.CriteriosDesempate.Select(static c => c.Ordem),
                "pré-condição: a permutação tem de inverter a ordem de entrada dos critérios de desempate");
        direto.CronogramaFases.Select(static f => f.Codigo)
            .Should().NotEqual(permutado.CronogramaFases.Select(static f => f.Codigo),
                "pré-condição: a permutação tem de inverter a ordem de entrada das fases do cronograma");

        // As bancas requeridas não têm chave `Ordem` — o encoder ordena pela IDENTIDADE DE ORIGEM
        // (TipoBancaOrigemId), não pela chave de conteúdo. A fase RESULTADO_PRELIMINAR é a única
        // do corpus com mais de uma banca — sem esta pré-condição, remover o OrderBy de
        // SerializarBancasRequeridas deixaria a comparação de bytes ao final vazia para esta lista.
        FaseCronograma faseComBancasDireto = direto.CronogramaFases.Single(static f => f.Codigo == "RESULTADO_PRELIMINAR");
        FaseCronograma faseComBancasPermutado = permutado.CronogramaFases.Single(static f => f.Codigo == "RESULTADO_PRELIMINAR");
        faseComBancasDireto.BancasRequeridas.Select(static b => b.TipoBancaOrigemId)
            .Should().NotEqual(faseComBancasPermutado.BancasRequeridas.Select(static b => b.TipoBancaOrigemId),
                "pré-condição: a permutação tem de inverter a ordem de entrada das bancas requeridas da fase RESULTADO_PRELIMINAR");

        direto.FatosColetados.Select(static f => f.FatoCodigo)
            .Should().NotEqual(permutado.FatosColetados.Select(static f => f.FatoCodigo),
                "pré-condição: a permutação tem de inverter a ordem de entrada dos fatos coletados");
        direto.RegrasDerivacao.Select(static c => c.CodigoFato)
            .Should().NotEqual(permutado.RegrasDerivacao.Select(static c => c.CodigoFato),
                "pré-condição: a permutação tem de inverter a ordem de entrada da lista de configurações de derivação");

        // A lista EXTERNA de configurações de derivação já está coberta acima — mas o `permutar`
        // também inverte, DENTRO da configuração de MODALIDADE, a lista de regras (AC/LB_PPI) e,
        // dentro da regra LB_PPI, a lista de condições aninhadas (COR_RACA/RENDA). Nenhuma das duas
        // tinha pré-condição própria: um agregado que normalizasse a ordem de QUALQUER uma na
        // entrada (por exemplo, reordenando por `Ordem`/`Clausula` num factory futuro) passaria
        // pela comparação de bytes ao final por vacuidade, sem que a lacuna aparecesse (issue #1087).
        ConfiguracaoDerivacaoFato derivacaoModalidadeDireto = direto.RegrasDerivacao.Single(static c => c.CodigoFato == "MODALIDADE");
        ConfiguracaoDerivacaoFato derivacaoModalidadePermutado = permutado.RegrasDerivacao.Single(static c => c.CodigoFato == "MODALIDADE");
        derivacaoModalidadeDireto.Regras.Select(static r => r.Contribui)
            .Should().NotEqual(derivacaoModalidadePermutado.Regras.Select(static r => r.Contribui),
                "pré-condição: a permutação tem de inverter a ordem de entrada das regras dentro da configuração de derivação de MODALIDADE");

        RegraDerivacaoConfigurada regraComCondicoesDireto = derivacaoModalidadeDireto.Regras.Single(static r => r.Contribui == "LB_PPI");
        RegraDerivacaoConfigurada regraComCondicoesPermutado = derivacaoModalidadePermutado.Regras.Single(static r => r.Contribui == "LB_PPI");
        regraComCondicoesDireto.Condicoes.Select(static c => c.Fato)
            .Should().NotEqual(regraComCondicoesPermutado.Condicoes.Select(static c => c.Fato),
                "pré-condição: a permutação tem de inverter a ordem de entrada das condições aninhadas da regra que contribui LB_PPI");

        // A origem fixada é a primeira das 8 federais — permutar embaralha os DESTINOS dentro dela;
        // inverter a sequência de ORIGENS não provaria nada (cada origem aparece uma única vez na
        // cascata, então a ordem entre origens nunca alimenta SerializarCascataRemanejamento).
        string origemCascataDeTeste = ModalidadesFederaisLei12711.Codigos[0];
        direto.Cascata!.Destinos.Where(d => d.ModalidadeOrigemCodigo == origemCascataDeTeste)
            .Select(static d => d.ModalidadeDestinoCodigo)
            .Should().NotEqual(
                permutado.Cascata!.Destinos.Where(d => d.ModalidadeOrigemCodigo == origemCascataDeTeste)
                    .Select(static d => d.ModalidadeDestinoCodigo),
                $"pré-condição: a permutação tem de inverter a ordem de entrada dos destinos da cascata dentro da origem '{origemCascataDeTeste}'");

        direto.RaizesDeExigencia.Select(static r => r.Tipo)
            .Should().NotEqual(permutado.RaizesDeExigencia.Select(static r => r.Tipo),
                "pré-condição: a permutação tem de inverter a ordem de entrada das raízes da árvore de satisfação");

        NoExigencia grupoDireto = direto.RaizesDeExigencia.Single(static r => r.Filhos.Count > 0);
        NoExigencia grupoPermutado = permutado.RaizesDeExigencia.Single(static r => r.Filhos.Count > 0);
        grupoDireto.Filhos.Select(static f => f.DocumentoExigido!.TipoDocumentoCodigo)
            .Should().NotEqual(grupoPermutado.Filhos.Select(static f => f.DocumentoExigido!.TipoDocumentoCodigo),
                "pré-condição: a permutação tem de inverter a ordem de entrada dos filhos do grupo da árvore de satisfação");

        SnapshotCanonico bytesDireto = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(direto));
        SnapshotCanonico bytesPermutado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(permutado));

        bytesPermutado.Bytes.Should().Equal(bytesDireto.Bytes,
            "o envelope depende só do conteúdo — permutar a ordem de entrada de etapas, oferta de atendimento, " +
            "distribuição/ofertas, critérios de desempate, cronograma de fases (inclusive as bancas requeridas " +
            "de uma fase), fatos coletados, regras de derivação (a lista externa, as regras internas de uma " +
            "configuração e as condições aninhadas de uma regra), destinos da cascata e a árvore de satisfação " +
            "não muda um único byte");
        PerfilCanonicoV1.Instancia.HashHex(bytesPermutado.Bytes)
            .Should().Be(PerfilCanonicoV1.Instancia.HashHex(bytesDireto.Bytes),
                "bytes idênticos ⟹ hash idêntico");
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
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();

        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> direto =
            CorpusEnvelope.ValoresSelecionaveisRicos(permutarValores: false);
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> permutado =
            CorpusEnvelope.ValoresSelecionaveisRicos(permutarValores: true);

        direto["COR_RACA"]!.Select(static v => v.Codigo).Should().NotEqual(
            permutado["COR_RACA"]!.Select(static v => v.Codigo),
            "pré-condição: a permutação tem de inverter a ordem de entrada da lista de valores selecionáveis");

        SnapshotCanonico bytesDireto = CorpusEnvelope.Codec.Codificar(
            CorpusEnvelope.Entrada(processo, permutarValoresSelecionaveis: false));
        SnapshotCanonico bytesPermutado = CorpusEnvelope.Codec.Codificar(
            CorpusEnvelope.Entrada(processo, permutarValoresSelecionaveis: true));

        bytesPermutado.Bytes.Should().Equal(bytesDireto.Bytes,
            "valoresSelecionaveis[] é ordenado pelo encoder por Ordem/Codigo (D2) — a ordem de entrada da lista " +
            "recebida do resolvedor não pode vazar para os bytes");
    }

    [Fact(DisplayName = "Recanonicalizar a mesma configuração sem alteração reproduz o mesmo hash")]
    public void Recanonicalizacao_SemAlteracao_MesmoHash()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();

        byte[] primeira = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;
        byte[] segunda = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;

        segunda.Should().Equal(primeira, "a projeção é pura — recanonicalizar sem alteração é estável byte a byte");
    }
}
