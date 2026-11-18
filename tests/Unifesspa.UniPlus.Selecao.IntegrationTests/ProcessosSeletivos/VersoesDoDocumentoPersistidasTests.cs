namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class VersoesDoDocumentoPersistidasTests(ProcessoSeletivoDbFixture fixture) : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact]
    public async Task Persistir_RemocaoDoNomeAbreviado_ConservaAberturaEGravaRegraNula()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico(1);
        processo.DefinirConfiguracaoDivulgacao(ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!)
            .IsSuccess.Should().BeTrue();
        VersaoConfiguracao abertura = Abrir(processo);
        processo.DefinirConfiguracaoDivulgacao(ConfiguracaoDivulgacao.Criar(["numero_inscricao"], null).Value!).IsSuccess.Should().BeTrue();
        VersaoConfiguracao sucessora = Suceder(processo, abertura);
        await PersistirAsync(processo, abertura, sucessora);
        await using UniPlusDbContext db = fixture.CreateDbContext();
        VersaoConfiguracao antiga = await LerAsync(db, abertura.Id);
        VersaoConfiguracao nova = await LerAsync(db, sucessora.Id);
        ConferirImutabilidade(antiga, abertura);
        Divulgacao(antiga)["regraNomeAbreviado"]!.GetValue<string>().Should().Be(RegrasDeNomeAbreviado.Vigente);
        Divulgacao(nova)["camposPublicos"]!.AsArray().Select(n => n!.GetValue<string>()).Should().Equal("numero_inscricao");
        Divulgacao(nova)["regraNomeAbreviado"].Should().BeNull();
    }

    [Fact]
    public async Task Persistir_ExigenciaAlterada_ConservaHashEBytesDaVersaoAnterior()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico(2, comArvoreSatisfacao: true);
        VersaoConfiguracao abertura = Abrir(processo);
        await PersistirAsync(processo, abertura);
        await AlterarExigenciaAsync(processo.Id);
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo atual = (await ProcessoSeletivoRepositoryDeTeste.Criar(db).ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
        atual.DocumentosExigidos.Should().BeEmpty();
        JsonNode.Parse(abertura.ConfiguracaoCongelada)!["documentosExigidos"]!["exigencias"]!.AsArray().Should().NotBeEmpty();
        VersaoConfiguracao antiga = await LerAsync(db, abertura.Id);
        antiga.HashConfiguracao.Should().Be(abertura.HashConfiguracao);
        antiga.ConfiguracaoCongeladaCanonica.Should().Equal(abertura.ConfiguracaoCongeladaCanonica);
    }

    [Fact]
    public async Task Persistir_IdentificadorDeclaradoNaSucessora_TrocaPosteriorRecusa()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico(3);
        IdentificadoresDeTeste.Retirar(processo);
        VersaoConfiguracao abertura = Abrir(processo);
        IdentificadorLegivel declarado = IdentificadoresDeTeste.Novo();
        processo.DefinirIdentificadorLegivel(declarado).IsSuccess.Should().BeTrue();
        VersaoConfiguracao sucessora = Suceder(processo, abertura);
        await PersistirAsync(processo, abertura, sucessora);
        await using UniPlusDbContext db = fixture.CreateDbContext();
        EnvelopeReidratado anterior = ApoioDoEnvelopeCodec.Reidratado(await LerAsync(db, abertura.Id));
        EnvelopeReidratado atual = ApoioDoEnvelopeCodec.Reidratado(await LerAsync(db, sucessora.Id));
        anterior.Grafo.IdentificadorLegivel.Should().BeNull();
        atual.Grafo.IdentificadorLegivel.Should().Be(declarado);
        ConferenciaDoIdentificadorDaRetificacao.Conferir(atual.Grafo.IdentificadorLegivel, IdentificadoresDeTeste.Novo())
            .Should().ContainSingle().Which.Error.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel);
    }

    private async Task AlterarExigenciaAsync(Guid processoId)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = (await ProcessoSeletivoRepositoryDeTeste.Criar(db).ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        processo.DocumentosExigidos.Should().NotBeEmpty();
        processo.DefinirDocumentosExigidos([]).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private async Task PersistirAsync(ProcessoSeletivo processo, params VersaoConfiguracao[] versoes)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        db.Add(processo);
        db.AddRange(versoes);
        await db.SaveChangesAsync();
    }

    private static void ConferirImutabilidade(VersaoConfiguracao lida, VersaoConfiguracao original)
    {
        lida.ConfiguracaoCongeladaCanonica.Should().Equal(original.ConfiguracaoCongeladaCanonica);
        lida.HashConfiguracao.Should().Be(original.HashConfiguracao);
    }

    private static Task<VersaoConfiguracao> LerAsync(UniPlusDbContext db, Guid id) =>
        db.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.Id == id);

    private static JsonNode Divulgacao(VersaoConfiguracao versao) => JsonNode.Parse(versao.ConfiguracaoCongelada)!["divulgacao"]!;

    private static DocumentoCongelado Congelar(ProcessoSeletivo processo, RetificacaoDoEdital? retificacao = null)
    {
        SnapshotCanonico snapshot = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo, retificacao));
        return new DocumentoCongelado(snapshot.Bytes, snapshot.SchemaVersion, snapshot.AlgoritmoHash, CorpusEnvelope.HashDocumento);
    }

    private static VersaoConfiguracao Abrir(ProcessoSeletivo processo) => VersaoConfiguracao.Abrir(
        processo.Id, Congelar(processo), Guid.CreateVersion7(), "teste", new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    private static VersaoConfiguracao Suceder(ProcessoSeletivo processo, VersaoConfiguracao anterior) => VersaoConfiguracao.Suceder(
        anterior, Congelar(processo, new RetificacaoDoEdital(anterior.AtoCriadorId, "Correção da configuração")),
        Guid.CreateVersion7(), "teste", new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero));
}
