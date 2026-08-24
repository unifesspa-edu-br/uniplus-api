namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// O que separa congelar o calendário <b>por valor</b> de guardar uma referência a ele: a versão
/// publicada continua reproduzindo a própria contagem depois que o dataset de origem deixa de ser
/// vigente — e mesmo depois que ele é removido do cadastro.
/// </summary>
/// <remarks>
/// Estes cenários não são exercitáveis com identificador. Guardando só <c>origemId</c> e
/// <c>versaoDataset</c>, a recanonicalização precisaria reler o catálogo vivo: com o dataset
/// trocado produziria bytes diferentes, e com o dataset removido não produziria bytes nenhum.
/// A prova de fidelidade — que é o que autoriza o descarte de uma retificação — deixaria de
/// valer justamente quando mais importa.
/// </remarks>
public sealed class CalendarioCongeladoRoundTripTests
{
    private static JsonObject Payload(byte[] bytes) => JsonNode.Parse(bytes)!.AsObject();

    [Fact(DisplayName = "O bloco congela a lista inteira de dias, não uma referência ao dataset")]
    public void Bloco_CongelaListaPorValor()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));

        JsonObject bloco = Payload(congelado.Bytes)["calendarioDiasUteis"]!.AsObject();

        bloco["presente"]!.GetValue<bool>().Should().BeTrue();
        bloco["diasNaoUteis"]!.AsArray().Should().HaveCount(4,
            "o corpus congela uma data de cada abrangência — nacional, estadual, municipal e institucional");

        // origemId e versaoDataset ficam como rastreio da procedência. O que reproduz a contagem
        // é a lista ao lado deles.
        bloco.Should().ContainKey("origemId").And.ContainKey("versaoDataset");

        JsonObject municipal = bloco["diasNaoUteis"]!.AsArray()
            .Single(d => d!["abrangencia"]!.GetValue<string>() == "MUNICIPAL")!.AsObject();
        municipal["municipioIbge"]!.GetValue<string>().Should().Be("1504208");
        municipal["uf"]!.GetValue<string>().Should().Be("PA");
    }

    [Fact(DisplayName = "A recanonicalização usa o calendário do envelope — o catálogo vivo não é consultado")]
    public void Recanonicalizacao_UsaOCalendarioDoEnvelope()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        reidratado.Value!.CalendarioDiasUteis.Should().NotBeNull(
            "o calendário volta do envelope, e é dele que a recanonicalização parte");

        byte[] recodificado = CorpusEnvelope.Registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(
                processo,
                reidratado.Value.Dados,
                reidratado.Value.HashDocumento,
                reidratado.Value.FusoHorario,
                reidratado.Value.Retificacao,
                reidratado.Value.Conformidade,
                reidratado.Value.MetadadosFatosCongelados,
                reidratado.Value.ValoresSelecionaveisCongelados,
                reidratado.Value.CalendarioDiasUteis)).Value!.Bytes;

        recodificado.Should().Equal(congelado.Bytes);
    }

    [Fact(DisplayName = "Trocado o dataset vigente, a versão publicada continua reproduzindo os mesmos bytes")]
    public void DatasetTrocado_NaoAfetaVersaoPublicada()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));

        // O cadastro publica um dataset novo: outra origem, outra versão, outro conteúdo.
        CalendarioDiasUteisCongelado datasetNovo = CalendarioDiasUteisCongelado.Criar(
            Guid.Parse("01930000-0000-7000-8000-0000000000ff"),
            "2027",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2027, 1, 1), "NACIONAL", null, null, null).Value!]).Value!;

        byte[] sobDatasetNovo = CorpusEnvelope.Codec.Codificar(
            CorpusEnvelope.Entrada(processo) with { CalendarioDiasUteis = datasetNovo }).Bytes;

        sobDatasetNovo.Should().NotEqual(congelado.Bytes,
            "pré-condição: o conteúdo do calendário participa dos bytes — se não participasse, " +
            "este teste passaria sem provar nada");

        // A versão publicada não se move: ela carrega o calendário que congelou.
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(
            CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes));

        reidratado.Value!.CalendarioDiasUteis!.VersaoDataset.Should().Be("2026");
        reidratado.Value.CalendarioDiasUteis.DiasNaoUteis.Should().HaveCount(4);
    }

    [Fact(DisplayName = "Removido o dataset da origem, a versão publicada ainda reproduz os bytes congelados")]
    public void DatasetRemovido_VersaoAindaSeReproduz()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));
        CorpusEnvelope.Publicar(processo);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(processo, congelado.Bytes);

        // Nada aqui consulta cadastro nenhum: a reidratação parte só dos bytes persistidos. É
        // essa ausência que o cenário prova — com o dataset removido da origem, um envelope que
        // guardasse apenas a referência não teria de onde recuperar a lista.
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        byte[] recodificado = CorpusEnvelope.Registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(
                processo,
                reidratado.Value!.Dados,
                reidratado.Value.HashDocumento,
                reidratado.Value.FusoHorario,
                reidratado.Value.Retificacao,
                reidratado.Value.Conformidade,
                reidratado.Value.MetadadosFatosCongelados,
                reidratado.Value.ValoresSelecionaveisCongelados,
                reidratado.Value.CalendarioDiasUteis)).Value!.Bytes;

        recodificado.Should().Equal(congelado.Bytes,
            "a lista congelada basta para reproduzir a versão — a origem é rastreio, não dependência");
    }
}
