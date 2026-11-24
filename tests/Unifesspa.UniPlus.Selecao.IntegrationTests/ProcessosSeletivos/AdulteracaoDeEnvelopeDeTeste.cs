namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.VersoesDoEnvelopeDeTeste;

/// <summary>
/// Adultera o envelope e <b>recomputa o hash</b> — é o cenário mais exigente: quem adultera controla a
/// linha inteira. O que sobra para recusar são a canonicidade e a gramática, e é isso que os testes de
/// recusa do codec exercitam.
/// </summary>
internal static class AdulteracaoDeEnvelopeDeTeste
{
    public static Result<EnvelopeReidratado> ReidratarComEnvelopeAdulterado(Action<JsonObject> adulterar, bool comRegrasDeComprovacao = false)
    {
        byte[] originais = ExemploDeEnvelope.Canonicalizador.Canonicalizar(
            ExemploDeEnvelopeEntrada.Entrada(ExemploDeEnvelopeProcessoRico.ProcessoRico(comRegrasDeComprovacao: comRegrasDeComprovacao))).Bytes;

        // A versão é reconstruída SOBRE os bytes adulterados — o hash bate, e o gate de
        // integridade não é o que os recusa. É a gramática.
        return ReidratarComoAbertura(BytesAdulterados(originais, adulterar));
    }

    /// <summary>
    /// Mesmo raciocínio de <see cref="ReidratarComEnvelopeAdulterado"/>, mas sobre o processo de
    /// referência da golden fixture (<see cref="EnvelopeDeReferencia.ProcessoDeReferencia"/>) —
    /// é ele, não <see cref="ExemploDeEnvelopeProcessoRico.ProcessoRico"/>, quem tem <c>documentosExigidos</c>
    /// populado, e por isso <c>metadadosFatos[].valoresDominioDeclarados</c> só existe aqui.
    /// </summary>
    public static Result<EnvelopeReidratado> ReidratarComEnvelopeDeReferenciaAdulterado(Action<JsonObject> adulterar)
    {
        ProcessoSeletivo processo = EnvelopeDeReferencia.ProcessoDeReferencia(comBancasExigidasNaPublicacao: true);
        DadosEdital dados = EnvelopeDeReferencia.DadosDeReferencia();
        SnapshotCanonico congelado = CongelarReferencia(processo, dados);

        byte[] adulterados = BytesAdulterados(congelado.Bytes, adulterar);

        return ExemploDeEnvelope.Registro.Reidratar(PublicarSobreBytes(processo, dados, congelado, adulterados));
    }

    public static JsonObject FatoColetadoPorCodigo(JsonObject envelope, string codigo) =>
        envelope["dadosDosFormularios"]!.AsArray()
            .Single(fato => fato!["codigoDoDado"]!.GetValue<string>() == codigo)!.AsObject();

    public static JsonObject MetadadoFatoPorCodigo(JsonObject envelope, string codigo) =>
        envelope["documentosExigidos"]!["definicoesDosDados"]!.AsArray()
            .Single(metadado => metadado!["codigoDoDado"]!.GetValue<string>() == codigo)!.AsObject();

    public static JsonObject Navegar(JsonObject raiz, string caminho)
    {
        JsonNode atual = raiz;
        foreach (string parte in caminho.Split('.'))
        {
            atual = int.TryParse(parte, System.Globalization.CultureInfo.InvariantCulture, out int indice)
                ? atual.AsArray()[indice]!
                : atual.AsObject()[parte]!;
        }

        return atual.AsObject();
    }

    private static byte[] BytesAdulterados(byte[] originais, Action<JsonObject> adulterar)
    {
        JsonObject envelope = EnvelopeDosBytes(originais);
        adulterar(envelope);

        byte[] adulterados = PerfilCanonicoV1.Instancia.Serializar(envelope);
        adulterados.Should().NotEqual(originais, "pré-condição: a adulteração tem de mudar os bytes");

        return adulterados;
    }

    private static SnapshotCanonico CongelarReferencia(ProcessoSeletivo processo, DadosEdital dados) =>
        new SnapshotPublicacaoCanonicalizer().Canonicalizar(new EntradaCanonicalizacao(processo, dados, EnvelopeDeReferencia.HashFixo, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(MetadadosFatosCongelados: EnvelopeDeReferencia.MetadadosFatosDeReferencia(), ValoresSelecionaveisCongelados: EnvelopeDeReferencia.ValoresSelecionaveisDeReferencia(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));

    private static VersaoConfiguracao PublicarSobreBytes(
        ProcessoSeletivo processo, DadosEdital dados, SnapshotCanonico congelado, byte[] bytes)
    {
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, EnvelopeDeReferencia.HashFixo), AtorUsuarioContaId = ExemploDeEnvelope.Ator, Contagem = ExemploDeEnvelopeDadosDoEdital.ContextoRico() });
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        processo.ClearDomainEvents();

        return publicacao.Value!;
    }
}
