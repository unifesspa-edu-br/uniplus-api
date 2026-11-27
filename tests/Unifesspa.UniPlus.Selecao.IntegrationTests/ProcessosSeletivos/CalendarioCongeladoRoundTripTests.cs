namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que separa congelar o calendário <b>por valor</b> de guardar uma referência a ele: a versão
/// publicada continua reproduzindo a própria contagem depois que o calendário de origem deixa de ser
/// vigente — e mesmo depois que ele é removido do cadastro.
/// </summary>
/// <remarks>
/// Estes cenários não são exercitáveis com identificador. Guardando só <c>origemId</c> e
/// <c>versaoDoCalendario</c>, a recanonicalização precisaria reler o catálogo vivo: com o calendário
/// trocado produziria bytes diferentes, e com o calendário removido não produziria bytes nenhum.
/// A prova de fidelidade — que é o que autoriza o descarte de uma retificação — deixaria de
/// valer justamente quando mais importa.
/// </remarks>
public sealed class CalendarioCongeladoRoundTripTests
{
    private static JsonObject Payload(byte[] bytes) => JsonNode.Parse(bytes)!.AsObject();

    [Fact(DisplayName = "O bloco congela a lista inteira de dias, não uma referência ao calendário")]
    public void Bloco_CongelaListaPorValor()
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo));

        JsonObject bloco = Payload(congelado.Bytes)["calendarioDiasUteis"]!.AsObject();

        bloco["presente"]!.GetValue<bool>().Should().BeTrue();
        bloco["diasNaoUteis"]!.AsArray().Should().HaveCount(4,
            "o exemplo congela uma data de cada abrangência — nacional, estadual, municipal e institucional");

        // origemId e versaoDoCalendario ficam como rastreio da procedência. O que reproduz a contagem
        // é a lista ao lado deles.
        bloco.Should().ContainKey("origemId").And.ContainKey("versaoDoCalendario");
        ConferirDiaMunicipal(bloco);
    }

    private static void ConferirDiaMunicipal(JsonObject bloco)
    {
        JsonObject municipal = bloco["diasNaoUteis"]!.AsArray()
            .Single(d => d!["abrangencia"]!.GetValue<string>() == "MUNICIPAL")!.AsObject();
        municipal["municipioIbge"]!.GetValue<string>().Should().Be("1504208");
        municipal["uf"]!.GetValue<string>().Should().Be("PA");
    }

    [Fact(DisplayName = "A recanonicalização usa o calendário do envelope — o catálogo vivo não é consultado")]
    public void Recanonicalizacao_UsaOCalendarioDoEnvelope()
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico(comBancasExigidasNaPublicacao: true);
        IsencaoDaTaxaDeTeste.GarantirOFormularioDeQuemCobraTaxa(processo);
        SnapshotCanonico congelado = ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo));
        EnvelopeReidratado envelope = PublicarEReidratar(processo, congelado);

        envelope.DadosDeCatalogo.CalendarioDiasUteis.Should().NotBeNull(
            "o calendário volta do envelope, e é dele que a recanonicalização parte");

        Recodificar(processo, envelope).Should().Equal(congelado.Bytes);
    }

    [Fact(DisplayName = "Trocado o calendário vigente, a versão publicada continua reproduzindo os mesmos bytes")]
    public void CalendarioTrocado_NaoAfetaVersaoPublicada()
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo));

        // O cadastro publica um calendário novo: outra origem, outra versão, outro conteúdo.
        byte[] sobCalendarioNovo = CodificarComOutroCalendario(processo);

        sobCalendarioNovo.Should().NotEqual(congelado.Bytes,
            "pré-condição: o conteúdo do calendário participa dos bytes — se não participasse, " +
            "este teste passaria sem provar nada");

        // A versão publicada não se move: ela carrega o calendário que congelou.
        ConferirCalendarioPublicado(processo, congelado);
    }

    [Fact(DisplayName = "Removido o calendário da origem, a versão publicada ainda reproduz os bytes congelados")]
    public void CalendarioRemovido_VersaoAindaSeReproduz()
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico(comBancasExigidasNaPublicacao: true);
        IsencaoDaTaxaDeTeste.GarantirOFormularioDeQuemCobraTaxa(processo);
        SnapshotCanonico congelado = ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo));

        // Nada aqui consulta cadastro nenhum: a reidratação parte só dos bytes persistidos. É
        // essa ausência que o cenário prova — com o calendário removido da origem, um envelope que
        // guardasse apenas a referência não teria de onde recuperar a lista.
        EnvelopeReidratado envelope = PublicarEReidratar(processo, congelado);

        Recodificar(processo, envelope).Should().Equal(congelado.Bytes,
            "a lista congelada basta para reproduzir a versão — a origem é rastreio, não dependência");
    }

    private static EnvelopeReidratado PublicarEReidratar(ProcessoSeletivo processo, SnapshotCanonico congelado)
    {
        ExemploDeProcessoSeletivoEntrada.Publicar(processo);
        VersaoConfiguracao versao = ExemploDeProcessoSeletivoEntrada.VersaoDeAbertura(processo, congelado.Bytes);
        Result<EnvelopeReidratado> reidratado = ExemploDeProcessoSeletivo.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);

        return reidratado.Value!;
    }

    private static byte[] Recodificar(ProcessoSeletivo processo, EnvelopeReidratado envelope) =>
        new SnapshotPublicacaoCanonicalizer().Canonicalizar(
            new EntradaCanonicalizacao(processo, envelope.Dados, envelope.HashDocumento, envelope.FusoHorario,
                envelope.Retificacao, envelope.Conformidade, envelope.DadosDeCatalogo)).Bytes;

    private static void ConferirCalendarioPublicado(ProcessoSeletivo processo, SnapshotCanonico congelado)
    {
        Result<EnvelopeReidratado> reidratado = ExemploDeProcessoSeletivo.Registro.Reidratar(
            ExemploDeProcessoSeletivoEntrada.VersaoDeAbertura(processo, congelado.Bytes));
        reidratado.Value!.DadosDeCatalogo.CalendarioDiasUteis!.VersaoDoCalendario.Should().Be("2026");
        reidratado.Value.DadosDeCatalogo.CalendarioDiasUteis.DiasNaoUteis.Should().HaveCount(4);
    }

    private static byte[] CodificarComOutroCalendario(ProcessoSeletivo processo)
    {
        CalendarioDiasUteisCongelado calendarioNovo = CalendarioDiasUteisCongelado.Criar(
            Guid.Parse("01930000-0000-7000-8000-0000000000ff"), "2027",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2027, 1, 1), "NACIONAL", null, null, null).Value!]).Value!;
        EntradaCanonicalizacao entrada = ExemploDeProcessoSeletivoEntrada.Entrada(processo);
        return ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(entrada with
        {
            DadosDeCatalogo = entrada.DadosDeCatalogo with { CalendarioDiasUteis = calendarioNovo },
        }).Bytes;
    }
}
