namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static EtapasNoBancoDeTeste;

/// <summary>
/// A recusa da definição das etapas não deixa nada no banco, ainda que o handler tenha mutado instâncias
/// rastreadas antes de recusar e que a transação ambiente salve o contexto depois dele.
/// </summary>
public sealed partial class DefinirEtapasPersistenciaTests
{
    private const string NomeDaRedacaoAlterada = "Redação — nova denominação";

    [Fact(DisplayName = "Na retificação, retirar a etapa que já começou é recusado e as etapas continuam as mesmas no banco")]
    public async Task Definir_RemocaoDeEtapaIniciada_RecusaENadaGrava()
    {
        ProcessoComDuasEtapas processo = await SemearRetificacaoComProvaIniciadaAsync(fixture, $"PS remoção de etapa iniciada {Guid.CreateVersion7()}");
        IReadOnlyList<EtapaGravada> antes = await EtapasGravadasAsync(fixture, processo.ProcessoId);
        string? sessaoAntes = await ETagDaSessaoGravadaAsync(fixture, processo.ProcessoId);

        Result<MutacaoAceita> resultado = await RetirandoAProvaIniciadaAsync(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.EtapaIniciadaNaoSaiDoCronograma");
        resultado.Error.Message.Should().Be("A etapa 'Prova Objetiva' já começou, e ela não sai do cronograma.");
        (await EtapasGravadasAsync(fixture, processo.ProcessoId)).Should().BeEquivalentTo(antes, "nada foi gravado");
        (await ETagDaSessaoGravadaAsync(fixture, processo.ProcessoId)).Should().Be(sessaoAntes, "a sessão editorial não avançou");
    }

    [Fact(DisplayName = "Na retificação, retirar a etapa que não começou, mantendo a iniciada, é aceito")]
    public async Task Definir_RemocaoDeEtapaQueNaoComecou_Aceita()
    {
        ProcessoComDuasEtapas processo = await SemearRetificacaoComProvaIniciadaAsync(fixture, $"PS remoção de etapa que não começou {Guid.CreateVersion7()}");

        Result<MutacaoAceita> resultado = await DefinirApenasAsync(
            processo, PrecondicaoIfMatch.Curinga, Reenvio(processo.ProvaObjetivaId, processo.TipoProvaObjetivaId, "Prova Objetiva", ordem: 1));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        (await EtapasGravadasAsync(fixture, processo.ProcessoId)).Select(e => e.Id).Should().Equal(processo.ProvaObjetivaId);
    }

    [Fact(DisplayName = "A coleção recusada por ordem repetida não grava a alteração que a reconciliação já tinha feito nas etapas existentes")]
    public async Task Definir_OrdemRepetida_RecusaENadaGrava()
    {
        ProcessoComDuasEtapas processo = await SemearRascunhoAsync(fixture, $"PS ordem repetida {Guid.CreateVersion7()}");
        IReadOnlyList<EtapaGravada> antes = await EtapasGravadasAsync(fixture, processo.ProcessoId);

        Result<MutacaoAceita> resultado = await DefinirApenasAsync(
            processo,
            PrecondicaoIfMatch.Ausente,
            Reenvio(processo.ProvaObjetivaId, processo.TipoProvaObjetivaId, "Prova Objetiva", ordem: 1),
            Reenvio(processo.RedacaoId, processo.TipoRedacaoId, NomeDaRedacaoAlterada, ordem: 1));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
        IReadOnlyList<EtapaGravada> depois = await EtapasGravadasAsync(fixture, processo.ProcessoId);
        depois.Should().BeEquivalentTo(antes, "a Redação continua com o nome e a ordem de antes");
        depois.Should().NotContain(e => e.Nome == NomeDaRedacaoAlterada);
    }

    // A Redação é reenviada com outro nome para que a reconciliação já tenha mutado a instância rastreada
    // quando a recusa da Prova Objetiva chega: é essa mutação que o salvamento posterior gravaria.
    private async Task<Result<MutacaoAceita>> RetirandoAProvaIniciadaAsync(ProcessoComDuasEtapas processo) =>
        await DefinirApenasAsync(
            processo, PrecondicaoIfMatch.Curinga, Reenvio(processo.RedacaoId, processo.TipoRedacaoId, NomeDaRedacaoAlterada, ordem: 2));

    private async Task<Result<MutacaoAceita>> DefinirApenasAsync(
        ProcessoComDuasEtapas processo, PrecondicaoIfMatch precondicao, params EtapaProcessoInput[] etapas)
    {
        await using DefinicaoDasEtapasNoBanco definicao = new(fixture, RelogioDoDiaSeguinte());

        return await definicao.DefinirAsync(new DefinirEtapasCommand(processo.ProcessoId, etapas, precondicao));
    }
}
