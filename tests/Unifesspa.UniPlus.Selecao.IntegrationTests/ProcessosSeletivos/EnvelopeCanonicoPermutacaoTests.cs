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
/// agregado. Permutar a ordem de entrada de etapas, distribuição de vagas e ofertas, critérios de
/// desempate, cronograma de fases, fatos coletados, regras de derivação (a lista externa e as
/// condições/regras aninhadas), destinos da cascata de remanejamento (dentro de uma mesma origem) e
/// a árvore de satisfação (as raízes e os filhos de um grupo) — sem mudar nenhum valor — SHALL
/// produzir os mesmos bytes canônicos e o mesmo hash.
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
/// FORA desta prova, por decisão: as coleções que o encoder já ordena pela CHAVE DE CONTEÚDO do
/// próprio item (ADR-0109 D9) em vez de um campo <c>Ordem</c> declarado — <c>ofertaAtendimento</c>
/// (condições/recursos/tipos de deficiência), <c>modalidades[].criteriosCumulativos</c>,
/// <c>classificacao.regrasEliminacao</c>, <c>cronogramaFases.fases[].bancasRequeridas</c> e
/// <c>documentosExigidos.exigencias</c>. A ordem de CRIAÇÃO dessa família não pode vazar para o
/// envelope de qualquer forma (dois Guid v7 em ordem inversa também mudariam a ordem de ENTRADA), e
/// já tem prova própria — <c>EnvelopeCanonicoGoldenTests.Envelope_IndependeDaOrdemDeCriacao</c> —
/// no vetor de regressão que cabe a uma chave que É o próprio conteúdo, não uma posição declarada.
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
        direto.CriteriosDesempate.Select(static c => c.Ordem)
            .Should().NotEqual(permutado.CriteriosDesempate.Select(static c => c.Ordem),
                "pré-condição: a permutação tem de inverter a ordem de entrada dos critérios de desempate");
        direto.CronogramaFases.Select(static f => f.Codigo)
            .Should().NotEqual(permutado.CronogramaFases.Select(static f => f.Codigo),
                "pré-condição: a permutação tem de inverter a ordem de entrada das fases do cronograma");
        direto.FatosColetados.Select(static f => f.FatoCodigo)
            .Should().NotEqual(permutado.FatosColetados.Select(static f => f.FatoCodigo),
                "pré-condição: a permutação tem de inverter a ordem de entrada dos fatos coletados");
        direto.RegrasDerivacao.Select(static c => c.CodigoFato)
            .Should().NotEqual(permutado.RegrasDerivacao.Select(static c => c.CodigoFato),
                "pré-condição: a permutação tem de inverter a ordem de entrada da lista de configurações de derivação");

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
            "o envelope depende só do conteúdo — permutar a ordem de entrada de etapas, distribuição/ofertas, " +
            "critérios de desempate, cronograma de fases, fatos coletados, regras de derivação, destinos da " +
            "cascata e a árvore de satisfação não muda um único byte");
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
