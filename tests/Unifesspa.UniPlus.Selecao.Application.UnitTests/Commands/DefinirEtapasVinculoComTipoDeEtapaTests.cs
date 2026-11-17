namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasVinculoComTipoDeEtapaTests
{
    /// <summary>
    /// A primeira etapa do payload (vínculo inalterado) não pode ter sido mutada quando a SEGUNDA é recusada por tipo
    /// inativo: o SaveChangesAsync automático do Wolverine persistiria a mutação parcial mesmo com o PUT inteiro
    /// recusado. A resolução dos tipos acontece na passada que confere o caráter, antes de a reconciliação tocar
    /// qualquer instância rastreada, então não há mutação parcial a desfazer; a recusa ainda descarta o rastreamento,
    /// como toda recusa da substituição.
    /// </summary>
    [Fact(DisplayName = "Handle com tipo inativo numa etapa posterior recusa sem mutar a anterior")]
    public async Task Handle_ComTipoInativoNaEtapaPosterior_RecusaSemMutarAAnterior()
    {
        EtapaProcesso etapaOriginal = EtapaDeProvaObjetiva("Prova Objetiva");
        ProcessoSeletivo processo = ProcessoComAEtapa(etapaOriginal);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DefinirEtapasCommand command = RevisaAProvaEAdicionaEtapaDeTipoInativo(processo, etapaOriginal);

        Result<MutacaoAceita> result = await Executar(command, RepositorioCom(processo), unitOfWork, ReaderSemTipoAtivo());

        await RecusouSemGravarAsync(result, unitOfWork, "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        etapaOriginal.Should().BeEquivalentTo(new { Nome = "Prova Objetiva", Peso = 1m }, "a etapa anterior não pode ter sido mutada");
    }

    private static DefinirEtapasCommand RevisaAProvaEAdicionaEtapaDeTipoInativo(ProcessoSeletivo processo, EtapaProcesso etapaOriginal) => new(
        processo.Id,
        [
            // Vínculo e caráter inalterados: só o peso muda, e a etapa é a instância tracked.
            new EtapaProcessoInput("Prova Objetiva (revisada)", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 9m, null, 1, etapaOriginal.Id, Produtos: [], Bancas: [], Recursos: []),
            // Etapa nova com tipo inativo — é ela que derruba o PUT inteiro.
            new EtapaProcessoInput("Entrevista", CaraterEtapa.Classificatoria, Guid.CreateVersion7(), 1m, null, 2, Produtos: [], Bancas: [], Recursos: []),
        ],
        PrecondicaoIfMatch.Ausente);

    /// <summary>
    /// Desativar um tipo já vinculado a uma etapa
    /// não pode bloquear PUTs subsequentes da coleção inteira — só vínculos NOVOS ou que MUDAM
    /// de tipo precisam do cadastro ativo. O snapshot já congelado é reaproveitado sem nova
    /// leitura do cadastro.
    /// </summary>
    [Fact(DisplayName = "Handle com vínculo inalterado reaproveita o snapshot já congelado, mesmo com o tipo desde então desativado")]
    public async Task Handle_ComVinculoInalterado_ReaproveitaSnapshotMesmoComTipoDesativado()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        // Reader configurado para devolver null para TODO Id — simula o tipo desativado depois
        // que a etapa já existia. Se o handler tentasse reavaliar o vínculo inalterado, a
        // publicação inteira recusaria com TipoEtapaNaoEncontradoOuInativo.
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        ITipoBancaReader tipoBancaReader = Substitute.For<ITipoBancaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoEtapaReader.ObterAtivoPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TipoEtapaView?)null);
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        // Edita só o Peso — TipoEtapaOrigemId permanece o mesmo do snapshot já congelado.
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 9m, null, 1, etapaOriginal.Id, Produtos: [], Bancas: [], Recursos: [])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().Peso.Should().Be(9m);
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("PROVA_OBJETIVA");
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    /// <summary>
    /// O snapshot-copy (ADR-0061) só muda quando o
    /// VÍNCULO muda — renomear o tipo no cadastro, ainda ativo, não pode reescrever
    /// silenciosamente o snapshot de uma etapa que não trocou de TipoEtapaOrigemId.
    /// </summary>
    [Fact(DisplayName = "Handle com vínculo inalterado não atualiza o Nome congelado, mesmo com o tipo renomeado no cadastro")]
    public async Task Handle_ComVinculoInalterado_NaoAtualizaNomeAoRenomearTipoNoCadastro()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        // Se o handler fosse reler o cadastro para um vínculo inalterado, encontraria este Nome
        // renomeado — a asserção abaixo prova que ele nunca chega a consultar.
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        IRegraCatalogoReader regraCatalogoReader = Substitute.For<IRegraCatalogoReader>();
        ITipoAtoPublicadoReader tipoAtoPublicadoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoEtapaReader.ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>())
            .Returns(new TipoEtapaView(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva (renomeada)", null, true, true, false, false, []));
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapaOriginal.Id, Produtos: [], Bancas: [], Recursos: [])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.Nome.Should().Be("Prova Objetiva",
            "o snapshot só muda quando o VÍNCULO (TipoEtapaOrigemId) muda, não quando o cadastro de origem muda");
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    [Fact(DisplayName = "Handle com vínculo alterado resolve o novo tipo contra o cadastro corrente")]
    public async Task Handle_ComVinculoAlterado_ResolveContraCadastroAtual()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        EtapaProcesso etapaOriginal = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([etapaOriginal], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        // Mesma etapa (mesmo Id), agora vinculada à Redação em vez de Prova Objetiva.
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 1, etapaOriginal.Id, Produtos: [], Bancas: [], Recursos: [])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("REDACAO");
        await tipoEtapaReader.Received(1).ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// TipoEtapa é owned type (OwnsOne) de
    /// EtapaProcesso — EF Core não aceita a MESMA instância de TipoEtapaSnapshot pertencendo a
    /// duas etapas ao mesmo tempo. Duas etapas novas com o mesmo tipo é caso legítimo (duas
    /// fases classificatórias do mesmo propósito institucional); o cache de resolução tem de
    /// reaproveitar só o dado do catálogo, nunca a instância do snapshot já construído.
    /// </summary>
    [Fact(DisplayName = "Handle com duas etapas novas do mesmo tipo cria instâncias distintas de TipoEtapaSnapshot")]
    public async Task Handle_ComDuasEtapasMesmoTipo_CriaInstanciasDistintas()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        DefinirEtapasCommand command = new(
            processo.Id,
            [
                new EtapaProcessoInput("Prova — Manhã", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, Produtos: [], Bancas: [], Recursos: []),
                new EtapaProcessoInput("Prova — Tarde", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 2, Produtos: [], Bancas: [], Recursos: []),
            ], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, repository, unitOfWork, tipoEtapaReader);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        EtapaProcesso[] etapas = [.. processo.Etapas.OrderBy(e => e.Ordem)];
        etapas.Should().HaveCount(2);
        etapas[0].TipoEtapa.Should().NotBeSameAs(etapas[1].TipoEtapa,
            "TipoEtapa é owned type — a mesma instância em dois donos quebraria o change tracking do EF Core");
        etapas[0].TipoEtapa.Should().Be(etapas[1].TipoEtapa, "mesmo conteúdo, apesar de instâncias diferentes (record com igualdade por valor)");
        await tipoEtapaReader.Received(1).ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com tipo de etapa inexistente ou inativo recusa e não persiste etapa")]
    public async Task Handle_TipoEtapaInexistenteOuInativo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria, Guid.CreateVersion7(), 3m, null, 1, Produtos: [], Bancas: [], Recursos: [])], PrecondicaoIfMatch.Ausente);

        Result<MutacaoAceita> result = await Executar(command, RepositorioCom(processo), unitOfWork, ReaderSemTipoAtivo());

        await RecusouSemGravarAsync(result, unitOfWork, "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        processo.Etapas.Should().BeEmpty("nenhuma etapa pode ser persistida sem snapshot de tipo");
    }
}
