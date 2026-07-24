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

    [Fact(DisplayName = "Recanonicalizar a mesma configuração sem alteração reproduz o mesmo hash")]
    public void Recanonicalizacao_SemAlteracao_MesmoHash()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();

        byte[] primeira = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;
        byte[] segunda = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;

        segunda.Should().Equal(primeira, "a projeção é pura — recanonicalizar sem alteração é estável byte a byte");
    }
}
