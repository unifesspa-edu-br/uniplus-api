namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Persistência EF da cascata de remanejamento (cascata de remanejamento de cotas, UNI-REQ-0056). Os
/// testes não precisam publicar: <c>DefinirCascataRemanejamento</c> não valida a cobertura por oferta
/// (isso é <c>PendenciaDaCascata</c>, exercitada em <c>ProcessoSeletivoCascataTests</c>,
/// Domain.UnitTests) — só a forma, na factory. O que está em jogo aqui é o que só o Postgres real
/// prova: o Include de <c>ProcessoSeletivoRepository</c> e o cascade da tabela de destinos.
/// </summary>
[Collection(PublicacaoSnapshotPersistenciaColecao.Nome)]
public sealed class CascataDeRemanejamentoPersistenciaTests
{
    private readonly CenarioDeCascataPersistida _cenario;

    public CascataDeRemanejamentoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeCascataPersistida(fixture);
    }

    [Fact(DisplayName = "A cascata e os destinos sobrevivem à gravação e à leitura")]
    public async Task Cascata_SobreviveASaveChangesEReload()
    {
        ProcessoSeletivo processo = await PersistirComCascataAsync(nameof(Cascata_SobreviveASaveChangesEReload), "1");
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        ProcessoSeletivo relido = await LerAsync(contexto, processo.Id);
        relido.Cascata.Should().NotBeNull();
        relido.Cascata!.ProcessoSeletivoId.Should().Be(processo.Id);
        relido.Cascata.FallbackCodigo.Should().Be("AC");
        relido.Cascata.Destinos.Should().HaveCount(3);
        relido.Cascata.Destinos.Should().OnlyContain(d => d.ConfiguracaoCascataRemanejamentoId == relido.Cascata.Id);
    }

    [Fact(DisplayName = "A substituição da cascata remove os destinos anteriores")]
    public async Task Cascata_SubstituidaDuasVezes_SoASegundaVersaoSobrevive()
    {
        ProcessoSeletivo processo = await PersistirComCascataAsync(nameof(Cascata_SubstituidaDuasVezes_SoASegundaVersaoSobrevive), "2");
        Guid primeira = processo.Cascata!.Id;
        ConfiguracaoCascataRemanejamento segunda = ConfiguracaoCascataRemanejamento.Criar(
            CenarioDeCascataPersistida.Regra(CriterioDeRemanejamento.OrdemDaLei12711.Codigo(), "3"),
            "AC", [DestinoRemanejamento.Criar("LI_PPI", 1, "AC").Value!]).Value!;
        await SubstituirAsync(processo.Id, segunda);
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        ProcessoSeletivo relido = await LerAsync(contexto, processo.Id);
        relido.Cascata.Should().NotBeNull();
        relido.Cascata!.Id.Should().Be(segunda.Id);
        relido.Cascata.Destinos.Should().ContainSingle().Which.ModalidadeOrigemCodigo.Should().Be("LI_PPI");
        await ConferirQuantidadeDeDestinosAsync(contexto, primeira, 0);
        await ConferirQuantidadeDeDestinosAsync(contexto, segunda.Id, 1);
    }

    [Fact(DisplayName = "A remoção da cascata remove também seus destinos")]
    public async Task Cascata_Removida_SomeComATabelaDeDestinos()
    {
        ProcessoSeletivo processo = await PersistirComCascataAsync(nameof(Cascata_Removida_SomeComATabelaDeDestinos), "4");
        Guid cascataId = processo.Cascata!.Id;
        await SubstituirAsync(processo.Id, null);
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        ProcessoSeletivo relido = await LerAsync(contexto, processo.Id);
        relido.Cascata.Should().BeNull();
        await ConferirQuantidadeDeDestinosAsync(contexto, cascataId, 0);
        (await contexto.Set<ConfiguracaoCascataRemanejamento>().AnyAsync(c => c.Id == cascataId, CancellationToken.None)).Should().BeFalse();
    }

    private async Task<ProcessoSeletivo> PersistirComCascataAsync(string nome, string hash)
    {
        ProcessoSeletivo processo = CenarioDeRetificacaoPersistida.NovoProcessoConforme(nome);
        processo.DefinirCascataRemanejamento(CenarioDeCascataPersistida.CascataDeDuasOrigens(hash)).IsSuccess.Should().BeTrue();
        await _cenario.PersistirAsync(processo);
        return processo;
    }

    private async Task SubstituirAsync(Guid processoId, ConfiguracaoCascataRemanejamento? cascata)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        ProcessoSeletivo processo = await LerAsync(contexto, processoId);
        processo.Cascata.Should().NotBeNull();
        processo.DefinirCascataRemanejamento(cascata).IsSuccess.Should().BeTrue();
        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<ProcessoSeletivo> LerAsync(UniPlusDbContext contexto, Guid processoId) =>
        (await ProcessoSeletivoRepositoryDeTeste.Criar(contexto).ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

    private static async Task ConferirQuantidadeDeDestinosAsync(UniPlusDbContext contexto, Guid cascataId, int quantidade) =>
        (await contexto.Set<DestinoRemanejamento>().CountAsync(d => d.ConfiguracaoCascataRemanejamentoId == cascataId, CancellationToken.None))
            .Should().Be(quantidade);
}
