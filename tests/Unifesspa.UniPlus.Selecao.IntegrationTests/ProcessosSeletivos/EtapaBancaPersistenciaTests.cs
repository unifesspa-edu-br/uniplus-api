namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

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
        Guid processoId;
        Guid etapaId;

        await using (SelecaoDbContext escrita = fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = ProcessoSeletivo.Criar(
                $"Certame de bancas {Guid.CreateVersion7()}",
                TipoProcesso.SiSU,
                OrigemCandidatos.InscricaoPropria,
                Guid.CreateVersion7(),
                UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
                LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

            EtapaProcesso etapa = EtapaProcesso.Criar(
                "Prova de títulos",
                CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_TITULOS", "Prova de títulos", admitePontuacao: true, admiteEliminacao: true).Value!,
                peso: 1m,
                ordem: 1).Value!;
            etapa.DefinirBancas([BancaDaEtapa.Criar(TipoBanca, "BANCA_TITULOS")]).IsSuccess.Should().BeTrue();

            processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            escrita.ProcessosSeletivos.Add(processo);
            await escrita.SaveChangesAsync();

            processoId = processo.Id;
            etapaId = etapa.Id;
        }

        await using (SelecaoDbContext regravacao = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await regravacao.ProcessosSeletivos
                .Include(p => p.Etapas).ThenInclude(e => e.Bancas)
                .FirstAsync(p => p.Id == processoId);

            EtapaProcesso etapa = tracked.Etapas.Single(e => e.Id == etapaId);

            // Instância NOVA com o MESMO código — é o que o handler monta a cada PUT, porque o
            // corpo declara o tipo de banca, não a linha que já existe.
            etapa.DefinirBancas([BancaDaEtapa.Criar(TipoBanca, "BANCA_TITULOS")]).IsSuccess.Should().BeTrue();

            Func<Task> gravar = async () => await regravacao.SaveChangesAsync();

            await gravar.Should().NotThrowAsync(
                "regravar a etapa sem mudar a banca é o caso comum do editor, e não pode falhar " +
                "por causa de como a coleção é substituída");
        }

        await using SelecaoDbContext conferencia = fixture.CreateDbContext();
        EtapaProcesso relida = await conferencia.Set<EtapaProcesso>()
            .Include(e => e.Bancas)
            .FirstAsync(e => e.Id == etapaId);

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
        Guid processoId;
        Guid etapaId;

        await using (SelecaoDbContext escrita = fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = ProcessoSeletivo.Criar(
                $"Certame de troca {Guid.CreateVersion7()}",
                TipoProcesso.SiSU,
                OrigemCandidatos.InscricaoPropria,
                Guid.CreateVersion7(),
                UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
                LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

            EtapaProcesso etapa = EtapaProcesso.Criar(
                "Prova de títulos",
                CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_TITULOS", "Prova de títulos", admitePontuacao: true, admiteEliminacao: true).Value!,
                peso: 1m,
                ordem: 1).Value!;
            etapa.DefinirBancas([
                BancaDaEtapa.Criar(TipoBanca, "BANCA_A"),
                BancaDaEtapa.Criar(OutroTipoBanca, "BANCA_B"),
            ]).IsSuccess.Should().BeTrue();

            processo.DefinirEtapas([etapa], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            escrita.ProcessosSeletivos.Add(processo);
            await escrita.SaveChangesAsync();

            processoId = processo.Id;
            etapaId = etapa.Id;
        }

        await using (SelecaoDbContext regravacao = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await regravacao.ProcessosSeletivos
                .Include(p => p.Etapas).ThenInclude(e => e.Bancas)
                .FirstAsync(p => p.Id == processoId);

            EtapaProcesso etapa = tracked.Etapas.Single(e => e.Id == etapaId);

            // Os códigos trocam de dono: o tipo que era A passa a responder por B e vice-versa.
            etapa.DefinirBancas([
                BancaDaEtapa.Criar(TipoBanca, "BANCA_B"),
                BancaDaEtapa.Criar(OutroTipoBanca, "BANCA_A"),
            ]).IsSuccess.Should().BeTrue();

            Func<Task> gravar = async () => await regravacao.SaveChangesAsync();

            await gravar.Should().NotThrowAsync(
                "trocar os códigos de lugar é configuração legítima, e não pode depender de o " +
                "banco aceitar duas linhas no mesmo slot do índice único durante a transação");
        }

        await using SelecaoDbContext conferencia = fixture.CreateDbContext();
        EtapaProcesso relida = await conferencia.Set<EtapaProcesso>()
            .Include(e => e.Bancas)
            .FirstAsync(e => e.Id == etapaId);

        relida.Bancas.Select(b => (b.TipoBancaOrigemId, b.Codigo))
            .Should().BeEquivalentTo([(TipoBanca, "BANCA_B"), (OutroTipoBanca, "BANCA_A")]);
    }
}
