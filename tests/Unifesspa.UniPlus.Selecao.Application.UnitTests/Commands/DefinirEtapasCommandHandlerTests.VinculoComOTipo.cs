namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ArranjoDasEtapas;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDasEtapas;

public sealed partial class DefinirEtapasCommandHandlerTests
{
    /// <summary>
    /// A primeira etapa do payload (vínculo inalterado) não pode ter sido mutada quando a SEGUNDA
    /// é recusada por tipo inativo: o SaveChangesAsync automático do Wolverine persistiria a
    /// mutação parcial mesmo com o PUT inteiro recusado. A resolução dos tipos acontece na passada
    /// que confere o caráter, antes de a reconciliação tocar qualquer instância rastreada — então
    /// não há mutação parcial a desfazer, e não há rastreamento a descartar. Na primeira etapa só
    /// o peso muda, e ela é a instância tracked; a etapa nova tem tipo inativo e é ela que derruba
    /// o PUT inteiro.
    /// </summary>
    [Fact(DisplayName = "Handle com tipo inativo numa etapa posterior recusa sem mutar a anterior")]
    public async Task Handle_ComTipoInativoNaEtapaPosterior_RecusaSemMutarAAnterior()
    {
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal);
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: CadastroSemTipoAtivo());
        EtapaProcessoInput pesoRevisado = ProvaObjetiva("Prova Objetiva (revisada)", id: etapaOriginal.Id) with { Peso = 9m };
        EtapaProcessoInput comTipoInativo = EtapaClassificatoria("Entrevista", Guid.CreateVersion7(), 2);

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, pesoRevisado, comTipoInativo));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        etapaOriginal.Nome.Should().Be("Prova Objetiva", "a etapa anterior não pode ter sido mutada");
        etapaOriginal.Peso.Should().Be(1m);
        cenario.UnitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    /// <summary>
    /// Desativar um tipo já vinculado a uma etapa não pode bloquear PUTs subsequentes da coleção
    /// inteira — só vínculos NOVOS ou que MUDAM de tipo precisam do cadastro ativo. O snapshot já
    /// congelado é reaproveitado sem nova leitura do cadastro, em que todo Id resolve para nada:
    /// se o handler reavaliasse o vínculo inalterado, a publicação inteira recusaria com
    /// TipoEtapaNaoEncontradoOuInativo. Aqui só o Peso muda.
    /// </summary>
    [Fact(DisplayName = "Handle com vínculo inalterado reaproveita o snapshot já congelado, mesmo com o tipo desde então desativado")]
    public async Task Handle_ComVinculoInalterado_ReaproveitaSnapshotMesmoComTipoDesativado()
    {
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal);
        ITipoEtapaReader cadastro = CadastroSemTipoAtivo();
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: cadastro);
        DefinirEtapasCommand command = Definindo(processo, ProvaObjetiva(id: etapaOriginal.Id) with { Peso = 9m });

        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().Peso.Should().Be(9m);
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("PROVA_OBJETIVA");
        await cadastro.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    /// <summary>
    /// O snapshot-copy (ADR-0061) só muda quando o VÍNCULO muda — renomear o tipo no cadastro,
    /// ainda ativo, não pode reescrever silenciosamente o snapshot de uma etapa que não trocou de
    /// TipoEtapaOrigemId. Se o handler relesse o cadastro para um vínculo inalterado, encontraria
    /// o nome renomeado — a asserção prova que ele nunca chega a consultar.
    /// </summary>
    [Fact(DisplayName = "Handle com vínculo inalterado não atualiza o Nome congelado, mesmo com o tipo renomeado no cadastro")]
    public async Task Handle_ComVinculoInalterado_NaoAtualizaNomeAoRenomearTipoNoCadastro()
    {
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal);
        ITipoEtapaReader cadastro = CadastroDeTipos(TipoDoCadastro(TipoProvaObjetivaOrigemId, "PROVA_OBJETIVA", "Prova Objetiva (renomeada)"));
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: cadastro);
        DefinirEtapasCommand command = Definindo(processo, ProvaObjetiva(id: etapaOriginal.Id));

        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.Nome.Should().Be("Prova Objetiva",
            "o snapshot só muda quando o VÍNCULO (TipoEtapaOrigemId) muda, não quando o cadastro de origem muda");
        await cadastro.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
    }

    /// <summary>A mesma etapa (mesmo Id), agora vinculada à Redação em vez de Prova Objetiva.</summary>
    [Fact(DisplayName = "Handle com vínculo alterado resolve o novo tipo contra o cadastro corrente")]
    public async Task Handle_ComVinculoAlterado_ResolveContraCadastroAtual()
    {
        ProcessoSeletivo processo = ProcessoComEtapa(out EtapaProcesso etapaOriginal);
        CenarioDasEtapas cenario = Para(processo);
        EtapaProcessoInput daRedacao = EtapaClassificatoria("Redação", TipoRedacaoOrigemId, id: etapaOriginal.Id);

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, daRedacao));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().TipoEtapa.Codigo.Should().Be("REDACAO");
        await cenario.TiposDeEtapa.Received(1).ObterAtivoPorIdAsync(TipoRedacaoOrigemId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// TipoEtapa é owned type (OwnsOne) de EtapaProcesso — EF Core não aceita a MESMA instância
    /// de TipoEtapaSnapshot pertencendo a duas etapas ao mesmo tempo. Duas etapas novas com o
    /// mesmo tipo é caso legítimo (duas fases classificatórias do mesmo propósito institucional);
    /// o cache de resolução tem de reaproveitar só o dado do catálogo, nunca a instância do
    /// snapshot já construído.
    /// </summary>
    [Fact(DisplayName = "Handle com duas etapas novas do mesmo tipo cria instâncias distintas de TipoEtapaSnapshot")]
    public async Task Handle_ComDuasEtapasMesmoTipo_CriaInstanciasDistintas()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo);

        Result<MutacaoAceita> result = await cenario.DefinirAsync(
            Definindo(processo, ProvaObjetiva("Prova — Manhã"), ProvaObjetiva("Prova — Tarde", 2)));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        EtapaProcesso[] etapas = [.. processo.Etapas.OrderBy(e => e.Ordem)];
        etapas.Should().HaveCount(2);
        etapas[0].TipoEtapa.Should().NotBeSameAs(etapas[1].TipoEtapa,
            "TipoEtapa é owned type — a mesma instância em dois donos quebraria o change tracking do EF Core");
        etapas[0].TipoEtapa.Should().Be(etapas[1].TipoEtapa, "mesmo conteúdo, apesar de instâncias diferentes (record com igualdade por valor)");
        await cenario.TiposDeEtapa.Received(1).ObterAtivoPorIdAsync(TipoProvaObjetivaOrigemId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Nada a descartar: os tipos do payload são resolvidos na passada que confere o caráter,
    /// antes de a reconciliação tocar qualquer instância rastreada.
    /// </summary>
    [Fact(DisplayName = "issue #1071 — CA-03: tipo de etapa inexistente ou inativo é recusado")]
    public async Task Handle_TipoEtapaInexistenteOuInativo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: CadastroSemTipoAtivo());
        EtapaProcessoInput comTipoInexistente = EtapaClassificatoria("Prova Objetiva", Guid.CreateVersion7()) with { Peso = 3m };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, comTipoInexistente));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.TipoEtapaNaoEncontradoOuInativo");
        processo.Etapas.Should().BeEmpty("nenhuma etapa pode ser persistida sem snapshot de tipo (CA-04)");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        cenario.UnitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "ADR-0125: violações de forma acumulam entre etapas, com o índice prefixado ao field, ANTES de consultar o tipoEtapaReader")]
    public async Task Handle_DuasEtapasComViolacaoDeForma_AcumulaComIndicePrefixadoESemConsultarReader()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo);
        EtapaProcessoInput semNome = ProvaObjetiva(" ") with { Peso = 3m };
        EtapaProcessoInput semPeso = EtapaClassificatoria("Redação", TipoRedacaoOrigemId, 2) with { Peso = 0m };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, semNome, semPeso));

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["etapas[0].nome", "etapas[1].peso"]);
        await cenario.TiposDeEtapa.DidNotReceiveWithAnyArgs().ObterAtivoPorIdAsync(default, default);
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
