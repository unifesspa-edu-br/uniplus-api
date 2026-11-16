namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ProvaPresencial;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// O que a publicação exige da declaração de prova presencial (UNI-REQ-0407, ADR-0090): a declaração
/// existe, o processo com prova presencial tem município, e todo município foi conferido no Geo. O
/// Geo fora do ar é uma só causa, e nunca vale como município conferido.
/// </summary>
public sealed class ConferenciaDaProvaPresencialTests
{
    private const string Marabá = "1504208";
    private const string Xinguara = "1508159";

    private static DeclaracaoDaProvaPresencial ComProva(params string[] codigos) => DeclaracaoDaProvaPresencial.Declarar(
        temProvaPresencial: true, [.. codigos.Select(static codigo => new MunicipioDeAplicacaoInformado(codigo, ReferenciaCidadeGeo.UfDoCodigoIbge(codigo)))], limiteDeMunicipios: 100).Value!;

    private static ResultadoDoMunicipioNoGeo Recusado(string codigo) => ResultadoDoMunicipioNoGeo.Recusado(
        new DomainError(codigo, "O Geo não confirma o município."));

    [Fact(DisplayName = "Sem declaração, o processo está por declarar")]
    public void SemDeclaracao_PorDeclarar() =>
        ConferenciaDaProvaPresencial.Conferir(null, MunicipiosConferidosNoGeo.Nenhum).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "provaPresencial",
            Error = new { Code = ProvaPresencialErrorCodes.PorDeclarar },
        });

    [Fact(DisplayName = "Sem prova presencial, não há município a conferir")]
    public void SemProva_NadaAConferir() =>
        ConferenciaDaProvaPresencial.Conferir(
            DeclaracaoDaProvaPresencial.Declarar(temProvaPresencial: false, [], 100).Value!, MunicipiosConferidosNoGeo.Nenhum)
            .Should().BeEmpty();

    [Fact(DisplayName = "Com prova presencial e sem município, a publicação é recusada")]
    public void ComProva_SemMunicipio_Recusa() =>
        ConferenciaDaProvaPresencial.Conferir(ComProva(), MunicipiosConferidosNoGeo.Nenhum).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "provaPresencial.municipios",
            Error = new { Code = ProvaPresencialErrorCodes.SemMunicipio },
        });

    [Fact(DisplayName = "Todo município conferido no Geo, de qualquer UF, é aceito")]
    public void TodosConferidos_Aceita()
    {
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            [Marabá] = ResultadoDoMunicipioNoGeo.Conferido("Marabá"),
            [Xinguara] = ResultadoDoMunicipioNoGeo.Conferido("Xinguara"),
        });

        ConferenciaDaProvaPresencial.Conferir(ComProva(Marabá, Xinguara), conferidos).Should().BeEmpty();
    }

    [Fact(DisplayName = "O município que o Geo recusa leva o erro do Geo, na posição dele")]
    public void MunicipioRecusadoNoGeo_LevaOErroDoGeo()
    {
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            [Marabá] = ResultadoDoMunicipioNoGeo.Conferido("Marabá"),
            [Xinguara] = Recusado("ConferenciaEnderecoGeo.MunicipioNaoEncontrado"),
        });

        ConferenciaDaProvaPresencial.Conferir(ComProva(Marabá, Xinguara), conferidos).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "provaPresencial.municipios[1].codigoIbge",
            Error = new { Code = "ConferenciaEnderecoGeo.MunicipioNaoEncontrado" },
        });
    }

    [Fact(DisplayName = "Com o Geo fora do ar, os municípios não conferidos são uma só causa, nunca aceitos")]
    public void GeoForaDoAr_UmaSoCausa()
    {
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            [Marabá] = ResultadoDoMunicipioNoGeo.Conferido("Marabá"),
        });

        ConferenciaDaProvaPresencial.Conferir(ComProva(Marabá, Xinguara, "3550308"), conferidos).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "provaPresencial.municipios",
            Error = new { Code = ProvaPresencialErrorCodes.MunicipiosNaoConferidosNoGeo },
        });
    }

    [Fact(DisplayName = "O município recusado e o não conferido se acumulam, cada um com a sua causa")]
    public void RecusadoENaoConferido_Acumulam()
    {
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            [Marabá] = Recusado("ConferenciaEnderecoGeo.MunicipioDivergente"),
        });

        ConferenciaDaProvaPresencial.Conferir(ComProva(Marabá, Xinguara), conferidos).Select(static erro => erro.Error.Code)
            .Should().BeEquivalentTo(["ConferenciaEnderecoGeo.MunicipioDivergente", ProvaPresencialErrorCodes.MunicipiosNaoConferidosNoGeo]);
    }

    [Fact(DisplayName = "O nome que o Geo devolveu fica disponível para o congelamento")]
    public void NomeDoGeo_FicaDisponivel()
    {
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            [Marabá] = ResultadoDoMunicipioNoGeo.Conferido("Marabá"),
        });

        conferidos.NomeConferido(Marabá).Should().Be("Marabá");
        conferidos.NomeConferido(Xinguara).Should().BeNull();
    }

    [Theory(DisplayName = "O município conferido sem nome, e o recusado sem erro, não existem")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResultadoSemConteudo_NaoExiste(string? nome)
    {
        Action conferidoSemNome = () => ResultadoDoMunicipioNoGeo.Conferido(nome!);
        Action recusadoSemErro = () => ResultadoDoMunicipioNoGeo.Recusado(null!);

        conferidoSemNome.Should().Throw<ArgumentException>();
        recusadoSemErro.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "A conferência não muda quando quem a forneceu altera o dicionário depois")]
    public void Conferencia_NaoMudaComAAlteracaoDoDicionarioDeOrigem()
    {
        Dictionary<string, ResultadoDoMunicipioNoGeo> origem = new() { [Marabá] = ResultadoDoMunicipioNoGeo.Conferido("Marabá") };
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(origem);

        origem[Marabá] = Recusado("ConferenciaEnderecoGeo.MunicipioNaoEncontrado");
        origem[Xinguara] = ResultadoDoMunicipioNoGeo.Conferido("Xinguara");

        conferidos.NomeConferido(Marabá).Should().Be("Marabá");
        conferidos.NomeConferido(Xinguara).Should().BeNull();
    }

    [Fact(DisplayName = "Dois conjuntos de municípios conferidos são iguais quando trazem o mesmo resultado para os mesmos municípios")]
    public void Igualdade_PeloResultadoDeCadaMunicipio()
    {
        MunicipiosConferidosNoGeo Conjunto(string nome) => MunicipiosConferidosNoGeo.De(
            new Dictionary<string, ResultadoDoMunicipioNoGeo> { [Marabá] = ResultadoDoMunicipioNoGeo.Conferido(nome) });

        Conjunto("Marabá").Should().Be(Conjunto("Marabá"));
        Conjunto("Marabá").Should().NotBe(Conjunto("Outro nome"));
        Conjunto("Marabá").Should().NotBe(MunicipiosConferidosNoGeo.Nenhum);
    }

    [Fact(DisplayName = "A igualdade dos conjuntos conferidos é simétrica, não depende da ordem e distingue municípios diferentes")]
    public void Igualdade_SimetricaSemOrdemENuloENaoEIgualAOutroMunicipio()
    {
        MunicipiosConferidosNoGeo umDoisOrdemA = Conjunto((Marabá, "Marabá"), (Xinguara, "Xinguara"));
        MunicipiosConferidosNoGeo umDoisOrdemB = Conjunto((Xinguara, "Xinguara"), (Marabá, "Marabá"));

        umDoisOrdemA.Should().Be(umDoisOrdemB);
        umDoisOrdemB.Should().Be(umDoisOrdemA);
        umDoisOrdemA.GetHashCode().Should().Be(umDoisOrdemB.GetHashCode());
        umDoisOrdemA.Should().NotBe(Conjunto((Marabá, "Marabá"), ("3550308", "Xinguara")));
        umDoisOrdemA.Equals(null).Should().BeFalse();
    }

    private static MunicipiosConferidosNoGeo Conjunto(params (string Codigo, string Nome)[] municipios) =>
        MunicipiosConferidosNoGeo.De(municipios.ToDictionary(
            static municipio => municipio.Codigo, static municipio => ResultadoDoMunicipioNoGeo.Conferido(municipio.Nome)));

    [Fact(DisplayName = "O conjunto conferido recusa resultado nulo e a cópia com with mantém o resultado de cada município")]
    public void ResultadoNulo_Recusa_ECopiaComWithMantemOsResultados()
    {
        Action comResultadoNulo = () => MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo> { [Marabá] = null! });
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(
            new Dictionary<string, ResultadoDoMunicipioNoGeo> { [Marabá] = ResultadoDoMunicipioNoGeo.Conferido("Marabá") });

        MunicipiosConferidosNoGeo copia = conferidos with { };

        comResultadoNulo.Should().Throw<ArgumentException>();
        copia.Should().Be(conferidos);
        copia.NomeConferido(Marabá).Should().Be("Marabá");
    }
}
