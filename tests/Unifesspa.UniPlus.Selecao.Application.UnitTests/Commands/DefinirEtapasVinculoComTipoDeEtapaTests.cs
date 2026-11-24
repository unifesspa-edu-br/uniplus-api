namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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

        Result result = await Executar(command, RepositorioCom(processo), unitOfWork, ReaderSemTipoAtivo());

        await RecusouSemGravarAsync(result, unitOfWork, "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        etapaOriginal.Should().BeEquivalentTo(new { Nome = "Prova Objetiva", Peso = 1m }, "a etapa anterior não pode ter sido mutada");
    }

    private static DefinirEtapasCommand RevisaAProvaEAdicionaEtapaDeTipoInativo(ProcessoSeletivo processo, EtapaProcesso etapaOriginal) => new(
        processo.Id,
        [
            // Vínculo e caráter inalterados: só o peso muda, e a etapa é a instância tracked.
            new EtapaProcessoInput("Prova Objetiva (revisada)", CaraterEtapa.Classificatoria.Codigo(), TipoProvaObjetivaOrigemId, 9m, null, 1, etapaOriginal.Id, Produtos: [], Bancas: [], Recursos: []),
            // Etapa nova com tipo inativo — é ela que derruba o PUT inteiro.
            new EtapaProcessoInput("Entrevista", CaraterEtapa.Classificatoria.Codigo(), Guid.CreateVersion7(), 1m, null, 2, Produtos: [], Bancas: [], Recursos: []),
        ]);

    [Fact(DisplayName = "Handle com vínculo inalterado reaproveita o snapshot já congelado, mesmo com o tipo desde então desativado")]
    public async Task Handle_ComVinculoInalterado_ReaproveitaSnapshotMesmoComTipoDesativado()
    {
        EtapaProcesso etapaOriginal = EtapaDeProvaObjetiva("Prova Objetiva");
        ProcessoSeletivo processo = ProcessoComAEtapa(etapaOriginal);
        // O cadastro devolve null para TODO id: o tipo foi desativado depois que a etapa já existia.
        // Se o handler reavaliasse o vínculo inalterado, a gravação inteira seria recusada.
        ITipoEtapaReader tipoEtapaReader = ReaderSemTipoAtivo();

        // Edita só o peso — TipoEtapaOrigemId permanece o mesmo do snapshot já congelado.
        Result result = await ExecutarSobre(
            processo, tipoEtapaReader, EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 9m, null, 1, etapaOriginal.Id));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().Peso.Should().Be(9m);
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("PROVA_OBJETIVA");
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    [Fact(DisplayName = "Handle com vínculo inalterado não atualiza o Nome congelado, mesmo com o tipo renomeado no cadastro")]
    public async Task Handle_ComVinculoInalterado_NaoAtualizaNomeAoRenomearTipoNoCadastro()
    {
        EtapaProcesso etapaOriginal = EtapaDeProvaObjetiva("Prova Objetiva");
        ProcessoSeletivo processo = ProcessoComAEtapa(etapaOriginal);
        // Se o handler relesse o cadastro para um vínculo inalterado, encontraria este nome
        // renomeado — a asserção abaixo prova que ele nunca chega a consultar.
        ITipoEtapaReader tipoEtapaReader = ReaderDoTipo(TipoNoCadastro(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva (renomeada)"));

        Result result = await ExecutarSobre(
            processo, tipoEtapaReader, EtapaInput("Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, etapaOriginal.Id));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.Nome.Should().Be("Prova Objetiva",
            "o snapshot só muda quando o VÍNCULO (TipoEtapaOrigemId) muda, não quando o cadastro de origem muda");
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    [Fact(DisplayName = "Handle com vínculo alterado resolve o novo tipo contra o cadastro corrente")]
    public async Task Handle_ComVinculoAlterado_ResolveContraCadastroAtual()
    {
        EtapaProcesso etapaOriginal = EtapaDeProvaObjetiva("Prova");
        ProcessoSeletivo processo = ProcessoComAEtapa(etapaOriginal);
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();

        // Mesma etapa (mesmo id), agora vinculada à Redação em vez de Prova Objetiva.
        Result result = await ExecutarSobre(
            processo, tipoEtapaReader, EtapaInput("Redação", CaraterEtapa.Classificatoria, TipoRedacaoOrigemId, 1m, null, 1, etapaOriginal.Id));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("REDACAO");
        await tipoEtapaReader.Received(1).ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com duas etapas novas do mesmo tipo cria instâncias distintas de TipoEtapaSnapshot")]
    public async Task Handle_ComDuasEtapasMesmoTipo_CriaInstanciasDistintas()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        ITipoEtapaReader tipoEtapaReader = ReaderPadrao();

        Result result = await ExecutarSobre(
            processo, tipoEtapaReader,
            EtapaInput("Prova — Manhã", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1),
            EtapaInput("Prova — Tarde", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 2));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        ConferirQueCadaEtapaTemASuaInstanciaDoTipo(processo);
        await tipoEtapaReader.Received(1).ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com tipo de etapa inexistente ou inativo recusa e não persiste etapa")]
    public async Task Handle_TipoEtapaInexistenteOuInativo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DefinirEtapasCommand command = new(
            processo.Id,
            [new EtapaProcessoInput("Prova Objetiva", CaraterEtapa.Classificatoria.Codigo(), Guid.CreateVersion7(), 3m, null, 1, Produtos: [], Bancas: [], Recursos: [])]);

        Result result = await Executar(command, RepositorioCom(processo), unitOfWork, ReaderSemTipoAtivo());

        await RecusouSemGravarAsync(result, unitOfWork, "ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        processo.Etapas.Should().BeEmpty("nenhuma etapa pode ser persistida sem snapshot de tipo");
    }

    private static void ConferirQueCadaEtapaTemASuaInstanciaDoTipo(ProcessoSeletivo processo)
    {
        EtapaProcesso[] etapas = [.. processo.Etapas.OrderBy(e => e.Ordem)];
        etapas.Should().HaveCount(2);
        etapas[0].TipoEtapa.Should().NotBeSameAs(etapas[1].TipoEtapa,
            "TipoEtapa é owned type — a mesma instância em dois donos quebraria o change tracking do EF Core");
        etapas[0].TipoEtapa.Should().Be(etapas[1].TipoEtapa, "mesmo conteúdo, apesar de instâncias diferentes (record com igualdade por valor)");
    }
}
