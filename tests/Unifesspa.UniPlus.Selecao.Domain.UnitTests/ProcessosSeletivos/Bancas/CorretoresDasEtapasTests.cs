namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// O número de corretores é da versão da regra de consolidação que a etapa referencia. Zero só vale para a etapa
/// sem regra: a etapa com regra cujo número não foi lido não está resolvida, e um zero por omissão aceitaria uma banca
/// menor do que a regra exige.
/// </summary>
public sealed class CorretoresDasEtapasTests
{
    private static readonly ReferenciaRegra CorrecaoIndependente =
        ReferenciaRegra.De(RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo(), "v1", new string('d', 64));

    private static EtapaProcesso Etapa(string nome, int ordem, ReferenciaRegra? consolidacao) => EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
        new IdentificacaoDaEtapa(
            Nome: nome, Carater: CaraterEtapa.Classificatoria,
            TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            Ordem: ordem, FaseCodigo: "RESULTADO_FINAL"),
        new DeclaracaoDaAvaliacaoDaEtapa(
            Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null,
            FormaDeAvaliacao: consolidacao is null ? FormaDeAvaliacao.Objetiva : FormaDeAvaliacao.Humana,
            RegraConsolidacao: consolidacao, EscalaDeAtendimento: null)).Value!;

    [Fact(DisplayName = "A etapa sem regra de consolidação tem zero corretores, esteja a regra no conjunto ou não")]
    public void De_EtapaSemConsolidacao_EZero()
    {
        EtapaProcesso objetiva = Etapa("Objetiva", 1, consolidacao: null);

        CorretoresDeTeste.SemConsolidacao.EmCampo(objetiva).Should().Be(0);
        new CorretoresDasEtapas(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [CorrecaoIndependente] = CorretoresDaRegra.SemNovoCorretor(3) }).EmCampo(objetiva).Should().Be(0);
    }

    [Fact(DisplayName = "A etapa com regra de consolidação tem o número lido para a regra dela")]
    public void De_EtapaComConsolidacaoLida_DevolveONumero()
    {
        EtapaProcesso redacao = Etapa("Redação", 1, CorrecaoIndependente);

        new CorretoresDasEtapas(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [CorrecaoIndependente] = CorretoresDaRegra.SemNovoCorretor(3) }).EmCampo(redacao).Should().Be(3);
    }

    [Fact(DisplayName = "As etapas que referenciam a mesma regra têm o mesmo número")]
    public void De_MesmaReferencia_DaOMesmoNumero()
    {
        EtapaProcesso redacao = Etapa("Redação", 1, CorrecaoIndependente);
        EtapaProcesso projeto = Etapa("Projeto", 2, ReferenciaRegra.De(RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo(), "v1", new string('d', 64)));
        CorretoresDasEtapas corretores = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [CorrecaoIndependente] = CorretoresDaRegra.SemNovoCorretor(3) });

        corretores.EmCampo(projeto).Should().Be(corretores.EmCampo(redacao)).And.Be(3);
    }

    [Fact(DisplayName = "A etapa com regra fora do conjunto não está resolvida e o número dela não se presume")]
    public void De_EtapaComConsolidacaoForaDoConjunto_Lanca()
    {
        EtapaProcesso redacao = Etapa("Redação", 1, CorrecaoIndependente);
        ReferenciaRegra outraVersao = ReferenciaRegra.De(RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo(), "v2", new string('f', 64));
        CorretoresDasEtapas deOutraVersao = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [outraVersao] = CorretoresDaRegra.SemNovoCorretor(3) });

        Func<int> ler = () => deOutraVersao.EmCampo(redacao);
        Func<int> lerSemConjunto = () => CorretoresDeTeste.SemConsolidacao.EmCampo(redacao);

        ler.Should().Throw<InvalidOperationException>().WithMessage($"*{RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo()}*v1*");
        lerSemConjunto.Should().Throw<InvalidOperationException>();
        deOutraVersao.Resolvida(redacao).Should().BeFalse();
        CorretoresDeTeste.SemConsolidacao.Resolvida(redacao).Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa sem regra de consolidação está sempre resolvida, e a com regra lida também")]
    public void Resolvida_EtapaSemRegraOuComRegraLida_ESim()
    {
        EtapaProcesso objetiva = Etapa("Objetiva", 1, consolidacao: null);
        EtapaProcesso redacao = Etapa("Redação", 2, CorrecaoIndependente);
        CorretoresDasEtapas corretores = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [CorrecaoIndependente] = CorretoresDaRegra.SemNovoCorretor(3) });

        CorretoresDeTeste.SemConsolidacao.Resolvida(objetiva).Should().BeTrue();
        corretores.Resolvida(redacao).Should().BeTrue();
    }

    [Fact(DisplayName = "O conjunto copia os números recebidos, e mudar a origem depois não o altera")]
    public void Construcao_CopiaOsNumeros()
    {
        EtapaProcesso redacao = Etapa("Redação", 1, CorrecaoIndependente);
        Dictionary<ReferenciaRegra, CorretoresDaRegra> origem = new() { [CorrecaoIndependente] = CorretoresDaRegra.SemNovoCorretor(3) };
        CorretoresDasEtapas corretores = new(origem);

        origem.Clear();

        corretores.EmCampo(redacao).Should().Be(3);
    }

    [Fact(DisplayName = "Conjunto e etapa nulos são recusados")]
    public void Nulos_Lancam()
    {
        Action semConjunto = () => _ = new CorretoresDasEtapas(null!);
        Func<int> semEtapa = () => CorretoresDeTeste.SemConsolidacao.EmCampo(null!);
        Func<bool> semEtapaResolvida = () => CorretoresDeTeste.SemConsolidacao.Resolvida(null!);

        semConjunto.Should().Throw<ArgumentNullException>();
        semEtapa.Should().Throw<ArgumentNullException>();
        semEtapaResolvida.Should().Throw<ArgumentNullException>();
    }
}
