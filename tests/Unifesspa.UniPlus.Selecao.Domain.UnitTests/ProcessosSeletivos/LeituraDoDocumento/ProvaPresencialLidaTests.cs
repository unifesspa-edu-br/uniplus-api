namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

using Xunit;

/// <summary>
/// A prova presencial do documento passa pelas regras da declaração e pela conferência da publicação: o bloco ausente é o processo por
/// declarar, a declaração recusada só devolve as causas da leitura e não vai ao Geo, e cada causa aponta o item pela posição enviada.
/// </summary>
public sealed class ProvaPresencialLidaTests
{
    private const int Limite = 100;
    private const string Marabá = "1504208";
    private const string Xinguara = "1508159";
    private const string SaoPaulo = "3550308";

    private static readonly MunicipiosConferidosNoGeo GeoConhecendoTodos = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
    {
        [Marabá] = ResultadoDoMunicipioNoGeo.Conferido("Marabá"),
        [Xinguara] = ResultadoDoMunicipioNoGeo.Conferido("Xinguara"),
        [SaoPaulo] = ResultadoDoMunicipioNoGeo.Conferido("São Paulo"),
    });

    private static MunicipioDeProvaInformado Municipio(string? codigo, string? uf) => new(codigo, uf);

    private static ProvaPresencialLida Lida(bool temProva, int limite = Limite, params MunicipioDeProvaInformado[] municipios) =>
        ProvaPresencialLida.Ler(new ProvaPresencialNoDocumento(temProva, municipios), limite);

    private static (string? Ponteiro, string Codigo)[] Pares(IEnumerable<FieldError> causas) =>
        [.. causas.Select(static causa => (causa.Field, causa.Error.Code))];

    [Fact(DisplayName = "O documento sem o bloco da prova presencial é o processo por declarar, sem recusa da leitura")]
    public void BlocoAusente_EhPorDeclarar()
    {
        ProvaPresencialLida lida = ProvaPresencialLida.Ler(null, Limite);

        lida.Declaracao.Should().BeNull();
        lida.Origem.PartesRecusadas.Should().BeEmpty();
        Pares(lida.Causas(GeoConhecendoTodos)).Should().Equal(("/provaPresencial", ProvaPresencialErrorCodes.PorDeclarar));
    }

    [Fact(DisplayName = "O processo sem prova presencial e sem município não tem causa")]
    public void SemProvaESemMunicipio_NaoTemCausa()
    {
        ProvaPresencialLida lida = Lida(temProva: false);

        lida.Declaracao!.TemProvaPresencial.Should().BeFalse();
        lida.Causas(MunicipiosConferidosNoGeo.Nenhum).Should().BeEmpty();
    }

    [Fact(DisplayName = "O processo com prova presencial e sem município fica com a declaração e a causa de município faltando, apontada na lista")]
    public void ComProvaESemMunicipio_ApontaALista()
    {
        ProvaPresencialLida lida = Lida(temProva: true);

        lida.Declaracao!.Municipios.Should().BeEmpty();
        lida.Origem.PartesRecusadas.Should().BeEmpty();
        Pares(lida.Causas(MunicipiosConferidosNoGeo.Nenhum)).Should().Equal(("/provaPresencial/municipios", ProvaPresencialErrorCodes.SemMunicipio));
    }

    [Fact(DisplayName = "Os municípios que o Geo conhece, de qualquer UF, ficam na ordem enviada e sem causa")]
    public void MunicipiosConhecidosNoGeo_NaoTemCausa()
    {
        ProvaPresencialLida lida = Lida(true, Limite, Municipio(SaoPaulo, "SP"), Municipio(Marabá, "PA"));

        lida.Declaracao!.Municipios.Select(static municipio => municipio.CodigoIbge).Should().Equal(SaoPaulo, Marabá);
        lida.Causas(GeoConhecendoTodos).Should().BeEmpty();
    }

    [Fact(DisplayName = "O município que o Geo não conhece é apontado pela posição enviada, sem apontar os que ele conhece")]
    public void MunicipioQueOGeoNaoConhece_ApontaOCodigoDaPosicao()
    {
        MunicipiosConferidosNoGeo geoSemXinguara = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            [Marabá] = ResultadoDoMunicipioNoGeo.Conferido("Marabá"),
            [SaoPaulo] = ResultadoDoMunicipioNoGeo.Conferido("São Paulo"),
        });

        ProvaPresencialLida lida = Lida(true, Limite, Municipio(Marabá, "PA"), Municipio(SaoPaulo, "SP"), Municipio(Xinguara, "PA"));

        Pares(lida.Causas(geoSemXinguara)).Should().Equal(("/provaPresencial/municipios/2/codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo));
    }

    [Fact(DisplayName = "O Geo que não confirmou nenhum município aponta cada um pela posição, na ordem enviada")]
    public void GeoSemNenhumMunicipio_ApontaTodos()
    {
        ProvaPresencialLida lida = Lida(true, Limite, Municipio(Marabá, "PA"), Municipio(Xinguara, "PA"));

        Pares(lida.Causas(MunicipiosConferidosNoGeo.Nenhum)).Should().Equal(
            ("/provaPresencial/municipios/0/codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo),
            ("/provaPresencial/municipios/1/codigoIbge", ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo));
    }

    [Fact(DisplayName = "O município repetido é apontado na posição da repetição e a declaração fica recusada, sem ir ao Geo")]
    public void MunicipioRepetido_RecusaALeituraENaoVaiAoGeo()
    {
        ProvaPresencialLida lida = Lida(true, Limite, Municipio(Marabá, "PA"), Municipio(Xinguara, "PA"), Municipio(Marabá, "PA"));

        lida.Declaracao.Should().BeNull();
        Pares(lida.Causas(MunicipiosConferidosNoGeo.Nenhum)).Should().Equal(("/provaPresencial/municipios/2/codigoIbge", ProvaPresencialErrorCodes.MunicipioRepetido));
    }

    [Theory(DisplayName = "A UF ausente ou incoerente com o prefixo do código é apontada no campo da UF do município")]
    [InlineData(null, CidadeReferenciaErrorCodes.UfObrigatoria)]
    [InlineData("SP", CidadeReferenciaErrorCodes.UfIncoerente)]
    [InlineData("pa", CidadeReferenciaErrorCodes.UfIncoerente)]
    public void Uf_AusenteOuIncoerente_ApontaOCampoDaUf(string? uf, string esperado)
    {
        ProvaPresencialLida lida = Lida(true, Limite, Municipio(Marabá, "PA"), Municipio(Xinguara, uf));

        lida.Declaracao.Should().BeNull();
        Pares(lida.Causas(GeoConhecendoTodos)).Should().Equal(("/provaPresencial/municipios/1/uf", esperado));
    }

    [Theory(DisplayName = "O código ausente ou fora da forma de município é apontado no campo do código, sem repetir o texto enviado")]
    [InlineData(null, CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio)]
    [InlineData("", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio)]
    [InlineData("15042", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido)]
    [InlineData("15O4208", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido)]
    [InlineData("9904208", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido)]
    public void Codigo_AusenteOuForaDaForma_ApontaOCampoDoCodigo(string? codigo, string esperado)
    {
        ProvaPresencialLida lida = Lida(true, Limite, Municipio(codigo, "PA"));

        lida.Declaracao.Should().BeNull();
        FieldError causa = lida.Causas(GeoConhecendoTodos).Should().ContainSingle().Subject;
        (causa.Field, causa.Error.Code).Should().Be(("/provaPresencial/municipios/0/codigoIbge", esperado));
        if (!string.IsNullOrEmpty(codigo))
        {
            causa.Error.Message.Should().NotContain(codigo);
        }
    }

    [Fact(DisplayName = "O processo sem prova presencial que declara município é recusado na lista, antes das causas de cada município")]
    public void SemProvaComMunicipio_RecusaALista()
    {
        ProvaPresencialLida lida = Lida(false, Limite, Municipio(Marabá, "SP"));

        Pares(lida.Causas(GeoConhecendoTodos)).Should().Equal(
            ("/provaPresencial/municipios", ProvaPresencialErrorCodes.MunicipiosSemProvaPresencial),
            ("/provaPresencial/municipios/0/uf", CidadeReferenciaErrorCodes.UfIncoerente));
    }

    [Fact(DisplayName = "A lista com mais municípios que o limite fica recusada, e a causa do limite vem por último")]
    public void ListaAcimaDoLimite_RecusaEApontaPorUltimo()
    {
        ProvaPresencialLida lida = Lida(true, 2, Municipio(Marabá, "SP"), Municipio(Xinguara, "PA"), Municipio(SaoPaulo, "SP"));

        lida.Declaracao.Should().BeNull();
        Pares(lida.Causas(MunicipiosConferidosNoGeo.Nenhum)).Should().Equal(
            ("/provaPresencial/municipios/0/uf", CidadeReferenciaErrorCodes.UfIncoerente),
            ("/provaPresencial/municipios", ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite));
    }

    [Fact(DisplayName = "A lista que cabe exatamente no limite é aceita e vai ao Geo")]
    public void ListaNoLimite_EhAceita()
    {
        ProvaPresencialLida lida = Lida(true, 2, Municipio(Marabá, "PA"), Municipio(Xinguara, "PA"));

        lida.Declaracao!.Municipios.Should().HaveCount(2);
        lida.Causas(GeoConhecendoTodos).Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas se acumulam na ordem do documento: item a item, com o limite por último")]
    public void Recusas_AcumulamNaOrdemDoDocumento()
    {
        ProvaPresencialLida lida = Lida(
            true, 2, Municipio(Marabá, "SP"), Municipio("123", "PA"), Municipio(Marabá, "PA"), Municipio(Marabá, "PA"));

        Pares(lida.Causas(GeoConhecendoTodos)).Should().Equal(
            ("/provaPresencial/municipios/0/uf", CidadeReferenciaErrorCodes.UfIncoerente),
            ("/provaPresencial/municipios/1/codigoIbge", CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido),
            ("/provaPresencial/municipios/2/codigoIbge", ProvaPresencialErrorCodes.MunicipioRepetido),
            ("/provaPresencial/municipios/3/codigoIbge", ProvaPresencialErrorCodes.MunicipioRepetido),
            ("/provaPresencial/municipios", ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite));
    }

    [Fact(DisplayName = "A leitura recusada guarda cada causa como parte recusada do bloco, no ponteiro do documento")]
    public void LeituraRecusada_GuardaAsPartesRecusadasDoBloco()
    {
        ProvaPresencialLida lida = Lida(true, Limite, Municipio(Marabá, "SP"));

        lida.Origem.PartesRecusadas.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Caminho = DeclaracaoDaProvaPresencial.Campo,
            Causa = new { Field = "/provaPresencial/municipios/0/uf" },
        });
        lida.Origem.TemParteRecusadaEm(DeclaracaoDaProvaPresencial.Campo).Should().BeTrue();
    }

    [Fact(DisplayName = "O conjunto de municípios do Geo nulo é defeito de quem chama")]
    public void GeoNulo_Lanca()
    {
        Action causas = () => Lida(true).Causas(null!);

        causas.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Os municípios nulos são defeito de quem chama")]
    public void MunicipiosNulos_Lanca()
    {
        Action ler = () => ProvaPresencialLida.Ler(new ProvaPresencialNoDocumento(true, null!), Limite);

        ler.Should().Throw<ArgumentNullException>();
    }
}
