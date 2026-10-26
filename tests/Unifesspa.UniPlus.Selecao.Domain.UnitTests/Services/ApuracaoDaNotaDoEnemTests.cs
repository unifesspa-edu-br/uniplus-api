namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A nota da etapa do ENEM: média das cinco áreas ponderada pelos pesos do grupo do curso, sem
/// arredondar, uma avaliação por oferta concorrida e "sem nota" com o motivo quando faltam os lançamentos.
/// </summary>
public sealed class ApuracaoDaNotaDoEnemTests
{
    private const string Tecnologica = "TECNOLOGICA";
    private const string HumanisticaI = "HUMANISTICA_I";

    private static readonly IReadOnlyList<PesoDaArea> PesosTecnologica =
    [
        new(AreaDoEnem.Redacao, 2.0m),
        new(AreaDoEnem.CienciasDaNatureza, 2.5m),
        new(AreaDoEnem.CienciasHumanas, 1.5m),
        new(AreaDoEnem.Linguagens, 1.5m),
        new(AreaDoEnem.Matematica, 2.5m),
    ];

    private static readonly IReadOnlyList<PesoDaArea> PesosHumanisticaI =
    [
        new(AreaDoEnem.Redacao, 2.0m),
        new(AreaDoEnem.CienciasDaNatureza, 1.0m),
        new(AreaDoEnem.CienciasHumanas, 3.0m),
        new(AreaDoEnem.Linguagens, 3.0m),
        new(AreaDoEnem.Matematica, 1.0m),
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<PesoDaArea>> Quadro =
        new Dictionary<string, IReadOnlyList<PesoDaArea>>
        {
            [Tecnologica] = PesosTecnologica,
            [HumanisticaI] = PesosHumanisticaI,
        };

    private static ResultadoDaAreaNoRetorno[] Areas(
        decimal redacao, decimal natureza, decimal humanas, decimal linguagens, decimal matematica) =>
    [
        new(AreaDoEnem.Redacao, redacao, SituacaoDaProvaNoEnem.Presente),
        new(AreaDoEnem.CienciasDaNatureza, natureza, SituacaoDaProvaNoEnem.Presente),
        new(AreaDoEnem.CienciasHumanas, humanas, SituacaoDaProvaNoEnem.Presente),
        new(AreaDoEnem.Linguagens, linguagens, SituacaoDaProvaNoEnem.Presente),
        new(AreaDoEnem.Matematica, matematica, SituacaoDaProvaNoEnem.Presente),
    ];

    private static IndiceCegoDoCpf Indice(string valor) => IndiceCegoDoCpf.De(Encoding.UTF8.GetBytes(valor));

    private static ResultadoDoCruzamento Cruzamento(
        IReadOnlyList<InscritoComNota>? comNota = null,
        IReadOnlyList<Guid>? semLinha = null,
        IReadOnlyList<ConflitoDeLinhas>? conflitos = null) =>
        new(comNota ?? [], semLinha ?? [], [], conflitos ?? []);

    [Fact(DisplayName = "a nota é a média das áreas ponderada pelos pesos do grupo: Tecnológica dá 674,5")]
    public void Apurar_MediaPonderada()
    {
        decimal nota = ApuracaoDaNotaDoEnem.Apurar(Areas(redacao: 800m, natureza: 650m, humanas: 580m, linguagens: 600m, matematica: 700m), PesosTecnologica);

        nota.Should().Be(674.5m);
    }

    [Fact(DisplayName = "a nota não é arredondada")]
    public void Apurar_NaoArredonda()
    {
        IReadOnlyList<PesoDaArea> pesos =
        [
            new(AreaDoEnem.Redacao, 1m),
            new(AreaDoEnem.CienciasDaNatureza, 1m),
            new(AreaDoEnem.CienciasHumanas, 1m),
            new(AreaDoEnem.Linguagens, 0m),
            new(AreaDoEnem.Matematica, 0m),
        ];

        decimal nota = ApuracaoDaNotaDoEnem.Apurar(Areas(100m, 100m, 101m, 900m, 900m), pesos);

        nota.Should().Be(301m / 3m);
        nota.Should().NotBe(Math.Round(nota, 3));
    }

    [Fact(DisplayName = "a prova ausente entra na média com o zero do lançamento")]
    public void Apurar_AusenteEntraComZero()
    {
        ResultadoDaAreaNoRetorno[] areas = Areas(redacao: 800m, natureza: 650m, humanas: 580m, linguagens: 600m, matematica: 0m);
        areas[4] = areas[4] with { Situacao = SituacaoDaProvaNoEnem.Ausente };

        decimal nota = ApuracaoDaNotaDoEnem.Apurar(areas, PesosTecnologica);

        nota.Should().Be(((800m * 2m) + (650m * 2.5m) + (580m * 1.5m) + (600m * 1.5m)) / 10m);
    }

    [Fact(DisplayName = "pesos que somam zero ou sem alguma das cinco áreas são violação do quadro, não nota")]
    public void Apurar_QuadroInvalido_Lanca()
    {
        IReadOnlyList<PesoDaArea> somamZero = [.. AreaDoEnem.Todas.Select(a => new PesoDaArea(a, 0m))];
        IReadOnlyList<PesoDaArea> semMatematica = [.. PesosTecnologica.Where(p => p.AreaCodigo != AreaDoEnem.Matematica)];
        ResultadoDaAreaNoRetorno[] areas = Areas(redacao: 800m, natureza: 650m, humanas: 580m, linguagens: 600m, matematica: 700m);

        Action zerados = () => ApuracaoDaNotaDoEnem.Apurar(areas, somamZero);
        Action incompletos = () => ApuracaoDaNotaDoEnem.Apurar(areas, semMatematica);

        zerados.Should().Throw<InvalidOperationException>();
        incompletos.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "1ª e 2ª opção em grupos diferentes dão duas avaliações com notas e grupos diferentes")]
    public void ApurarDoLote_SegundaOpcaoEmOutroGrupo()
    {
        Guid inscricao = Guid.CreateVersion7();
        Guid primeira = Guid.CreateVersion7();
        Guid segunda = Guid.CreateVersion7();
        ResultadoDaAreaNoRetorno[] areas = Areas(redacao: 800m, natureza: 650m, humanas: 580m, linguagens: 600m, matematica: 700m);

        IReadOnlyList<AvaliacaoApurada> avaliacoes = ApuracaoDaNotaDoEnem.ApurarDoLote(
            Cruzamento(comNota: [new InscritoComNota(inscricao, "240000000001", areas)]),
            [new InscritoDaEdicao(inscricao, Indice("a"), [primeira, segunda])],
            new Dictionary<Guid, string> { [primeira] = Tecnologica, [segunda] = HumanisticaI },
            Quadro);

        avaliacoes.Should().HaveCount(2);
        AvaliacaoApurada daPrimeira = avaliacoes.Single(a => a.OfertaCursoId == primeira);
        AvaliacaoApurada daSegunda = avaliacoes.Single(a => a.OfertaCursoId == segunda);
        daPrimeira.GrupoAreaEnemCodigo.Should().Be(Tecnologica);
        daPrimeira.Situacao.Should().Be(SituacaoDaAvaliacao.Apurada);
        daPrimeira.Nota.Should().Be(674.5m);
        daSegunda.GrupoAreaEnemCodigo.Should().Be(HumanisticaI);
        daSegunda.Nota.Should().Be(ApuracaoDaNotaDoEnem.Apurar(areas, PesosHumanisticaI));
        daSegunda.Nota.Should().NotBe(daPrimeira.Nota);
    }

