namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

public sealed class CascatasDasVersoesPersistidasTests(ProcessoSeletivoDbFixture fixture) : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly DateTimeOffset Inicio = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Persistir_CascatasDistintas_ConservaAberturaELigaOsAtos()
    {
        (VersaoConfiguracao abertura, VersaoConfiguracao sucessora) = await PersistirVersoesAsync(13);
        await using UniPlusDbContext db = fixture.CreateDbContext();
        VersaoConfiguracao primeira = await db.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.Id == abertura.Id);
        VersaoConfiguracao segunda = await db.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.Id == sucessora.Id);
        primeira.HashConfiguracao.Should().Be(abertura.HashConfiguracao);
        primeira.ConfiguracaoCongeladaCanonica.Should().Equal(abertura.ConfiguracaoCongeladaCanonica);
        segunda.NumeroVersao.Should().Be(2);
        segunda.AtoCriadorRetificaId.Should().Be(abertura.AtoCriadorId);
        DestinosDaPrimeiraOrigem(primeira).Should().Be(7);
        DestinosDaPrimeiraOrigem(segunda).Should().Be(1);
        segunda.ConfiguracaoCongeladaCanonica.Should().Equal(sucessora.ConfiguracaoCongeladaCanonica);
    }

    [Fact]
    public async Task ObterVersaoVigente_CascatasDistintas_SelecionaPeloInstante()
    {
        (VersaoConfiguracao abertura, VersaoConfiguracao sucessora) = await PersistirVersoesAsync(14);
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(db);
        (await repository.ObterVersaoVigenteAsync(abertura.ProcessoSeletivoId, Inicio.AddTicks(-10), CancellationToken.None)).Should().BeNull();
        VersaoConfiguracao? primeira = await repository.ObterVersaoVigenteAsync(abertura.ProcessoSeletivoId, Inicio, CancellationToken.None);
        primeira!.Id.Should().Be(abertura.Id);
        DestinosDaPrimeiraOrigem(primeira).Should().Be(7);
        VersaoConfiguracao? segunda = await repository.ObterVersaoVigenteAsync(abertura.ProcessoSeletivoId, Inicio.AddDays(2), CancellationToken.None);
        segunda!.Id.Should().Be(sucessora.Id);
        DestinosDaPrimeiraOrigem(segunda).Should().Be(1);
    }

    private async Task<(VersaoConfiguracao Abertura, VersaoConfiguracao Sucessora)> PersistirVersoesAsync(int variante)
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico(variante);
        VersaoConfiguracao abertura = VersaoConfiguracao.Abrir(processo.Id, Congelar(processo), Guid.CreateVersion7(), "teste", Inicio);
        processo.DefinirCascataRemanejamento(CascataCurta(processo.Cascata!)).IsSuccess.Should().BeTrue();
        VersaoConfiguracao sucessora = VersaoConfiguracao.Suceder(abertura,
            Congelar(processo, new RetificacaoDoEdital(abertura.AtoCriadorId, "Corrige a ordem de remanejamento")), Guid.CreateVersion7(), "teste", Inicio.AddDays(1));
        await using UniPlusDbContext db = fixture.CreateDbContext();
        db.Add(processo);
        db.AddRange(abertura, sucessora);
        await db.SaveChangesAsync();
        return (abertura, sucessora);
    }

    private static DocumentoCongelado Congelar(ProcessoSeletivo processo, RetificacaoDoEdital? retificacao = null)
    {
        Unifesspa.UniPlus.Selecao.Application.Abstractions.SnapshotCanonico snapshot = ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo, retificacao));
        return new DocumentoCongelado(snapshot.Bytes, snapshot.SchemaVersion, snapshot.AlgoritmoHash, ExemploDeEnvelope.HashDocumento);
    }

    private static ConfiguracaoCascataRemanejamento CascataCurta(ConfiguracaoCascataRemanejamento original) =>
        ConfiguracaoCascataRemanejamento.Criar(original.Regra, "AC",
            [.. original.Destinos.GroupBy(d => d.ModalidadeOrigemCodigo).Select(g => DestinoRemanejamento.Criar(g.Key, 1, "AC").Value!)]).Value!;

    private static int DestinosDaPrimeiraOrigem(VersaoConfiguracao versao) =>
        JsonNode.Parse(versao.ConfiguracaoCongelada)!["cascataRemanejamento"]!["ordens"]![0]!["destinos"]!.AsArray().Count;
}
