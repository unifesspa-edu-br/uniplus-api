namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O lugar que diz qual fase do cronograma vem depois de qual: a posição de cada fase é a ordem dela, lida da própria
/// instância da fase que consta na lista do cronograma informado.
/// </summary>
public sealed class OrdemDoCronogramaTests
{
    [Fact(DisplayName = "A fase de ordem maior é posterior à de ordem menor, e a de ordem menor não é posterior à maior")]
    public void Posterior_FasesDeOrdensDiferentes_SegueAOrdem()
    {
        FaseCronograma primeira = FaseDeTeste.Criar(1, "PRIMEIRA");
        FaseCronograma terceira = FaseDeTeste.Criar(3, "TERCEIRA");
        FaseCronograma[] cronograma = [primeira, terceira];

        OrdemDoCronograma.Do(cronograma).Posterior(terceira, primeira).Should().BeTrue();
        OrdemDoCronograma.Do(cronograma).Posterior(primeira, terceira).Should().BeFalse();
    }

    [Fact(DisplayName = "A fase não é posterior a ela mesma nem a outra fase na mesma posição")]
    public void Posterior_MesmaPosicao_NaoEPosterior()
    {
        FaseCronograma umaFase = FaseDeTeste.Criar(2, "UMA");
        FaseCronograma outraFase = FaseDeTeste.Criar(2, "OUTRA");
        FaseCronograma[] cronograma = [umaFase, outraFase];

        OrdemDoCronograma.Do(cronograma).Posterior(umaFase, umaFase).Should().BeFalse();
        OrdemDoCronograma.Do(cronograma).Posterior(umaFase, outraFase).Should().BeFalse();
        OrdemDoCronograma.Do(cronograma).Posterior(outraFase, umaFase).Should().BeFalse();
    }

    [Fact(DisplayName = "A fase da isenção na posição 1 não é posterior à inscrição na posição 2, e a inscrição é posterior a ela")]
    public void Posterior_IsencaoAntesDaInscricao_SegueAOrdemDeclarada()
    {
        FaseCronograma isencao = FaseDeTeste.Criar(1, "ISENCAO", coletaSolicitacaoIsencao: true);
        FaseCronograma inscricao = FaseDeTeste.Criar(2, "INSCRICAO", coletaInscricao: true);
        FaseCronograma[] cronograma = [isencao, inscricao];

        OrdemDoCronograma.Do(cronograma).Posterior(inscricao, isencao).Should().BeTrue();
        OrdemDoCronograma.Do(cronograma).Posterior(isencao, inscricao).Should().BeFalse();
    }

    [Fact(DisplayName = "A ordem em que as fases são informadas não muda a posição de cada uma")]
    public void Posterior_FasesForaDeOrdem_UsaAOrdemDeclarada()
    {
        FaseCronograma primeira = FaseDeTeste.Criar(1, "PRIMEIRA");
        FaseCronograma segunda = FaseDeTeste.Criar(2, "SEGUNDA");
        FaseCronograma[] cronograma = [segunda, primeira];

        OrdemDoCronograma.Do(cronograma).Posterior(segunda, primeira).Should().BeTrue();
    }

    [Fact(DisplayName = "Fases com a mesma identidade não se fundem: cada uma vale pela própria ordem")]
    public void Posterior_IdentidadeRepetida_CadaFaseValePelaPropriaOrdem()
    {
        Guid identidade = Guid.CreateVersion7();
        FaseCronograma antiga = FaseReidratada(identidade, 1);
        FaseCronograma recente = FaseReidratada(identidade, 4);

        FaseCronograma[] cronograma = [antiga, recente];

        OrdemDoCronograma.Do(cronograma).Posterior(recente, antiga).Should().BeTrue();
        OrdemDoCronograma.Do(cronograma).Posterior(antiga, recente).Should().BeFalse();
    }

    [Fact(DisplayName = "A fase que não está no cronograma informado é erro de quem pergunta, de qualquer lado da comparação")]
    public void Posterior_FaseForaDoCronograma_Lanca()
    {
        FaseCronograma noCronograma = FaseDeTeste.Criar(1, "NO_CRONOGRAMA");
        FaseCronograma deFora = FaseDeTeste.Criar(2, "DE_FORA");
        FaseCronograma[] cronograma = [noCronograma];

        Action comAFaseDeForaPrimeiro = () => OrdemDoCronograma.Do(cronograma).Posterior(deFora, noCronograma);
        Action comAFaseDeForaDepois = () => OrdemDoCronograma.Do(cronograma).Posterior(noCronograma, deFora);

        comAFaseDeForaPrimeiro.Should().Throw<ArgumentException>().WithParameterName("fase");
        comAFaseDeForaDepois.Should().Throw<ArgumentException>().WithParameterName("referencia");
    }

