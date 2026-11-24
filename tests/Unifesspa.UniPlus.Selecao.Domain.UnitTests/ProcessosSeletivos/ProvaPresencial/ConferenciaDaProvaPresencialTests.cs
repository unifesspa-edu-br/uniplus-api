namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ProvaPresencial;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// O que a publicação exige da declaração de prova presencial (UNI-REQ-0407, ADR-0090): a declaração
/// existe, o processo com prova presencial tem município, e todo município foi conferido no Geo. O
/// Município desconhecido nunca vale como conferido.
/// </summary>
public sealed class ConferenciaDaProvaPresencialTests
{
    private const string Marabá = "1504208";
    private const string Xinguara = "1508159";

    private static DeclaracaoDaProvaPresencial ComProva(params string[] codigos) => DeclaracaoDaProvaPresencial.Declarar(
        temProvaPresencial: true, [.. codigos.Select(static codigo => new MunicipioDeProvaInformado(codigo, ReferenciaCidadeGeo.UfDoCodigoIbge(codigo)))], limiteDeMunicipios: 100).Value!;

    [Fact(DisplayName = "Sem declaração, o processo está por declarar")]
    public void SemDeclaracao_PorDeclarar() =>
        ConferenciaDaProvaPresencial.Conferir(null, MunicipiosConferidosNoGeo.Nenhum, 100).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "provaPresencial",
            Error = new { Code = ProvaPresencialErrorCodes.PorDeclarar },
        });

    [Fact(DisplayName = "Sem prova presencial, não há município a conferir")]
    public void SemProva_NadaAConferir() =>
        ConferenciaDaProvaPresencial.Conferir(
            DeclaracaoDaProvaPresencial.Declarar(temProvaPresencial: false, [], 100).Value!, MunicipiosConferidosNoGeo.Nenhum, 100)
            .Should().BeEmpty();

    [Fact(DisplayName = "Com prova presencial e sem município, a publicação é recusada")]
    public void ComProva_SemMunicipio_Recusa() =>
        ConferenciaDaProvaPresencial.Conferir(ComProva(), MunicipiosConferidosNoGeo.Nenhum, 100).Should().ContainSingle().Which.Should().BeEquivalentTo(new
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

        ConferenciaDaProvaPresencial.Conferir(ComProva(Marabá, Xinguara), conferidos, 100).Should().BeEmpty();
    }

    [Fact(DisplayName = "O município que o Geo recusa leva o código da prova presencial, na posição dele")]
    public void MunicipioRecusadoNoGeo_LevaOCodigoDaProva()
    {
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            [Marabá] = ResultadoDoMunicipioNoGeo.Conferido("Marabá"),
            [Xinguara] = ResultadoDoMunicipioNoGeo.NaoConferido,
        });

        ConferenciaDaProvaPresencial.Conferir(ComProva(Marabá, Xinguara), conferidos, 100).Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "provaPresencial.municipios[1].codigoIbge",
            Error = new { Code = ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo },
        });
    }

    [Fact(DisplayName = "Cada município desconhecido tem uma causa na posição correspondente")]
    public void MunicipiosDesconhecidos_DuasPosicoes_DuasCausasEmOrdem()
    {
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            [Xinguara] = ResultadoDoMunicipioNoGeo.Conferido("Xinguara"),
        });
        IReadOnlyList<FieldError> recusas = ConferenciaDaProvaPresencial.Conferir(ComProva(Marabá, Xinguara, "3550308"), conferidos, 100);
        recusas.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("provaPresencial.municipios[0].codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo),
            ("provaPresencial.municipios[2].codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo));
        recusas[0].Error.Message.Should().Be("O Geo não confirmou o município de prova: confira o código do IBGE e a UF informados.");
    }

    [Fact(DisplayName = "Município desconhecido e UF divergente são recusados pela posição")]
    public void DesconhecidoERecusado_AcumulamEmOrdem()
    {
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            [Xinguara] = ResultadoDoMunicipioNoGeo.NaoConferido,
        });
        IReadOnlyList<FieldError> recusas = ConferenciaDaProvaPresencial.Conferir(ComProva(Marabá, Xinguara), conferidos, 100);
        recusas.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("provaPresencial.municipios[0].codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo),
            ("provaPresencial.municipios[1].codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo));
        recusas.Count.Should().NotBe(0);
    }

    [Fact]
    public void Conferir_LimiteReduzido_RecusaSomenteAcimaDoLimite()
    {
        DeclaracaoDaProvaPresencial declaracao = ComProva(Marabá, Xinguara);
        MunicipiosConferidosNoGeo conferidos = Conjunto((Marabá, "Marabá"), (Xinguara, "Xinguara"));
        ConferenciaDaProvaPresencial.Conferir(declaracao, conferidos, 2).Should().BeEmpty();
        ConferenciaDaProvaPresencial.Conferir(declaracao, conferidos, 1)
            .Should().ContainSingle().Which.Error.Code.Should().Be(ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite);
    }

    [Fact]
    public void Conferir_AcimaDoLimiteComMunicipioNaoConferido_AcumulaARecusaDoMunicipioEADoLimite()
    {
        DeclaracaoDaProvaPresencial declaracao = ComProva(Marabá, Xinguara);
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            [Marabá] = ResultadoDoMunicipioNoGeo.NaoConferido,
            [Xinguara] = ResultadoDoMunicipioNoGeo.Conferido("Xinguara"),
        });
        ConferenciaDaProvaPresencial.Conferir(declaracao, conferidos, 1).Select(static recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("provaPresencial.municipios[0].codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo),
            ("provaPresencial.municipios", ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite));
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

    [Theory(DisplayName = "O município conferido sem nome não existe")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResultadoSemConteudo_NaoExiste(string? nome)
    {
        Action conferidoSemNome = () => ResultadoDoMunicipioNoGeo.Conferido(nome!);

        conferidoSemNome.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A conferência não muda quando quem a forneceu altera o dicionário depois")]
    public void Conferencia_NaoMudaComAAlteracaoDoDicionarioDeOrigem()
    {
        Dictionary<string, ResultadoDoMunicipioNoGeo> origem = new() { [Marabá] = ResultadoDoMunicipioNoGeo.Conferido("Marabá") };
        MunicipiosConferidosNoGeo conferidos = MunicipiosConferidosNoGeo.De(origem);

        origem[Marabá] = ResultadoDoMunicipioNoGeo.NaoConferido;
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