    [Fact(DisplayName = "quem não tem linha ou está em conflito fica sem nota, com o motivo, nunca com zero")]
    public void ApurarDoLote_SemNota_TemMotivo()
    {
        Guid semLinha = Guid.CreateVersion7();
        Guid emConflito = Guid.CreateVersion7();
        Guid oferta = Guid.CreateVersion7();

        IReadOnlyList<AvaliacaoApurada> avaliacoes = ApuracaoDaNotaDoEnem.ApurarDoLote(
            Cruzamento(semLinha: [semLinha], conflitos: [new ConflitoDeLinhas(emConflito, [2, 3])]),
            [
                new InscritoDaEdicao(semLinha, Indice("a"), [oferta]),
                new InscritoDaEdicao(emConflito, Indice("b"), [oferta]),
            ],
            new Dictionary<Guid, string> { [oferta] = Tecnologica },
            Quadro);

        avaliacoes.Should().OnlyContain(a => a.Nota == null);
        avaliacoes.Single(a => a.InscricaoId == semLinha).Situacao.Should().Be(SituacaoDaAvaliacao.InscritoSemLinha);
        avaliacoes.Single(a => a.InscricaoId == emConflito).Situacao.Should().Be(SituacaoDaAvaliacao.EmConflito);
    }
}
