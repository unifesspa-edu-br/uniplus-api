namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Os lotes de notas do ENEM que os testes de persistência gravam direto pelo repositório.</summary>
internal static class LotesDoEnemDeTeste
{
    public const string Edicao = "2025";

    public static readonly string Hash = new('a', 64);

    /// <summary>O processo, a versão da configuração e a etapa em que o lote é importado.</summary>
    public sealed record Escopo(Guid ProcessoId, Guid VersaoId, Guid EtapaId);

    public static async Task<Escopo> NovoEscopoAsync(InscricaoDbFixture fixture)
    {
        await using SelecaoDbContext semente = fixture.CreateDbContext();
        ProcessoSeletivo processo = await InscricoesDoEnemDeTeste.NovoProcessoAsync(semente);
        Guid versao = await InscricoesDoEnemDeTeste.NovaVersaoAsync(fixture, processo.Id);
        return new Escopo(processo.Id, versao, Guid.CreateVersion7());
    }

    public static Task<Guid> InscreverAsync(
        InscricaoDbFixture fixture,
        Escopo escopo,
        PersonasDeTeste.PessoaFicticia pessoa,
        SituacaoInscricao situacao = SituacaoInscricao.Submetida,
        string? edicao = Edicao) =>
        InscricoesDoEnemDeTeste.NovaInscricaoAsync(fixture, escopo.ProcessoId, pessoa, situacao, edicao);

    public static ResultadoDaAreaNoRetorno[] Areas(decimal nota = 600m, SituacaoDaProvaNoEnem situacao = SituacaoDaProvaNoEnem.Presente) =>
        [.. AreaDoEnem.Todas.Select(a => new ResultadoDaAreaNoRetorno(a, nota, situacao))];

    public static LoteDeNotasDoEnem NovoLote(
        Escopo escopo,
        ResultadoDoCruzamento cruzamento,
        string arquivoHash,
        DateTimeOffset em,
        string leiaute = "INEP_ENEM_RETORNO",
        IReadOnlyList<AvaliacaoApurada>? avaliacoes = null) =>
        LoteDeNotasDoEnem.Registrar(
            escopo.ProcessoId, escopo.EtapaId, Edicao, leiaute, "v1", Hash, arquivoHash, "sub-admin", em,
            cruzamento.ComNota.Count + cruzamento.LinhasSemInscrito.Count, escopo.VersaoId, cruzamento, avaliacoes ?? []);

    public static ResultadoDoCruzamento ComNota(params (Guid Inscricao, string NumeroEnem, decimal Nota)[] inscritos) =>
        new([.. inscritos.Select(i => new InscritoComNota(i.Inscricao, i.NumeroEnem, Areas(i.Nota)))], [], [], []);

    /// <summary>Grava os lotes um a um, cada um na sua transação, na ordem em que vêm.</summary>
    public static async Task SalvarAsync(InscricaoDbFixture fixture, params LoteDeNotasDoEnem[] lotes)
    {
        foreach (LoteDeNotasDoEnem lote in lotes)
        {
            await using SelecaoDbContext context = fixture.CreateDbContext();
            await new LoteDeNotasDoEnemRepository(context, fixture.Encryption).AdicionarAsync(lote);
            await context.SaveChangesAsync();
        }
    }
}
