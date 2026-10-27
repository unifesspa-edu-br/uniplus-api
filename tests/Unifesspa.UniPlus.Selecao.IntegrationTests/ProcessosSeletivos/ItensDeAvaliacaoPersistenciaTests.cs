namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Os itens de avaliação e a escala de atendimento da etapa contra o Postgres real: a identidade
/// do item sobrevive à regravação, e a escala volta da coluna jsonb na ordem canônica.
/// </summary>
public sealed class ItensDeAvaliacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static TipoEtapaSnapshot Tipo() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ENTREVISTA", "Entrevista", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    private static EtapaProcesso Entrevista(TipoEtapaSnapshot tipo, IReadOnlyList<ItemDeAvaliacaoDeclarado> itens, IReadOnlyList<NivelDeAtendimento>? escala) =>
        EtapaProcesso.Criar(
            "Entrevista", CaraterEtapa.Classificatoria, tipo, peso: 1m, ordem: 1, notaMaxima: 10m,
            formaDeAvaliacao: FormaDeAvaliacao.Humana, itensDeAvaliacao: itens, escalaDeAtendimento: escala).Value!;

    [Fact(DisplayName = "Os itens e a escala gravados voltam com o mesmo id, e a regravação com o id troca o rótulo na mesma linha")]
    public async Task ItensEEscala_SobrevivemAoBancoEARegravacaoPreservaOId()
    {
        Guid etapaId;
        TipoEtapaSnapshot tipo = Tipo();

        await using (SelecaoDbContext escrita = fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = ProcessoSeletivo.Criar(
                $"Certame de itens {Guid.CreateVersion7()}", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
                UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
                LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
            EtapaProcesso etapa = Entrevista(
                tipo,
                [new(null, "Clareza", 4m), new(null, "Domínio", 6m)],
                [new("Não atendeu", 0m), new("Atendeu", 1m), new("Atendeu parcialmente", 0.5m)]);
            processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            escrita.ProcessosSeletivos.Add(processo);
            await escrita.SaveChangesAsync();
            etapaId = etapa.Id;
        }

        Guid idDaClareza;
        await using (SelecaoDbContext regravacao = fixture.CreateDbContext())
        {
            EtapaProcesso etapa = await Carregar(regravacao, etapaId);
            etapa.EscalaDeAtendimento!.Select(static n => (n.Rotulo, n.Fracao)).Should().Equal(
                ("Atendeu", 1m), ("Atendeu parcialmente", 0.5m), ("Não atendeu", 0m));
            idDaClareza = etapa.ItensDeAvaliacao.Single(static i => i.Rotulo == "Clareza").Id;

            etapa.AtualizarDados(
                etapa.Nome, etapa.Carater, etapa.TipoEtapa, etapa.Peso, etapa.NotaMinima, etapa.Ordem, etapa.FaseCodigo,
                etapa.NotaMaxima, etapa.PassoDoLancamento, etapa.FormaDeAvaliacao, etapa.RegraConsolidacao,
                [new(idDaClareza, "Objetividade", 4m), new(null, "Postura", 6m)],
                etapa.EscalaDeAtendimento).IsSuccess.Should().BeTrue();
            await regravacao.SaveChangesAsync();
        }

        await using SelecaoDbContext conferencia = fixture.CreateDbContext();
        EtapaProcesso relida = await Carregar(conferencia, etapaId);

        relida.ItensDeAvaliacao.Should().HaveCount(2)
            .And.Contain(i => i.Id == idDaClareza && i.Rotulo == "Objetividade")
            .And.Contain(i => i.Rotulo == "Postura");
        (await conferencia.Set<ItemDeAvaliacao>().CountAsync(i => i.EtapaProcessoId == etapaId))
            .Should().Be(2, "o item que saiu da lista não deixa linha órfã");
    }

    private static Task<EtapaProcesso> Carregar(SelecaoDbContext contexto, Guid etapaId) =>
        contexto.Set<EtapaProcesso>().Include(e => e.ItensDeAvaliacao).FirstAsync(e => e.Id == etapaId);
}