    [Fact(DisplayName = "A mais tardia é a de maior ordem, qualquer que seja a posição dela na lista")]
    public void MaisTardia_EscolheAMaiorOrdem()
    {
        FaseCronograma primeira = FaseDeTeste.Criar(1, "PRIMEIRA");
        FaseCronograma terceira = FaseDeTeste.Criar(3, "TERCEIRA");
        FaseCronograma segunda = FaseDeTeste.Criar(2, "SEGUNDA");
        FaseCronograma[] cronograma = [primeira, segunda, terceira];

        OrdemDoCronograma.Do(cronograma).MaisTardia([primeira, terceira, segunda]).Should().BeSameAs(terceira);
    }

    [Fact(DisplayName = "No empate, a mais tardia é a primeira na ordem recebida")]
    public void MaisTardia_Empate_FicaComAPrimeira()
    {
        FaseCronograma umaFase = FaseDeTeste.Criar(2, "UMA");
        FaseCronograma outraFase = FaseDeTeste.Criar(2, "OUTRA");
        FaseCronograma[] cronograma = [umaFase, outraFase];

        OrdemDoCronograma.Do(cronograma).MaisTardia([umaFase, outraFase]).Should().BeSameAs(umaFase);
        OrdemDoCronograma.Do(cronograma).MaisTardia([outraFase, umaFase]).Should().BeSameAs(outraFase);
    }

    [Fact(DisplayName = "As fases nulas são ignoradas, e sem nenhuma fase não há mais tardia")]
    public void MaisTardia_Nulas_SaoIgnoradas()
    {
        FaseCronograma unica = FaseDeTeste.Criar(1, "UNICA");
        FaseCronograma[] cronograma = [unica];

        OrdemDoCronograma.Do(cronograma).MaisTardia([null, unica, null]).Should().BeSameAs(unica);
        OrdemDoCronograma.Do(cronograma).MaisTardia([null, null]).Should().BeNull();
        OrdemDoCronograma.Do(cronograma).MaisTardia([]).Should().BeNull();
    }

    [Fact(DisplayName = "A fase que não está no cronograma informado também é erro de quem pergunta na escolha da mais tardia")]
    public void MaisTardia_FaseForaDoCronograma_Lanca()
    {
        FaseCronograma noCronograma = FaseDeTeste.Criar(1, "NO_CRONOGRAMA");
        FaseCronograma deFora = FaseDeTeste.Criar(2, "DE_FORA");
        FaseCronograma[] cronograma = [noCronograma];

        Func<FaseCronograma?> escolher = () => OrdemDoCronograma.Do(cronograma).MaisTardia([noCronograma, deFora]);

        escolher.Should().Throw<ArgumentException>().WithParameterName("fases");
    }

    [Fact(DisplayName = "Sem o cronograma, a ordem não se cria")]
    public void SemCronograma_Lanca()
    {
        Func<OrdemDoCronograma> criar = () => OrdemDoCronograma.Do(null!);

        criar.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Sem a fase, a referência ou a lista de fases, a comparação e a escolha não se fazem")]
    public void SemFaseReferenciaOuLista_Lanca()
    {
        FaseCronograma fase = FaseDeTeste.Criar(1, "FASE");
        FaseCronograma[] cronograma = [fase];

        OrdemDoCronograma ordem = OrdemDoCronograma.Do(cronograma);

        Action semFase = () => ordem.Posterior(null!, fase);
        Action semReferencia = () => ordem.Posterior(fase, null!);
        Func<FaseCronograma?> semLista = () => ordem.MaisTardia(null!);

        semFase.Should().Throw<ArgumentNullException>();
        semReferencia.Should().Throw<ArgumentNullException>();
        semLista.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "A mesma ordem responde a várias perguntas de forma consistente, e a mesma fase repetida na lista não lança")]
    public void MesmaOrdem_VariasPerguntas_RespondeDeFormaConsistente()
    {
        FaseCronograma primeira = FaseDeTeste.Criar(1, "PRIMEIRA");
        FaseCronograma segunda = FaseDeTeste.Criar(2, "SEGUNDA");
        FaseCronograma[] cronograma = [segunda, primeira, segunda];

        OrdemDoCronograma ordem = OrdemDoCronograma.Do(cronograma);

        ordem.Posterior(segunda, primeira).Should().BeTrue();
        ordem.MaisTardia([primeira, segunda]).Should().BeSameAs(segunda);
        ordem.Posterior(primeira, segunda).Should().BeFalse();
        ordem.MaisTardia([segunda, primeira]).Should().BeSameAs(segunda);
        ordem.Posterior(segunda, primeira).Should().BeTrue();
    }

    private static FaseCronograma FaseReidratada(Guid id, int ordem) =>
        FaseCronograma.Reidratar(
            id,
            new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), $"FASE_{ordem}", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], EmiteParecerIndividual: false, RegraRecurso: null),
            []);
}
