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
/// As bancas da etapa contra o Postgres real: o que o índice único cobra quando a mesma
/// configuração é gravada duas vezes.
/// </summary>
public sealed class EtapaBancaPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly Guid TipoBanca = new("dddd1111-0000-4000-8000-000000000001");
    private static readonly Guid OutroTipoBanca = new("dddd1111-0000-4000-8000-000000000002");

    /// <summary>
    /// Regravar a etapa sem mudar nada é o caso mais comum do editor — o operador salva de
    /// novo depois de mexer noutro campo. A coleção é substituída por inteiro, e substituir a
    /// banca por uma instância nova de MESMO código põe o INSERT da nova e o DELETE da órfã
    /// disputando o mesmo slot de <c>ux_bancas_da_etapa_codigo</c> na mesma transação.
    /// </summary>
    [Fact(DisplayName = "Regravar a etapa com a mesma banca não colide no índice único")]
    public async Task RegravarAMesmaBanca_NaoColide()
    {
        (Guid processoId, Guid etapaId) = await GravarEtapaComAsync("Certame de bancas", Banca(TipoBanca, "BANCA_TITULOS"));

        // Instância NOVA com o MESMO código — é o que o handler monta a cada PUT, porque o
        // corpo declara o tipo de banca, não a linha que já existe.
        await RegravarAsync(
            processoId,
            etapaId,
            [Banca(TipoBanca, "BANCA_TITULOS")],
            "regravar a etapa sem mudar a banca é o caso comum do editor, e não pode falhar por causa de como a coleção é substituída");

        EtapaProcesso relida = await RelerAsync(etapaId);
        relida.Bancas.Should().ContainSingle("a banca continua uma só — não duplicou nem sumiu");
        relida.Bancas.Single().Codigo.Should().Be("BANCA_TITULOS");
    }

    /// <summary>
    /// Duas bancas que trocam de código entre si. É o caso em que reordenar comandos não
    /// resolve: cada INSERT quer o slot que o outro DELETE ainda não liberou, e não existe
    /// ordem que sirva para os dois.
    /// </summary>
    [Fact(DisplayName = "Duas bancas que trocam de código entre si continuam gravando")]
    public async Task TrocaDeCodigosEntreBancas_NaoColide()
    {
        (Guid processoId, Guid etapaId) = await GravarEtapaComAsync(
            "Certame de troca", Banca(TipoBanca, "BANCA_A"), Banca(OutroTipoBanca, "BANCA_B"));

        // Os códigos trocam de dono: o tipo que era A passa a responder por B e vice-versa.
        await RegravarAsync(
            processoId, etapaId, [Banca(TipoBanca, "BANCA_B"), Banca(OutroTipoBanca, "BANCA_A")],
            "trocar os códigos de lugar é configuração legítima, e não pode depender de o banco aceitar duas linhas no mesmo slot do índice único durante a transação");

        EtapaProcesso relida = await RelerAsync(etapaId);
        relida.Bancas.Select(b => (b.TipoBancaOrigemId, b.Codigo))
            .Should().BeEquivalentTo([(TipoBanca, "BANCA_B"), (OutroTipoBanca, "BANCA_A")]);
    }

    private static BancaDaEtapa Banca(Guid tipo, string codigo) =>
        BancaDaEtapa.Criar(tipo, codigo, "Banca de teste");

    private static EtapaProcesso NovaEtapaDeTitulos() =>
        EtapaProcesso.Criar(
            "Prova de títulos",
            CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_TITULOS", "Prova de títulos", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            peso: 1m,
            ordem: 1).Value!;

    private static ProcessoSeletivo NovoProcesso(string nome) =>
        ProcessoSeletivo.Criar(
            $"{nome} {Guid.CreateVersion7()}",
            TipoProcesso.SiSU,
            OrigemCandidatos.InscricaoPropria,
            Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private async Task<(Guid ProcessoId, Guid EtapaId)> GravarEtapaComAsync(string nome, params BancaDaEtapa[] bancas)
    {
        await using UniPlusDbContext escrita = fixture.CreateDbContext();
        ProcessoSeletivo processo = NovoProcesso(nome);
        EtapaProcesso etapa = NovaEtapaDeTitulos();
        etapa.DefinirBancas(bancas).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
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
        tracked.Etapas.Single(e => e.Id == etapaId).DefinirBancas(bancas).IsSuccess.Should().BeTrue();

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
