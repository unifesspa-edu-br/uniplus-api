namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Regras.Enums;
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
        (Guid processoId, Guid etapaId) = await GravarEtapaComAsync("Certame de bancas", Banca(TipoBanca, TipoDeBanca.Entrevista));
        Guid idGravado = (await RelerAsync(etapaId)).Bancas.Single().Id;

        await RegravarAsync(
            processoId,
            etapaId,
            [Banca(TipoBanca, TipoDeBanca.Entrevista, idGravado)],
            "regravar a etapa sem mudar a banca é o caso comum do editor, e não pode falhar por causa de como a coleção é substituída");

        EtapaProcesso relida = await RelerAsync(etapaId);
        relida.Bancas.Should().ContainSingle("a banca continua uma só — não duplicou nem sumiu");
        relida.Bancas.Single().Id.Should().Be(idGravado);
    }

    /// <summary>Sem o id, a banca declarada é nova: a antiga sai e a nova entra na mesma posição.</summary>
    [Fact(DisplayName = "Regravar a etapa sem o id da banca recria a linha sem colidir")]
    public async Task RegravarAMesmaBancaSemOId_RecriaALinha()
    {
        (Guid processoId, Guid etapaId) = await GravarEtapaComAsync("Certame de recriação", Banca(TipoBanca, TipoDeBanca.Entrevista));
        Guid idGravado = (await RelerAsync(etapaId)).Bancas.Single().Id;

        await RegravarAsync(
            processoId, etapaId, [Banca(TipoBanca, TipoDeBanca.Entrevista)],
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
            "Certame de posições", Banca(TipoBanca, TipoDeBanca.Entrevista), Banca(TipoBanca, TipoDeBanca.Entrevista));
        IReadOnlyList<BancaDaEtapa> gravadas = [.. (await RelerAsync(etapaId)).Bancas.OrderBy(b => b.PosicaoNoTipo)];
        gravadas.Select(b => b.PosicaoNoTipo).Should().Equal(1, 2);

        await RegravarAsync(
            processoId, etapaId,
            [Banca(TipoBanca, TipoDeBanca.Entrevista, gravadas[1].Id), Banca(TipoBanca, TipoDeBanca.Entrevista, gravadas[0].Id)],
            "trocar a posição de duas bancas é configuração legítima, e a restrição única só pode ser conferida ao fim da transação");

        IReadOnlyList<BancaDaEtapa> relidas = [.. (await RelerAsync(etapaId)).Bancas.OrderBy(b => b.PosicaoNoTipo)];
        relidas.Select(b => b.Id).Should().Equal(gravadas[1].Id, gravadas[0].Id);
    }

    /// <summary>Duas bancas que trocam de tipo entre si, sem o id: cada uma é uma linha nova.</summary>
    [Fact(DisplayName = "Duas bancas que trocam de tipo entre si continuam gravando")]
    public async Task TrocaDeTiposEntreBancas_NaoColide()
    {
        (Guid processoId, Guid etapaId) = await GravarEtapaComAsync(
            "Certame de troca", Banca(TipoBanca, TipoDeBanca.AnaliseDocumental), Banca(OutroTipoBanca, TipoDeBanca.Heteroidentificacao));

        await RegravarAsync(
            processoId, etapaId, [Banca(TipoBanca, TipoDeBanca.Heteroidentificacao), Banca(OutroTipoBanca, TipoDeBanca.AnaliseDocumental)],
            "trocar os tipos de lugar é configuração legítima");

        EtapaProcesso relida = await RelerAsync(etapaId);
        relida.Bancas.Select(b => (b.Tipo.OrigemId, b.Tipo.Codigo))
            .Should().BeEquivalentTo([(TipoBanca, TipoDeBanca.Heteroidentificacao), (OutroTipoBanca, TipoDeBanca.AnaliseDocumental)]);
    }

    /// <summary>A cópia do tipo (código, nome, papel, marcas e tipos de decisão julgados) volta do banco como foi gravada.</summary>
    [Fact(DisplayName = "A cópia do tipo de banca volta do banco com papel, marcas e tipos de decisão julgados")]
    public async Task CopiaDoTipo_VoltaDoBancoComoFoiGravada()
    {
        TipoDeBancaCopiado copia = TiposDeBancaCopiadosDeTeste.Com(
            TipoDeBanca.Heteroidentificacao, PapelDaBanca.AvaliaOMerito, TipoBanca, "Comissão de heteroidentificação", exigeDuasOuMaisBancas: true, encerraRecursoNaPrimeiraInstancia: false);
        (_, Guid etapaId) = await GravarEtapaComAsync("Certame da cópia", BancaDaEtapa.Criar(copia));

        EtapaProcesso relida = await RelerAsync(etapaId);

        relida.Bancas.Single().Tipo.Should().Be(copia);
    }

    /// <summary>A lista da cópia é gravada pelos códigos de contrato dos tipos de decisão, na coluna da própria banca.</summary>
    [Fact(DisplayName = "A cópia do tipo de banca grava cada tipo de decisão pelo código de contrato, e não pelo nome do enum")]
    public async Task CopiaDoTipo_GravaOsCodigosDeContratoDosTiposDeDecisao()
    {
        TipoDeBancaCopiado copia = new(
            TipoBanca, TipoDeBanca.Heteroidentificacao, "Comissão de heteroidentificação", PapelDaBanca.AvaliaOMerito,
            exigeDuasOuMaisBancas: false, encerraRecursoNaPrimeiraInstancia: false,
            [TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.IsencaoDaTaxa]);
        (_, Guid etapaId) = await GravarEtapaComAsync("Certame dos códigos da cópia", BancaDaEtapa.Criar(copia));
        Guid bancaId = (await RelerAsync(etapaId)).Bancas.Single().Id;

        await using UniPlusDbContext conferencia = fixture.CreateDbContext();
        string gravado = await conferencia.Database
            .SqlQuery<string>($"SELECT array_to_string(tipos_de_decisao_julgados, ',') AS \"Value\" FROM selecao.bancas_da_etapa WHERE id = {bancaId}")
            .SingleAsync();

        gravado.Should().Be("ANALISE_DOCUMENTAL,ISENCAO_DA_TAXA");
    }

    /// <summary>A banca que troca de tipo, com o mesmo id, passa a ter a cópia do tipo novo.</summary>
    [Fact(DisplayName = "A banca que troca de tipo passa a guardar a cópia do tipo novo")]
    public async Task TrocaDeTipo_GravaACopiaDoTipoNovo()
    {
        (Guid processoId, Guid etapaId) = await GravarEtapaComAsync("Certame da troca de cópia", Banca(TipoBanca, TipoDeBanca.AnaliseDocumental));
        Guid idGravado = (await RelerAsync(etapaId)).Bancas.Single().Id;

        await RegravarAsync(processoId, etapaId, [Banca(OutroTipoBanca, TipoDeBanca.AnaliseDeRecursos, idGravado)], "trocar o tipo da banca é configuração legítima");

        BancaDaEtapa relida = (await RelerAsync(etapaId)).Bancas.Single();
        relida.Id.Should().Be(idGravado);
        relida.Tipo.Should().Be(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDeRecursos, OutroTipoBanca));
    }

    private static BancaDaEtapa Banca(Guid origemDoTipo, TipoDeBanca tipo, Guid? idDaBancaQueSubstitui = null) =>
        BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(tipo, origemDoTipo), idDaBancaQueSubstitui: idDaBancaQueSubstitui);

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
