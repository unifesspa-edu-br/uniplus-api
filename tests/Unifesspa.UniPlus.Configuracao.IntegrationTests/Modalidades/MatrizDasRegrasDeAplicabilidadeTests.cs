namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A união das regras de aplicabilidade semeadas, avaliada pelo motor de derivação sobre as
/// respostas do candidato: os perfis da derivação de modalidade pela Lei de Cotas (UNI-REQ-0076) e
/// a exclusividade entre cota da lei e ação afirmativa, com a exceção da reserva de pessoa com
/// deficiência (UNI-REQ-0142 e UNI-REQ-0266). A opção pelas cotas da lei sai da regra padrão do fato
/// semeado, como sairá na inscrição.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class MatrizDasRegrasDeAplicabilidadeTests
{
    private static readonly string[] CotasDaLei = ["LI_EP", "LB_EP", "LI_PPI", "LB_PPI", "LI_Q", "LB_Q", "LI_PCD", "LB_PCD"];
    private static readonly string[] AcoesAfirmativasQueNaoSeSomamAsCotas = ["AC_I", "AC_Q"];
    private static readonly string[] CoresOuRacas = ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA"];

    private static readonly string[] FatosBooleanos =
    [
        OrigemEscolar.FatoEgresso, OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, "BAIXA_RENDA", "CONCORRER_RENDA",
        "CONCORRER_PPI", "QUILOMBOLA", "CONCORRER_Q", "PCD", "CONCORRER_PCD", "INDIGENA", "CONCORRER_VAGA_POR_ACRESCIMO",
    ];

    private static readonly DerivacaoDoFato Opcao = DerivacaoDoFato.CriarBooleana(
        OpcaoPelasCotasDaLei.Fato,
        OpcaoPelasCotasDaLei.RegrasDaOpcao,
        [OrigemEscolar.FatoEgresso, OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica]).Value!;

    private static readonly DerivacaoDoFato Modalidades = DerivacaoDoFato.Criar(
        ModalidadeDoCandidato.Fato,
        [.. ModalidadeSeed.Itens.Select(static i => RegraDerivacao.Criar(i.RegraDeAplicabilidade, i.Codigo).Value!)],
        [.. ModalidadeSeed.Itens.SelectMany(static i => i.RegraDeAplicabilidade.FatosCitados).Distinct(StringComparer.Ordinal)],
        [.. ModalidadeSeed.Itens.Select(static i => i.Codigo)]).Value!;

    [Fact(DisplayName = "Pessoa com deficiência que não é egressa de escola pública concorre a AC e AC_PCD")]
    public void PcdForaDaEscolaPublica() =>
        Derivar(Candidato(("PCD", true), ("CONCORRER_PCD", true), (OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, true)))
            .Should().BeEquivalentTo(["AC", "AC_PCD"]);

    [Fact(DisplayName = "Pessoa com deficiência egressa de escola pública que recusou as cotas concorre a AC e AC_PCD")]
    public void PcdEgressaQueRecusouAsCotas() =>
        Derivar(Candidato((OrigemEscolar.FatoEgresso, true), ("PCD", true), ("CONCORRER_PCD", true)))
            .Should().BeEquivalentTo(["AC", "AC_PCD"]);

    [Fact(DisplayName = "Pessoa com deficiência egressa que optou pelas cotas, sem renda, concorre a AC, LI_EP, LI_PCD e AC_PCD")]
    public void PcdEgressaQueOptouPelasCotas() =>
        Derivar(Candidato(
                (OrigemEscolar.FatoEgresso, true), (OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, true),
                ("PCD", true), ("CONCORRER_PCD", true)))
            .Should().BeEquivalentTo(["AC", "LI_EP", "LI_PCD", "AC_PCD"]);

    [Fact(DisplayName = "Pessoa com deficiência egressa que optou pelas cotas, com renda, concorre às cotas de renda e à AC_PCD")]
    public void PcdEgressaQueOptouPelasCotasComRenda() =>
        Derivar(Candidato(
                (OrigemEscolar.FatoEgresso, true), (OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, true),
                ("PCD", true), ("CONCORRER_PCD", true), ("BAIXA_RENDA", true), ("CONCORRER_RENDA", true)))
            .Should().BeEquivalentTo(["AC", "LI_EP", "LB_EP", "LI_PCD", "LB_PCD", "AC_PCD"]);

    [Fact(DisplayName = "Pessoa com deficiência que optou pelas cotas, mas não quer concorrer como tal, não concorre a LI_PCD, LB_PCD nem AC_PCD")]
    public void PcdQueOptouPelasCotasESemConcorrerComoPcd() =>
        Derivar(Candidato(
                (OrigemEscolar.FatoEgresso, true), (OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, true),
                ("PCD", true), ("BAIXA_RENDA", true), ("CONCORRER_RENDA", true)))
            .Should().BeEquivalentTo(["AC", "LI_EP", "LB_EP"]);

    [Fact(DisplayName = "Candidato sem deficiência que não optou pelas cotas concorre só a AC")]
    public void SemDeficienciaSemOpcao() =>
        Derivar(Candidato((OrigemEscolar.FatoEgresso, true), ("CONCORRER_PCD", true)))
            .Should().BeEquivalentTo(["AC"]);

    [Fact(DisplayName = "Renda não aplicável mantém só as cotas independentes de renda")]
    public void RendaNaoAplicavel()
    {
        Dictionary<string, FatoResolvido> candidato = Candidato(
            (OrigemEscolar.FatoEgresso, true), (OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, true),
            ("PCD", true), ("CONCORRER_PCD", true), ("CONCORRER_RENDA", true));
        candidato["BAIXA_RENDA"] = FatoResolvido.NaoAplicavel();

        Derivar(candidato).Should().BeEquivalentTo(["AC", "LI_EP", "LI_PCD", "AC_PCD"]);
    }

    [Fact(DisplayName = "Cotista de renda em várias dimensões concorre também às cotas independentes de renda de cada uma")]
    public void CotaDeRendaExpandeParaAIndependenteDeRenda() =>
        Derivar(Candidato(
                ("COR_RACA", "PRETA"), (OrigemEscolar.FatoEgresso, true), (OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, true),
                ("BAIXA_RENDA", true), ("CONCORRER_RENDA", true), ("CONCORRER_PPI", true),
                ("QUILOMBOLA", true), ("CONCORRER_Q", true)))
            .Should().BeEquivalentTo(["AC", "LI_EP", "LB_EP", "LI_PPI", "LB_PPI", "LI_Q", "LB_Q"]);

    [Fact(DisplayName = "Indígena com o pertencimento declarado concorre à cota de pretos, pardos e indígenas, mas não à quilombola")]
    public void IndigenaComPertencimento() =>
        Derivar(Candidato(
                ("COR_RACA", "INDIGENA"), ("INDIGENA", true), (OrigemEscolar.FatoEgresso, true),
                (OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, true), ("CONCORRER_PPI", true), ("CONCORRER_Q", true)))
            .Should().BeEquivalentTo(["AC", "LI_EP", "LI_PPI"]);

    [Fact(DisplayName = "Indígena pela cor sem o pertencimento declarado não concorre à cota de pretos, pardos e indígenas")]
    public void IndigenaPelaCorSemPertencimento() =>
        Derivar(Candidato(
                ("COR_RACA", "INDIGENA"), (OrigemEscolar.FatoEgresso, true), (OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, true),
                ("BAIXA_RENDA", true), ("CONCORRER_RENDA", true), ("CONCORRER_PPI", true)))
            .Should().BeEquivalentTo(["AC", "LI_EP", "LB_EP"]);

    [Fact(DisplayName = "Candidato branco que pede a cota de pretos, pardos e indígenas não concorre a ela")]
    public void BrancoComOpcaoPpi() =>
        Derivar(Candidato(
                ("COR_RACA", "BRANCA"), (OrigemEscolar.FatoEgresso, true), (OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, true),
                ("BAIXA_RENDA", true), ("CONCORRER_RENDA", true), ("CONCORRER_PPI", true)))
            .Should().BeEquivalentTo(["AC", "LI_EP", "LB_EP"]);

    [Fact(DisplayName = "Indígena e quilombola fora das cotas da lei concorrem às vagas por acréscimo de cada um")]
    public void VagasPorAcrescimo() =>
        Derivar(Candidato(("INDIGENA", true), ("QUILOMBOLA", true), ("CONCORRER_VAGA_POR_ACRESCIMO", true)))
            .Should().BeEquivalentTo(["AC", "AC_I", "AC_Q"]);

    [Fact(DisplayName = "Nenhuma combinação deriva cota da lei junto de AC_I ou AC_Q, AC_PCD junto de cota só com deficiência, e todas derivam AC")]
    public void ExclusividadeEntreCotaDaLeiEAcaoAfirmativa()
    {
        foreach (Dictionary<string, FatoResolvido> candidato in TodasAsCombinacoesDeRespostas())
        {
            IReadOnlySet<string> modalidades = Derivar(candidato);

            modalidades.Should().Contain("AC");
            ConferirAExclusividade(modalidades, concorreComoPessoaComDeficiencia: EhVerdadeiro(candidato, "PCD") && EhVerdadeiro(candidato, "CONCORRER_PCD"));
        }
    }

    private static void ConferirAExclusividade(IReadOnlySet<string> modalidades, bool concorreComoPessoaComDeficiencia)
    {
        bool comCotaDaLei = modalidades.Overlaps(CotasDaLei);
        string derivadas = string.Join(", ", modalidades);

        (comCotaDaLei && modalidades.Overlaps(AcoesAfirmativasQueNaoSeSomamAsCotas)).Should().BeFalse(
            $"{derivadas} junta cota da lei e ação afirmativa que não se soma a ela");
        (comCotaDaLei && modalidades.Contains("AC_PCD") && !concorreComoPessoaComDeficiencia).Should().BeFalse(
            $"{derivadas} junta cota da lei e AC_PCD sem o candidato concorrer como pessoa com deficiência");
    }

    private static IEnumerable<Dictionary<string, FatoResolvido>> TodasAsCombinacoesDeRespostas()
    {
        for (int bits = 0; bits < 1 << FatosBooleanos.Length; bits++)
        {
            foreach (string corOuRaca in CoresOuRacas)
            {
                Dictionary<string, FatoResolvido> candidato = Candidato(("COR_RACA", corOuRaca));
                for (int i = 0; i < FatosBooleanos.Length; i++)
                {
                    candidato[FatosBooleanos[i]] = Valor((bits & (1 << i)) != 0);
                }

                yield return candidato;
            }
        }
    }

    private static bool EhVerdadeiro(Dictionary<string, FatoResolvido> candidato, string fato) =>
        candidato[fato].Valor is { ValueKind: JsonValueKind.True };

    private static IReadOnlySet<string> Derivar(Dictionary<string, FatoResolvido> candidato)
    {
        ResultadoDerivacao opcao = MotorDerivacao.Derivar(Opcao, candidato);
        candidato[OpcaoPelasCotasDaLei.Fato] = Valor(opcao.ValorBooleano!.Value);

        ResultadoDerivacao modalidades = MotorDerivacao.Derivar(Modalidades, candidato);
        modalidades.Estado.Should().Be(EstadoFato.Resolvido);
        return modalidades.Valores;
    }

    /// <summary>As respostas dadas; o que não foi dito é falso, e a cor ou raça, branca.</summary>
    private static Dictionary<string, FatoResolvido> Candidato(params (string Fato, object Valor)[] respostas)
    {
        Dictionary<string, FatoResolvido> candidato = new(StringComparer.Ordinal) { ["COR_RACA"] = Valor("BRANCA") };
        foreach (string fato in FatosBooleanos)
        {
            candidato[fato] = Valor(false);
        }

        foreach ((string fato, object valor) in respostas)
        {
            candidato[fato] = Valor(valor);
        }

        return candidato;
    }

    private static FatoResolvido Valor(object valor) => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(valor));
}
