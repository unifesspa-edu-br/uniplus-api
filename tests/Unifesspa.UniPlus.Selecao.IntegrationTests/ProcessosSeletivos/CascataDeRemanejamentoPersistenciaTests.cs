namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
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

    [Fact(DisplayName = "Cascata_SobreviveASaveChangesEReload — a cascata e os destinos persistem via Include e sobrevivem a um reload")]
    public async Task Cascata_SobreviveASaveChangesEReload()
    {
        ProcessoSeletivo processo = CenarioDeRetificacaoPersistida.NovoProcessoConforme(nameof(Cascata_SobreviveASaveChangesEReload));
        processo.DefinirCascataRemanejamento(CenarioDeCascataPersistida.CascataDeDuasOrigens("1"), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        await _cenario.PersistirAsync(processo);

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;

        relido.Cascata.Should().NotBeNull(
            "a cascata sobrevive ao SaveChanges e ao reload — sem o Include em ComConfiguracao ela nasceria " +
            "null em todo carregamento novo do agregado");
        relido.Cascata!.ProcessoSeletivoId.Should().Be(processo.Id);
        relido.Cascata.FallbackCodigo.Should().Be("AC");
        relido.Cascata.Destinos.Should().HaveCount(3);
        relido.Cascata.Destinos.Should().OnlyContain(d => d.ConfiguracaoCascataRemanejamentoId == relido.Cascata.Id,
            "as FKs dos destinos apontam para a cascata recém-persistida, não para uma cascata de outro processo");
    }

    [Fact(DisplayName = "Cascata_SubstituidaDuasVezes_SoASegundaVersaoSobrevive — DefinirCascataRemanejamento duas vezes não acumula destinos órfãos")]
    public async Task Cascata_SubstituidaDuasVezes_SoASegundaVersaoSobrevive()
    {
        ProcessoSeletivo processo = CenarioDeRetificacaoPersistida.NovoProcessoConforme(nameof(Cascata_SubstituidaDuasVezes_SoASegundaVersaoSobrevive));
        ConfiguracaoCascataRemanejamento primeiraVersao = CenarioDeCascataPersistida.CascataDeDuasOrigens("2");
        processo.DefinirCascataRemanejamento(primeiraVersao, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        Guid idPrimeiraVersao = primeiraVersao.Id;

        Guid processoId = processo.Id;
        await _cenario.PersistirAsync(processo);

        // Segunda sessão editorial: substitui a cascata inteira por OUTRA — conteúdo
        // diferente, origens diferentes, contagem de destinos diferente.
        ConfiguracaoCascataRemanejamento segundaVersao = ConfiguracaoCascataRemanejamento.Criar(
            CenarioDeCascataPersistida.Regra(RegraRemanejamentoCodigo.Cascata, "3"),
            "AC",
            [DestinoRemanejamento.Criar("LI_PPI", 1, "AC").Value!]).Value!;
        Guid idSegundaVersao = segundaVersao.Id;
        await using (UniPlusDbContext sessao = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
            ProcessoSeletivo tracked = (await repository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
            tracked.DefinirCascataRemanejamento(segundaVersao, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        relido.Cascata.Should().NotBeNull();
        relido.Cascata!.Id.Should().Be(idSegundaVersao, "a cascata VIVA é a SEGUNDA versão — a primeira não sobrevive");
        relido.Cascata.Destinos.Should().ContainSingle(
            "só a SEGUNDA versão da cascata sobrevive — a primeira (3 destinos, origens LB_PPI/LB_Q) " +
            "não pode deixar resíduo")
            .Which.ModalidadeOrigemCodigo.Should().Be("LI_PPI");

        // A prova de "sem órfãos": zero destinos ainda apontando para a PRIMEIRA cascata (que
        // não existe mais) — filtrado pelo id conhecido, não uma contagem cega da tabela
        // inteira, que este teste COMPARTILHA com as demais classes da coleção
        // (mesmo Postgres, mesma tabela, entre métodos de teste).
        int destinosOrfaosDaPrimeiraVersao = await readContext.Set<DestinoRemanejamento>()
            .CountAsync(d => d.ConfiguracaoCascataRemanejamentoId == idPrimeiraVersao, CancellationToken.None);
        destinosOrfaosDaPrimeiraVersao.Should().Be(0,
            "substituir a cascata inteira via DefinirCascataRemanejamento não pode deixar os 3 destinos da " +
            "primeira versão órfãos em destinos_remanejamento");

        int destinosDaSegundaVersao = await readContext.Set<DestinoRemanejamento>()
            .CountAsync(d => d.ConfiguracaoCascataRemanejamentoId == idSegundaVersao, CancellationToken.None);
        destinosDaSegundaVersao.Should().Be(1, "só o destino da segunda versão sobrevive");
    }

    [Fact(DisplayName = "Cascata_Removida_SomeComATabelaDeDestinos — DefinirCascataRemanejamento(null) remove a cascata e os destinos, sem lixo órfão")]
    public async Task Cascata_Removida_SomeComATabelaDeDestinos()
    {
        ProcessoSeletivo processo = CenarioDeRetificacaoPersistida.NovoProcessoConforme(nameof(Cascata_Removida_SomeComATabelaDeDestinos));
        ConfiguracaoCascataRemanejamento cascata = CenarioDeCascataPersistida.CascataDeDuasOrigens("4");
        processo.DefinirCascataRemanejamento(cascata, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        Guid idCascata = cascata.Id;

        Guid processoId = processo.Id;
        await _cenario.PersistirAsync(processo);

        await using (UniPlusDbContext sessao = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
            ProcessoSeletivo tracked = (await repository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
            tracked.Cascata.Should().NotBeNull("pré-condição: a cascata persistiu na sessão anterior");
            tracked.DefinirCascataRemanejamento(null, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;

        relido.Cascata.Should().BeNull("DefinirCascataRemanejamento(null) remove a cascata — o toggle 'sem cascata' é a própria ausência da entidade");

        // O cascade delete (ON DELETE CASCADE de destinos_remanejamento para
        // configuracoes_de_remanejamento) tem de ter levado os destinos junto — a FK
        // cuida disso, mas é exatamente o que se está provando aqui, não presumindo. Filtrado
        // pelo id conhecido: a coleção compartilha o mesmo Postgres entre TODOS os métodos de
        // teste, então uma contagem cega da tabela inteira pegaria linhas de outros testes.
        int destinosOrfaos = await readContext.Set<DestinoRemanejamento>()
            .CountAsync(d => d.ConfiguracaoCascataRemanejamentoId == idCascata, CancellationToken.None);
        destinosOrfaos.Should().Be(0,
            "remover a cascata tem de levar os destinos junto — nenhuma linha pode sobrar em destinos_remanejamento");

        bool cascataAindaExiste = await readContext.Set<ConfiguracaoCascataRemanejamento>()
            .AnyAsync(c => c.Id == idCascata, CancellationToken.None);
        cascataAindaExiste.Should().BeFalse("a linha da cascata também não pode sobrar em configuracoes_de_remanejamento");
    }
}
