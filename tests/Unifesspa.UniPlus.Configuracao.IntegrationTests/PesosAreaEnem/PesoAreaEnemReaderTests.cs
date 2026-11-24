namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PesosAreaEnem;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.PesosAreaEnem.PesoAreaEnemTestHelper;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class PesoAreaEnemReaderTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public PesoAreaEnemReaderTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Reader.ListarVivasAsync ordena por resolução e exclui removidos logicamente")]
    public async Task ListarVivas_OrdenaPorResolucaoEExcluiRemovidas()
    {
        // Prefixo único por execução: o banco é compartilhado na collection, então
        // filtramos o resultado às linhas deste teste para asserções determinísticas.
        string prefixo = $"Res. {Guid.NewGuid().ToString("N")[..12]}";
        string resA = $"{prefixo}-a";
        string resB = $"{prefixo}-b";
        string resExcluida = $"{prefixo}-d";

        await GravarResolucoesForaDeOrdemAsync(resA, resB, resExcluida);

        await RemoverPesoPorResolucaoAsync(resExcluida);

        await ConferirResolucoesVivasOrdenadasAsync(prefixo, resA, resB);
    }

    [Fact(DisplayName = "Leitura por resolução completa devolve as quatro linhas ordenadas pelo código do grupo, sem grupo ausente")]
    public async Task ObterPorResolucao_Completa_DevolveLinhasOrdenadasPorCodigo()
    {
        string resolucao = ResolucaoUnica();
        await GravarQuatroGruposAsync(resolucao);

        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(contextoLeitura).ObterPorResolucaoAsync($"  {resolucao} ");

        ConferirResolucaoCompleta(view, resolucao);
    }

    [Fact(DisplayName = "Leitura por resolução aponta como ausente o grupo sem linha viva, inclusive o removido logicamente")]
    public async Task ObterPorResolucao_Incompleta_ApontaGruposAusentes()
    {
        string resolucao = ResolucaoUnica();
        PesoAreaEnem removida = Nova(resolucao, GrupoCurso.HumanisticaII);
        await GravarGruposComUmParaRemocaoAsync(resolucao, removida);

        await RemoverGrupoAsync(removida);

        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(contextoLeitura).ObterPorResolucaoAsync(resolucao);

        ConferirGruposAusentes(view);
    }

    [Fact(DisplayName = "Leitura por resolução casa a entrada decomposta com a resolução gravada em NFC")]
    public async Task ObterPorResolucao_EntradaDecomposta_CasaComAGravadaEmNfc()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        string composta = $"Resolução {sufixo}".Normalize(System.Text.NormalizationForm.FormC);
        string decomposta = composta.Normalize(System.Text.NormalizationForm.FormD);
        await GravarPesoAsync(_fixture, Nova(decomposta));

        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(contextoLeitura).ObterPorResolucaoAsync(decomposta);

        view.Should().NotBeNull("o cadastro grava em NFC e a busca normaliza a entrada do mesmo jeito");
        view!.Resolucao.Should().Be(composta);
        view.Linhas.Should().ContainSingle().Which.Resolucao.Should().Be(composta);
    }

    [Theory(DisplayName = "Leitura por resolução com texto que não cabe na coluna devolve null, sem ir ao banco")]
    [InlineData("805\u0000")]
    [InlineData("Res.\n805")]
    [InlineData("Resolução com quarenta e um caracteres ..")]
    public async Task ObterPorResolucao_TextoInvalidoParaAColuna_DevolveNull(string resolucao)
    {
        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);

        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(contextoLeitura).ObterPorResolucaoAsync(resolucao);

        view.Should().BeNull("o Postgres recusaria o caractere nulo com erro de banco, e nenhuma linha gravada tem esse texto");
    }

    [Fact(DisplayName = "Leitura por resolução com não-caractere devolve null, sem exceção")]
    public async Task ObterPorResolucao_NaoCaractere_DevolveNull()
    {
        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);

        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(contextoLeitura).ObterPorResolucaoAsync("Res. 805" + (char)0xFFFE);

        view.Should().BeNull();
    }

    [Fact(DisplayName = "Leitura por resolução sem nenhuma linha viva devolve null")]
    public async Task ObterPorResolucao_Inexistente_DevolveNull()
    {
        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);

        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(contextoLeitura).ObterPorResolucaoAsync(ResolucaoUnica());

        view.Should().BeNull();
    }

    private async Task GravarResolucoesForaDeOrdemAsync(string resA, string resB, string resExcluida)
    {
        // Insere fora de ordem (B antes de A) para provar a ordenação do reader.
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminA))
        {
            contexto.PesosAreaEnem.Add(Nova(resB));
            contexto.PesosAreaEnem.Add(Nova(resA));
            contexto.PesosAreaEnem.Add(Nova(resExcluida));
            await contexto.SaveChangesAsync();
        }
    }

    private async Task RemoverPesoPorResolucaoAsync(string resExcluida)
    {
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem aExcluir = await contexto.PesosAreaEnem.SingleAsync(p => p.Resolucao == resExcluida);
            contexto.PesosAreaEnem.Remove(aExcluir);
            await contexto.SaveChangesAsync();
        }
    }

    private async Task GravarQuatroGruposAsync(string resolucao)
    {
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminA))
        {
            // Inseridos fora da ordem do código para a ordenação não sair de graça.
            contexto.PesosAreaEnem.AddRange(
                Nova(resolucao, GrupoCurso.Tecnologica),
                Nova(resolucao, GrupoCurso.SaudeEBiologicas),
                Nova(resolucao, GrupoCurso.HumanisticaII),
                Nova(resolucao, GrupoCurso.HumanisticaI));
            await contexto.SaveChangesAsync();
        }
    }

    private static void ConferirResolucaoCompleta(ResolucaoPesoAreaEnemView? view, string resolucao)
    {
        view.Should().NotBeNull();
        view!.Resolucao.Should().Be(resolucao);
        view.Completa.Should().BeTrue();
        view.GruposAusentes.Should().BeEmpty();
        view.Linhas.Select(l => l.GrupoCurso.Codigo).Should().Equal(
            GrupoCurso.HumanisticaI, GrupoCurso.HumanisticaII, GrupoCurso.SaudeEBiologicas, GrupoCurso.Tecnologica);
        view.Linhas.Should().OnlyContain(l => l.Resolucao == resolucao && l.Areas.Count == 5 && l.BaseLegal == BaseLegal);
    }

    private async Task GravarGruposComUmParaRemocaoAsync(string resolucao, PesoAreaEnem removida)
    {
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminA))
        {
            contexto.PesosAreaEnem.AddRange(
                Nova(resolucao, GrupoCurso.Tecnologica),
                Nova(resolucao, GrupoCurso.HumanisticaI),
                removida);
            await contexto.SaveChangesAsync();
        }
    }

    private async Task RemoverGrupoAsync(PesoAreaEnem removida)
    {
        await using (UniPlusDbContext contexto = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem aExcluir = await contexto.PesosAreaEnem.SingleAsync(p => p.Id == removida.Id);
            contexto.PesosAreaEnem.Remove(aExcluir);
            await contexto.SaveChangesAsync();
        }
    }

    private static void ConferirGruposAusentes(ResolucaoPesoAreaEnemView? view)
    {
        view.Should().NotBeNull();
        view!.Completa.Should().BeFalse();
        view.Linhas.Select(l => l.GrupoCurso.Codigo).Should().Equal(GrupoCurso.HumanisticaI, GrupoCurso.Tecnologica);
        view.GruposAusentes.Should().Equal(
            new GrupoAreaEnemView(GrupoCurso.HumanisticaII, "Humanística II"),
            new GrupoAreaEnemView(GrupoCurso.SaudeEBiologicas, "Saúde e Biológicas"));
    }

    private async Task ConferirResolucoesVivasOrdenadasAsync(string prefixo, string resA, string resB)
    {
        await using UniPlusDbContext contextoLeitura = _fixture.CreateDbContext(userId: null);
        var reader = new PesoAreaEnemReader(contextoLeitura);
        IReadOnlyList<PesoAreaEnemView> todas = await reader.ListarVivasAsync();

        string[] meus = [.. todas
            .Select(v => v.Resolucao)
            .Where(r => r.StartsWith(prefixo, StringComparison.Ordinal))];

        // O reader ordena por Resolucao ascendente e exclui o removido logicamente:
        // exatamente [resA, resB], nessa ordem (inserimos B antes de A).
        meus.Should().Equal([resA, resB]);
    }
}
