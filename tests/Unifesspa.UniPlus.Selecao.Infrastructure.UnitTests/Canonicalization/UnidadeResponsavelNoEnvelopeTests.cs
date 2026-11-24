namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Conferencias;

public sealed class UnidadeResponsavelNoEnvelopeTests
{
    [Theory]
    [InlineData("origemId")]
    [InlineData("codigo")]
    [InlineData("sigla")]
    [InlineData("nome")]
    public void UnidadeResponsavel_ChaveAusente_RecusaNoObjeto(string chave)
    {
        JsonObject unidade = new() { ["origemId"] = Guid.NewGuid(), ["codigo"] = "CEPS", ["sigla"] = "CEPS", ["nome"] = "Centro" };
        unidade.Remove(chave);
        JsonObject fase = new() { ["unidadeResponsavel"] = unidade };
        LeitorEnvelope leitor = new();

        LeituraDoCronograma.LerUnidadeResponsavel(leitor, fase, "cronogramaFases.fases[0]");

        leitor.Erro.Message.Should().Be($"Envelope malformado em 'cronogramaFases.fases[0].unidadeResponsavel': a chave '{chave}' está ausente.");
    }

    [Fact]
    public void EnvelopeCompleto_UnidadeResponsavel_RestauraTodosOsValores()
    {
        EntradaCanonicalizacao entrada = ExemploDeConfiguracao.Entrada();
        TipoDeAtoCongelado ato = new("RESULTADO_CLASSIFICACAO", "Resultado da classificação", true, true, false);
        entrada = entrada with { DadosDeCatalogo = new(TiposDeAtoCongelados: new Dictionary<string, TipoDeAtoCongelado> { [ato.Codigo] = ato }) };
        entrada.Processo.DefinirTaxaInscricao(ConfiguracaoTaxaInscricao.Criar(false, null, null).Value!).IsSuccess.Should().BeTrue();
        UnidadeResponsavelSnapshot esperada = entrada.Processo.CronogramaFases.Single().UnidadeResponsavel!;
        SnapshotCanonico documento = new SnapshotPublicacaoCanonicalizer().Canonicalizar(entrada);
        DocumentoCongelado congelado = new(documento.Bytes, documento.SchemaVersion, documento.AlgoritmoHash, entrada.HashDocumento);
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(entrada.Processo.Id, congelado, Guid.NewGuid(), "usuario-ficticio", DateTimeOffset.UnixEpoch);

        Result<EnvelopeReidratado> lido = new EnvelopeCodec().Reidratar(versao);

        lido.IsSuccess.Should().BeTrue(lido.Error?.Message);
        lido.Value!.Configuracao.Cronograma.CronogramaFases.Single().UnidadeResponsavel.Should().BeEquivalentTo(esperada);
    }
}
