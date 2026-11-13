namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class DefinirEtapasPrecedenciaDasRecusasTests
{
    private static readonly DateTimeOffset AberturaDaSessao = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static ProcessoSeletivo ProcessoPublicado(out VersaoConfiguracao versao)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            ProcessoConformeFactory.Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", new string('a', 64), "user-sub-1",
            TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        versao = publicacao.Value!;
        return processo;
    }

    private static ProcessoSeletivo ComSessaoAberta(out RascunhoRetificacao sessao)
    {
        ProcessoSeletivo processo = ProcessoPublicado(out VersaoConfiguracao versao);
        sessao = processo.AbrirRetificacao(
            "Correção das etapas", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "user-sub-1", AberturaDaSessao).Value!;
        return processo;
    }

    private static EtapaProcesso[] AcrescidasDeOutraNaMesmaOrdem(ProcessoSeletivo processo) =>
        [.. processo.Etapas, Etapa("Outra prova", processo.Etapas.Single().Ordem)];

    private static EtapaProcesso[] AcrescidasDeUmaComPreliminarAberto(ProcessoSeletivo processo) =>
        [.. processo.Etapas, EtapaComPreliminaresAbertos("Prova escrita", 9, "GABARITO")];

    private static void DeveTerPermanecido(ProcessoSeletivo processo, EtapaProcesso[] antes, IEnumerable<EtapaProcesso> informadas)
    {
        processo.Etapas.Should().HaveCount(antes.Length);
        processo.Etapas.Zip(antes).Should().OnlyContain(par => ReferenceEquals(par.First, par.Second));
        informadas.Where(e => !antes.Contains(e)).Should().OnlyContain(e => e.ProcessoSeletivoId == Guid.Empty);
    }

    [Fact(DisplayName = "Recusa simples da coleção deixa as etapas idênticas, nas mesmas instâncias e na mesma ordem")]
    public void RecusaDaColecao_NaoMudaAsEtapas()
    {
        ProcessoSeletivo processo = ComEtapas(NovoProcesso(), Etapa("Objetiva", ordem: 1), Etapa("Redação", ordem: 2));
        EtapaProcesso[] antes = [.. processo.Etapas];
        EtapaProcesso[] informadas = [Etapa("Entrevista", ordem: 1), Etapa("Análise", ordem: 1)];

        Result resultado = processo.DefinirEtapas(informadas, PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.OrdemEtapaDuplicada");
        DeveTerPermanecido(processo, antes, informadas);
    }

    [Fact(DisplayName = "Violação do ciclo recursal deixa as etapas idênticas, nas mesmas instâncias e na mesma ordem")]
    public void ViolacaoDoCiclo_NaoMudaAsEtapas()
    {
        ProcessoSeletivo processo = ComEtapas(NovoProcesso(), Etapa("Objetiva", ordem: 1), Etapa("Redação", ordem: 2));
        EtapaProcesso[] antes = [.. processo.Etapas];
        EtapaProcesso[] informadas = [EtapaComPreliminaresAbertos("Entrevista", 1, "GABARITO")];

        Result resultado = processo.DefinirEtapas(informadas, PrecondicaoIfMatch.Ausente);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].produtos");
        DeveTerPermanecido(processo, antes, informadas);
    }

    [Fact(DisplayName = "Recusa por dependência de outra configuração deixa as etapas e o desempate como estavam")]
    public void RecusaPorDependencia_NaoMudaAsEtapas()
    {
        EtapaProcesso citada = Etapa("Redação", ordem: 1);
        ProcessoSeletivo processo = ComDesempatePelaNotaDa(ComEtapas(NovoProcesso(), citada, Etapa("Objetiva", ordem: 2)), citada);
        EtapaProcesso[] antes = [.. processo.Etapas];
        EtapaProcesso[] informadas = [Etapa("Entrevista", ordem: 1)];

        Result resultado = processo.DefinirEtapas(informadas, PrecondicaoIfMatch.Ausente);

        DeveRecusarCom(resultado, "ProcessoSeletivo.EtapaReferenciadaPorDesempate");
        DeveTerPermanecido(processo, antes, informadas);
        processo.CriteriosDesempate.Should().ContainSingle();
    }

    [Fact(DisplayName = "Sob sessão, a recusa da coleção não avança a revisão nem muda a etag da sessão")]
    public void SobSessao_RecusaDaColecao_NaoAvancaARevisao()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao sessao);
        EtapaProcesso[] antes = [.. processo.Etapas];
        EtapaProcesso[] informadas = AcrescidasDeOutraNaMesmaOrdem(processo);
        string etag = sessao.ETag;

        Result resultado = processo.DefinirEtapas(informadas, PrecondicaoIfMatch.DeTags([etag]));

        DeveRecusarCom(resultado, "ProcessoSeletivo.OrdemEtapaDuplicada");
        sessao.Revisao.Should().Be(1);
        processo.ETagDaSessaoEditorial.Should().Be(etag);
        DeveTerPermanecido(processo, antes, informadas);
    }

    [Fact(DisplayName = "Sob sessão, a violação do ciclo recursal não avança a revisão")]
    public void SobSessao_ViolacaoDoCiclo_NaoAvancaARevisao()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao sessao);
        EtapaProcesso[] antes = [.. processo.Etapas];
        EtapaProcesso[] informadas = AcrescidasDeUmaComPreliminarAberto(processo);

        Result resultado = processo.DefinirEtapas(informadas, PrecondicaoIfMatch.DeTags([sessao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[1].produtos");
        sessao.Revisao.Should().Be(1);
        DeveTerPermanecido(processo, antes, informadas);
    }

    [Fact(DisplayName = "Sob sessão, cada gravação aceita avança a revisão uma vez e troca as etapas")]
    public void SobSessao_Sucesso_AvancaARevisaoUmaVez()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao sessao);
        EtapaProcesso[] informadas = [.. processo.Etapas, Etapa("Entrevista", 9)];

        Result primeira = processo.DefinirEtapas(informadas, PrecondicaoIfMatch.DeTags([sessao.ETag]));

        primeira.IsSuccess.Should().BeTrue(primeira.Error?.Message);
        sessao.Revisao.Should().Be(2);
        processo.Etapas.Should().HaveCount(informadas.Length);
        processo.DefinirEtapas(informadas, PrecondicaoIfMatch.DeTags([sessao.ETag])).IsSuccess.Should().BeTrue();
        sessao.Revisao.Should().Be(3);
    }
}
