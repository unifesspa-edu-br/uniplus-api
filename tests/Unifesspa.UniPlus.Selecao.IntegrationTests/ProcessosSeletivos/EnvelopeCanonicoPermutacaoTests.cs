namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// <b>Invariância à permutação</b> (Story #928, §7.5): o envelope canônico depende só do
/// <b>conteúdo</b> da configuração, nunca da ordem em que as coleções não ordenadas chegam ao
/// agregado. Permutar a ordem de entrada de fatos coletados, regras de derivação, condições de um
/// predicado e ofertas de curso — sem mudar nenhum valor — SHALL produzir os mesmos bytes canônicos
/// e o mesmo hash.
/// </summary>
/// <remarks>
/// A projeção ordena cada coleção por uma chave determinística (a <c>Ordem</c> onde é semântica, a
/// identidade de negócio onde existe, a chave de conteúdo onde não há chave natural), então duas
/// entradas equivalentes convergem para os mesmos bytes. O determinismo do grafo conjunto e da
/// identidade canônica é provado no grão do value object (<c>GrafoDependenciaConjuntaTests</c>,
/// <c>IdCanonicoTests</c>); aqui a prova é sobre os <b>bytes do envelope inteiro</b>.
/// </remarks>
public sealed class EnvelopeCanonicoPermutacaoTests
{
    [Fact(DisplayName = "Permutar a ordem de entrada das coleções produz bytes canônicos e hash idênticos")]
    public void Permutacao_ProduzMesmosBytesEHash()
    {
        ProcessoSeletivo direto = CorpusEnvelope.ProcessoRico();
        ProcessoSeletivo permutado = CorpusEnvelope.ProcessoRico(permutar: true);

        // Pré-condição: a permutação inverteu DE FATO a ordem de entrada — sem isto o teste seria
        // vacuamente verdadeiro (comparar um envelope com ele mesmo).
        direto.FatosColetados.Select(static f => f.FatoCodigo)
            .Should().NotEqual(permutado.FatosColetados.Select(static f => f.FatoCodigo),
                "pré-condição: a permutação tem de inverter a ordem de entrada dos fatos coletados");
        direto.RegrasDerivacao.Select(static c => c.CodigoFato)
            .Should().NotEqual(permutado.RegrasDerivacao.Select(static c => c.CodigoFato),
                "pré-condição: a permutação tem de inverter a ordem de entrada da lista de configurações de derivação");

        SnapshotCanonico bytesDireto = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(direto));
        SnapshotCanonico bytesPermutado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(permutado));

        bytesPermutado.Bytes.Should().Equal(bytesDireto.Bytes,
            "o envelope depende só do conteúdo — permutar a ordem de entrada de fatos, regras, condições e " +
            "ofertas não muda um único byte");
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
