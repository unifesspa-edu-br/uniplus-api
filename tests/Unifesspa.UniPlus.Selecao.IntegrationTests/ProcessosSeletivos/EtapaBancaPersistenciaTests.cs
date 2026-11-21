namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// As bancas da etapa contra o Postgres real: o que a restrição única da posição no tipo cobra
/// quando a mesma configuração é gravada de novo, reordenada ou trocada de código.
/// </summary>
public sealed class EtapaBancaPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly Guid TipoBanca = new("dddd1111-0000-4000-8000-000000000001");
    private static readonly Guid OutroTipoBanca = new("dddd1111-0000-4000-8000-000000000002");

    /// <summary>
    /// Regravar a etapa sem mudar nada é o caso mais comum do editor: o operador salva de
    /// novo depois de mexer noutro campo. O corpo devolve o id que leu, e a linha é mantida.
    /// </summary>
    [Fact(DisplayName = "Regravar a etapa com o id da banca mantém a mesma linha")]
    public async Task RegravarAMesmaBancaComOId_MantemALinha()
    {
        (Guid processoId, Guid etapaId) = await GravarEtapaComAsync("Certame de bancas", Banca(TipoBanca, "BANCA_TITULOS"));
        Guid idGravado = (await RelerAsync(etapaId)).Bancas.Single().Id;

        await RegravarAsync(
            processoId,
            etapaId,
            [Banca(TipoBanca, "BANCA_TITULOS", idGravado)],
            "regravar a etapa sem mudar a banca é o caso comum do editor, e não pode falhar por causa de como a coleção é substituída");

        EtapaProcesso relida = await RelerAsync(etapaId);
        relida.Bancas.Should().ContainSingle("a banca continua uma só — não duplicou nem sumiu");
        relida.Bancas.Single().Id.Should().Be(idGravado);
    }

    /// <summary>Sem o id, a banca declarada é nova: a antiga sai e a nova entra na mesma posição.</summary>
    [Fact(DisplayName = "Regravar a etapa sem o id da banca recria a linha sem colidir")]
    public async Task RegravarAMesmaBancaSemOId_RecriaALinha()
    {
        (Guid processoId, Guid etapaId) = await GravarEtapaComAsync("Certame de recriação", Banca(TipoBanca, "BANCA_TITULOS"));
        Guid idGravado = (await RelerAsync(etapaId)).Bancas.Single().Id;

        await RegravarAsync(
            processoId, etapaId, [Banca(TipoBanca, "BANCA_TITULOS")],
            "a banca nova ocupa a posição da que saiu, e a restrição adiada só confere ao fim da transação");

        EtapaProcesso relida = await RelerAsync(etapaId);
        relida.Bancas.Should().ContainSingle();
        relida.Bancas.Single().Id.Should().NotBe(idGravado);
    }

    /// <summary>
    /// Duas bancas do mesmo tipo e do mesmo código trocam de posição entre si. Cada UPDATE quer
    /// a posição que o outro ainda ocupa: só a restrição adiada ao fim da transação permite.
    /// </summary>
    [Fact(DisplayName = "Duas bancas do mesmo tipo que trocam de posição entre si continuam gravando")]
    public async Task TrocaDePosicoesEntreBancasDoMesmoTipo_NaoColide()
    {
        (Guid processoId, Guid etapaId) = await GravarEtapaComAsync(
            "Certame de posições", Banca(TipoBanca, "BANCA_TITULOS"), Banca(TipoBanca, "BANCA_TITULOS"));
        IReadOnlyList<BancaDaEtapa> gravadas = [.. (await RelerAsync(etapaId)).Bancas.OrderBy(b => b.PosicaoNoTipo)];
        gravadas.Select(b => b.PosicaoNoTipo).Should().Equal(1, 2);

        await RegravarAsync(
            processoId, etapaId,
            [Banca(TipoBanca, "BANCA_TITULOS", gravadas[1].Id), Banca(TipoBanca, "BANCA_TITULOS", gravadas[0].Id)],
            "trocar a posição de duas bancas é configuração legítima, e a restrição única só pode ser conferida ao fim da transação");

        IReadOnlyList<BancaDaEtapa> relidas = [.. (await RelerAsync(etapaId)).Bancas.OrderBy(b => b.PosicaoNoTipo)];
        relidas.Select(b => b.Id).Should().Equal(gravadas[1].Id, gravadas[0].Id);
    }

    /// <summary>Duas bancas que trocam de código entre si, sem o id: cada uma é uma linha nova.</summary>
    [Fact(DisplayName = "Duas bancas que trocam de código entre si continuam gravando")]
    public async Task TrocaDeCodigosEntreBancas_NaoColide()
    {
        (Guid processoId, Guid etapaId) = await GravarEtapaComAsync(
            "Certame de troca", Banca(TipoBanca, "BANCA_A"), Banca(OutroTipoBanca, "BANCA_B"));

        await RegravarAsync(
            processoId, etapaId, [Banca(TipoBanca, "BANCA_B"), Banca(OutroTipoBanca, "BANCA_A")],
            "trocar os códigos de lugar é configuração legítima");

        EtapaProcesso relida = await RelerAsync(etapaId);
        relida.Bancas.Select(b => (b.TipoBancaOrigemId, b.Codigo))
            .Should().BeEquivalentTo([(TipoBanca, "BANCA_B"), (OutroTipoBanca, "BANCA_A")]);
    }

    private static BancaDaEtapa Banca(Guid tipo, string codigo, Guid? idDaBancaQueSubstitui = null) =>
        BancaDaEtapa.Criar(tipo, codigo, "Banca de teste", idDaBancaQueSubstitui: idDaBancaQueSubstitui);

    private static EtapaProcesso NovaEtapaDeTitulos() =>
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova de títulos", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_TITULOS", "Prova de títulos", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static ProcessoSeletivo NovoProcesso(string nome) =>
        ProcessoSeletivo.Criar(
            $"{nome} {Guid.CreateVersion7()}",
            TiposDeProcessoDeTeste.SiSU,
            OrigemCandidatos.InscricaoPropria,
            Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private async Task<(Guid ProcessoId, Guid EtapaId)> GravarEtapaComAsync(string nome, params BancaDaEtapa[] bancas)
    {
        await using UniPlusDbContext escrita = fixture.CreateDbContext();
        ProcessoSeletivo processo = NovoProcesso(nome);
        EtapaProcesso etapa = NovaEtapaDeTitulos();
        etapa.DefinirBancas(bancas);
        processo.DefinirEtapas([etapa]).IsSuccess.Should().BeTrue();
        escrita.ProcessosSeletivos.Add(processo);
        await escrita.SaveChangesAsync();
        return (processo.Id, etapa.Id);
    }

    private async Task RegravarAsync(Guid processoId, Guid etapaId, BancaDaEtapa[] bancas, string motivo)
    {
        await using UniPlusDbContext regravacao = fixture.CreateDbContext();
        ProcessoSeletivo tracked = await regravacao.ProcessosSeletivos
            .Include(p => p.Etapas).ThenInclude(e => e.Bancas)
            .FirstAsync(p => p.Id == processoId);
        tracked.Etapas.Single(e => e.Id == etapaId).DefinirBancas(bancas);

        Func<Task> gravar = async () => await regravacao.SaveChangesAsync();

        await gravar.Should().NotThrowAsync(motivo);
    }

    private async Task<EtapaProcesso> RelerAsync(Guid etapaId)
    {
        await using UniPlusDbContext conferencia = fixture.CreateDbContext();
        return await conferencia.Set<EtapaProcesso>()
            .Include(e => e.Bancas)
            .FirstAsync(e => e.Id == etapaId);
    }
}
