namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed partial class DefinirEtapasPrecedenciaDasRecusasTests
{
    private static void ForcarStatus(ProcessoSeletivo processo, StatusProcesso status) =>
        typeof(ProcessoSeletivo)
            .GetProperty(nameof(ProcessoSeletivo.Status))!
            .SetValue(processo, status);

    [Fact(DisplayName = "Processo publicado sem sessão recusa a mutação antes de qualquer regra das etapas")]
    public void PublicadoSemSessao_RecusaAntesDasRegrasDasEtapas()
    {
        ProcessoSeletivo processo = ProcessoPublicado(out _);
        EtapaProcesso[] antes = [.. processo.Etapas];
        EtapaProcesso[] informadas = AcrescidasDeOutraNaMesmaOrdem(processo);

        Result resultado = processo.DefinirEtapas(informadas, PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        resultado.Errors.Should().ContainSingle();
        DeveTerPermanecido(processo, antes, informadas);
    }

    [Fact(DisplayName = "Processo fora de rascunho e publicado recusa a mutação antes de qualquer regra das etapas")]
    public void ForaDeEstadoEditavel_RecusaAntesDasRegrasDasEtapas()
    {
        ProcessoSeletivo processo = ComEtapas(NovoProcesso(), Etapa("Objetiva", ordem: 1));
        ForcarStatus(processo, StatusProcesso.Encerrado);

        Result resultado = processo.DefinirEtapas([Etapa("Redação", ordem: 1), Etapa("Entrevista", ordem: 1)], PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.MutacaoForaDeEstadoEditavel");
        processo.Etapas.Should().ContainSingle().Which.Nome.Should().Be("Objetiva");
    }

    [Fact(DisplayName = "Sob sessão, a precondição ausente é a recusa, antes das regras das etapas")]
    public void SobSessao_PrecondicaoAusente_RecusaAntesDasRegrasDasEtapas()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao sessao);
        EtapaProcesso[] antes = [.. processo.Etapas];
        EtapaProcesso[] informadas = AcrescidasDeOutraNaMesmaOrdem(processo);

        Result resultado = processo.DefinirEtapas(informadas, PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "Precondicao.Requerida");
        sessao.Revisao.Should().Be(1);
        DeveTerPermanecido(processo, antes, informadas);
    }

    [Fact(DisplayName = "Sob sessão, a precondição defasada é a recusa, antes das regras das etapas")]
    public void SobSessao_PrecondicaoDefasada_RecusaAntesDasRegrasDasEtapas()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao sessao);
        EtapaProcesso[] antes = [.. processo.Etapas];
        EtapaProcesso[] informadas = AcrescidasDeUmaComPreliminarAberto(processo);

        Result resultado = processo.DefinirEtapas(informadas, PrecondicaoIfMatch.DeTags(["\"etag-de-outra-revisao\""]));

        DeveRecusarCom(resultado, "Precondicao.Falhou");
        sessao.Revisao.Should().Be(1);
        DeveTerPermanecido(processo, antes, informadas);
    }

    [Fact(DisplayName = "Lista de etapas nula é erro de programação, não recusa de negócio")]
    public void ListaNula_Lanca()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Action act = () => processo.DefinirEtapas(null!, PrecondicaoIfMatch.Ausente);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Precondição nula é erro de programação, não recusa de negócio")]
    public void PrecondicaoNula_Lanca()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Action act = () => processo.DefinirEtapas([Etapa("Objetiva", ordem: 1)], null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
