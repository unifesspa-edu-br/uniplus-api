namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static EtapasNoBancoDeTeste;

/// <summary>
/// A definição das etapas pelo handler real contra o Postgres: o que a substituição deixa nas linhas de
/// <c>etapas_processo</c> e o que a recusa não deixa. Os serviços de aplicação vêm do registro do módulo e o
/// repositório, a leitura das fases iniciadas e a unidade de trabalho são os de produção.
/// </summary>
public sealed partial class DefinirEtapasPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    private const string NomeDaProvaAlterada = "Prova Objetiva — 1ª fase";
    private const string NomeDaProvaNova = "Prova Didática";

    private static readonly Guid TipoDaProvaNova = Guid.CreateVersion7();

    // O dia seguinte à publicação semeada: a versão publicada já vige e o relógio é lido uma vez, sem espera real.
    private static RelogioFixo RelogioDoDiaSeguinte() => new RelogioFixo(DateTimeOffset.UtcNow.AddDays(1));

    [Fact(DisplayName = "Em rascunho, a substituição tira uma etapa, acrescenta outra e altera uma, e o banco guarda só o novo conjunto")]
    public async Task Definir_EmRascunho_SubstituiAsEtapasNoBanco()
    {
        ProcessoComDuasEtapas processo = await SemearRascunhoAsync(fixture, $"PS substituição em rascunho {Guid.CreateVersion7()}");

        Result<MutacaoAceita> resultado = await DefinirAsync(processo, PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await ConferirOConjuntoSubstituidoAsync(processo);
    }

    [Fact(DisplayName = "Na retificação, a substituição mantém a etapa iniciada alterada, retira a que não começou e grava a nova")]
    public async Task Definir_NaRetificacao_SubstituiSemTocarNaEtapaIniciadaMantida()
    {
        ProcessoComDuasEtapas processo = await SemearRetificacaoComProvaIniciadaAsync(fixture, $"PS substituição na retificação {Guid.CreateVersion7()}");
        string? versaoDaSessaoAntes = await ETagDaSessaoGravadaAsync(fixture, processo.ProcessoId);

        Result<MutacaoAceita> resultado = await DefinirAsync(processo, PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await ConferirOConjuntoSubstituidoAsync(processo);
        string? versaoDaSessaoDepois = await ETagDaSessaoGravadaAsync(fixture, processo.ProcessoId);
        versaoDaSessaoDepois.Should().NotBe(versaoDaSessaoAntes, "a sessão editorial avança a cada edição aceita");
        resultado.Value!.ETag.Should().Be(versaoDaSessaoDepois, "o handler devolve a versão que gravou");
    }

    private async Task<Result<MutacaoAceita>> DefinirAsync(ProcessoComDuasEtapas processo, PrecondicaoIfMatch precondicao)
    {
        await using DefinicaoDasEtapasNoBanco definicao = NovaDefinicao(processo);

        return await definicao.DefinirAsync(new DefinirEtapasCommand(
            processo.ProcessoId,
            [
                Reenvio(processo.ProvaObjetivaId, processo.TipoProvaObjetivaId, NomeDaProvaAlterada, ordem: 1, peso: 2m),
                Nova(TipoDaProvaNova, NomeDaProvaNova, ordem: 3),
            ],
            precondicao));
    }

    private DefinicaoDasEtapasNoBanco NovaDefinicao(ProcessoComDuasEtapas processo) => new(
        fixture,
        RelogioDoDiaSeguinte(),
        CadastroDoTipo(processo.TipoProvaObjetivaId, "PROVA_OBJETIVA", "Prova Objetiva"),
        CadastroDoTipo(TipoDaProvaNova, "PROVA_DIDATICA", NomeDaProvaNova));

    private async Task ConferirOConjuntoSubstituidoAsync(ProcessoComDuasEtapas processo)
    {
        IReadOnlyList<EtapaGravada> gravadas = await EtapasGravadasAsync(fixture, processo.ProcessoId);

        gravadas.Select(e => (e.Nome, e.Ordem, e.Peso)).Should().BeEquivalentTo(
            [(NomeDaProvaNova, (int?)3, (decimal?)1m), (NomeDaProvaAlterada, (int?)1, (decimal?)2m)]);
        gravadas.Should().OnlyContain(e => e.ProcessoSeletivoId == processo.ProcessoId, "toda etapa gravada aponta para o processo");
        gravadas.Should().ContainSingle(e => e.Id == processo.ProvaObjetivaId, "a etapa reenviada com o mesmo Id é atualizada, não recriada");
        gravadas.Should().NotContain(e => e.Id == processo.RedacaoId, "a etapa fora do payload é removida");
        (await BancasGravadasAsync(fixture, processo.RedacaoId)).Should().Be(0, "a banca da etapa removida sai com ela");
    }
}
