namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ProcessoSeletivoConformeBuilder;

/// <summary>
/// A publicação recusa a regra de homologação que confirma o INEP de uma edição do ENEM que o processo não declara. A regra do
/// processo conforme confirma a edição 2026. A retificação passa pela mesma conferência da configuração, exercida nos testes do
/// domínio: o processo publicado não aceita mutação, e por isso não há como declará-lo, nesta suíte, com uma edição que a
/// publicação já recusaria.
/// </summary>
public sealed class EdicaoDoExameDaHomologacaoAoCongelarTests
{
    private const string CampoDaEdicao = "/cronograma/fases/0/regraDeHomologacao/alternativas/0/condicoes/0/edicaoDoExame";

    private static ProcessoSeletivo ProcessoComAsEdicoesDeclaradas(params string[] edicoes)
    {
        ProcessoSeletivo processo = Criar("PS Edições do ENEM");
        processo.DefinirClassificacao(ClassificacaoImportadaBaseadaEmEnem()).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas(
            "EDICAO_ENEM", [.. edicoes.Select(static (edicao, ordem) => OpcaoDeclaradaFato.Criar("EDICAO_ENEM", edicao, edicao, ordem).Value!)])
            .IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);

        return processo;
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> PublicarComAsEdicoesAsync(params string[] edicoes)
    {
        ProcessoSeletivo processo = ProcessoComAsEdicoesDeclaradas(edicoes);

        return ServicosDoCongelamentoDeTeste.Para(processo).PublicarAsync(processo.Id);
    }

    private static ConfiguracaoClassificacao ClassificacaoImportadaBaseadaEmEnem() => ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
    {
        RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", HashFixo),
        RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", HashFixo),
        BaseadoEmEnem = true,
    }).Value!;

    [Fact(DisplayName = "A publicação recusa a edição da regra que o processo não declara, no campo da condição, sem citar o ano")]
    public async Task Publicar_EdicaoDaRegraNaoDeclarada_RecusaNoCampoDaCondicao()
    {
        (Result resposta, IEnumerable<object> eventos) = await PublicarComAsEdicoesAsync("2024", "2025");

        FieldError causa = resposta.Errors.Where(static e => e.Error.Code == HomologacaoDasInscricoesErrorCodes.EdicaoDoExameNaoDeclarada)
            .Should().ContainSingle().Subject;
        causa.Field.Should().Be(CampoDaEdicao);
        causa.Error.Message.Should().NotContain("2026");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A publicação não recusa a edição da regra que o processo declara")]
    public async Task Publicar_EdicaoDaRegraDeclarada_NaoRecusaPelaEdicao()
    {
        (Result resposta, _) = await PublicarComAsEdicoesAsync("2025", "2026");

        resposta.Errors.Select(static e => e.Error.Code).Should().NotContain(HomologacaoDasInscricoesErrorCodes.EdicaoDoExameNaoDeclarada);
    }
}
