namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Xunit;

/// <summary>
/// O que o Postgres real cobra quando duas fases TROCAM de <c>Ordem</c> entre si — o caso em
/// que reordenar comandos não resolveria, porque cada <c>UPDATE</c> quer o slot de
/// <c>ux_fases_cronograma_processo_ordem</c> que o outro ainda não liberou.
/// </summary>
/// <remarks>
/// <para>
/// O agregado recusa a permutação cíclica antes de chegar aqui, com erro nomeado, apoiado na
/// premissa de que o EF não resolveria a troca. Este teste mede a premissa direto no contexto,
/// sem passar pelo domínio — e ela se confirma.
/// </para>
/// <para>
/// <b>Por que isto não contradiz o caso das bancas da etapa.</b> Lá duas bancas trocam de
/// código e gravam sem erro, porque a coleção é substituída por inteiro: são <c>INSERT</c> e
/// <c>DELETE</c> de linhas diferentes, e o EF os ordena de modo que o <c>DELETE</c> libere o
/// slot antes do <c>INSERT</c> que o quer. Aqui são duas linhas RETIDAS trocando valores entre
/// si — <c>UPDATE</c> contra <c>UPDATE</c> —, e não existe ordenação que resolva: cada uma
/// depende de a outra ceder o valor primeiro. São casos distintos, e confundi-los faria
/// remover um guard que ainda protege.
/// </para>
/// </remarks>
public sealed class FaseOrdemPermutacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact(DisplayName = "Duas fases retidas trocando de Ordem entre si não gravam num SaveChanges só")]
    public async Task DuasFasesTrocamDeOrdem_EfDetectaDependenciaCircular()
    {
        Guid processoId = await SemearProcessoComDuasFasesAsync();

        await using SelecaoDbContext troca = fixture.CreateDbContext();
        List<FaseCronograma> fases = await troca.Set<FaseCronograma>()
            .Where(f => f.ProcessoSeletivoId == processoId)
            .OrderBy(f => f.Ordem)
            .ToListAsync();

        fases.Should().HaveCount(2);

        // A@1, B@2 → A@2, B@1. Nenhuma ordem de UPDATE resolve isso sozinha: a primeira linha
        // a mudar quer o valor que a segunda ainda ocupa.
        troca.Entry(fases[0]).Property(nameof(FaseCronograma.Ordem)).CurrentValue = 2;
        troca.Entry(fases[1]).Property(nameof(FaseCronograma.Ordem)).CurrentValue = 1;

        Func<Task> gravar = async () => await troca.SaveChangesAsync();

        // É esta exceção que o guard de domínio existe para evitar: ela escapa do Result
        // pattern e chega ao operador como 500, sem dizer o que ele fez de errado.
        (await gravar.Should().ThrowAsync<InvalidOperationException>(
            "o EF ordena comandos, e nenhuma ordem resolve duas linhas que trocam o mesmo valor "
            + "coberto por índice único — é a premissa que sustenta o guard de permutação cíclica"))
            .WithMessage("*circular dependency*");

        // E o estado no banco não mudou: a transação inteira não chegou a ser emitida.
        await using SelecaoDbContext leitura = fixture.CreateDbContext();
        Dictionary<string, int> ordemPorCodigo = await leitura.Set<FaseCronograma>()
            .Where(f => f.ProcessoSeletivoId == processoId)
            .ToDictionaryAsync(f => f.Codigo, f => f.Ordem);

        ordemPorCodigo["INSCRICAO"].Should().Be(1);
        ordemPorCodigo["AVALIACAO"].Should().Be(2);
    }

    private async Task<Guid> SemearProcessoComDuasFasesAsync()
    {
        await using SelecaoDbContext escrita = fixture.CreateDbContext();

        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"Certame de permutação {Guid.CreateVersion7()}",
            TipoProcesso.SiSU,
            OrigemCandidatos.InscricaoPropria,
            Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirCronogramaFases(
            [Fase(1, "INSCRICAO"), Fase(2, "AVALIACAO")], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        escrita.ProcessosSeletivos.Add(processo);
        await escrita.SaveChangesAsync();
        return processo.Id;
    }

    private static FaseCronograma Fase(int ordem, string codigo) => FaseCronograma.Criar(
        ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Delegada,
        agrupaEtapas: false, permiteComplementacao: false,
        coletaInscricao: false, coletaSolicitacaoIsencao: false,
        inicio: null, fim: null, produtos: [], faseConcluinteCodigo: null,
        emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null).Value!;
}
