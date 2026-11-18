namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class EnvelopeLidoOrdemEIdentidadeTests
{
    [Fact]
    public void Reidratar_OrdemDeConteudoOpostaAosIds_ConservaASequencia()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        RegraAvaliada primeira = Regra("REGRA-A", "aaaaaaaa-0000-7000-8000-000000000099", "https://example.org/ato");
        RegraAvaliada segunda = Regra("REGRA-B", "aaaaaaaa-0000-7000-8000-000000000001", null);
        primeira.RegraId.CompareTo(segunda.RegraId).Should().BePositive();
        ResultadoConformidade conformidade = new([segunda, primeira], []);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo, conformidade: conformidade));
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo, CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        ApoioDoEnvelopeCodec.Envelope(congelado)["documentosExigidos"]!["obrigatoriedades"]!.AsArray()
            .Select(r => r!["regraCodigo"]!.GetValue<string>()).Should().Equal("REGRA-A", "REGRA-B");
        lido.Conformidade!.Regras.Select(r => r.RegraCodigo).Should().Equal("REGRA-A", "REGRA-B");
    }

    [Fact]
    public void Reidratar_Etapas_ConservaOsIdsDoJson()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo));
        EnvelopeReidratado lido = FidelidadeDoEnvelopeDeTeste.Conferir(processo, CorpusEnvelopeEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        lido.Configuracao.Avaliacao.Etapas.Select(e => e.Id).Should().Equal(ApoioDoEnvelopeCodec.Envelope(congelado)["etapas"]!.AsArray()
            .Select(e => Guid.Parse(e!["id"]!.GetValue<string>(), CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Reidratar_IdsDasEtapasTrocados_RecusaAsReferenciasOrfas()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        Result<EnvelopeReidratado> resultado = ReidratarComIdsNovos(processo);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        ProcessoSeletivo candidato = ProcessoSeletivo.Criar(processo.Nome, processo.TipoProcesso, processo.OrigemCandidatos,
            processo.UnidadeAdministradoraOrigemId, processo.UnidadeAdministradora, processo.Localidade, processo.IdentificadorLegivel!.Value);
        candidato.DefinirCronogramaFases(resultado.Value!.Configuracao.Cronograma.CronogramaFases, []).IsSuccess.Should().BeTrue();
        candidato.DefinirEtapas(resultado.Value.Configuracao.Avaliacao.Etapas).IsSuccess.Should().BeTrue();
        Result recusa = candidato.DefinirCriteriosDesempate(resultado.Value.Configuracao.Avaliacao.CriteriosDesempate);
        recusa.IsFailure.Should().BeTrue();
        recusa.Error!.Code.Should().Be("ProcessoSeletivo.EtapaRefDesempateInexistente");
        Result eliminacao = candidato.DefinirClassificacao(resultado.Value.Configuracao.Avaliacao.Classificacao!);
        eliminacao.IsFailure.Should().BeTrue();
        eliminacao.Error!.Code.Should().Be("ProcessoSeletivo.EtapaRefEliminacaoInexistente");
    }

    private static RegraAvaliada Regra(string codigo, string id, string? url) => new(
        RegraId: Guid.Parse(id, CultureInfo.InvariantCulture), RegraCodigo: codigo, Categoria: CategoriaObrigatoriedade.Outros,
        TipoProcessoCodigoAvaliado: "SiSU", Predicado: new ConcorrenciaDuplaObrigatoria(), Aprovada: true, Motivo: null,
        BaseLegal: "Lei de teste", AtoNormativoUrl: url, PortariaInterna: null, DescricaoHumana: "Regra de teste",
        VigenciaInicio: new DateOnly(2020, 1, 1), VigenciaFim: null, Hash: new string('a', 64));

    private static Result<EnvelopeReidratado> ReidratarComIdsNovos(ProcessoSeletivo processo)
    {
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes;
        JsonObject alterado = VersoesDoEnvelopeDeTeste.EnvelopeDosBytes(bytes);
        foreach (JsonNode? etapa in alterado["etapas"]!.AsArray())
        {
            etapa!["id"] = Guid.NewGuid().ToString("D");
        }
        return VersoesDoEnvelopeDeTeste.ReidratarComoAbertura(PerfilCanonicoV1.Instancia.Serializar(alterado));
    }
}
