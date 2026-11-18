namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>
/// A versão congela por valor os leiautes que a declaração cita (UNI-REQ-0084): o que o catálogo muda depois
/// não altera a cópia, e o leiaute que o catálogo não tem não entra.
/// </summary>
public sealed class CopiaDosLeiautesDeclaradosTests
{
    [Fact(DisplayName = "Copia só os leiautes declarados, na ordem da declaração, com os dados do arquivo")]
    public void CopiaOsLeiautesDeclarados()
    {
        LeiauteDoRelatorio regular = LeiauteDeTeste.Criar("CHAMADA_REGULAR", LeiauteDeTeste.EmTodaLinha("DATA_NASCIMENTO"));
        LeiauteDoRelatorio espera = LeiauteDeTeste.Criar("LISTA_DE_ESPERA", LeiauteDeTeste.Opcional("ENDERECO"));
        LeiauteDoRelatorio naoDeclarado = LeiauteDeTeste.Criar("OUTRO");

        IReadOnlyList<LeiauteDoRelatorio> copia = CopiaDosLeiautesDeclarados.Copiar(
            LeiauteDeTeste.Declarar(null, "LISTA_DE_ESPERA", "CHAMADA_REGULAR"), [regular, espera, naoDeclarado]);

        copia.Should().Equal(espera, regular);
    }

    [Fact(DisplayName = "Mudar o dado do leiaute no catálogo depois da cópia não muda a versão congelada")]
    public void MudarOCatalogoDepois_NaoMudaACopia()
    {
        List<DadoDoArquivo> dados = [LeiauteDeTeste.EmTodaLinha("DATA_NASCIMENTO")];
        LeiauteDoRelatorio doCatalogo = new("CHAMADA_REGULAR", "v1", "hash", dados);

        IReadOnlyList<LeiauteDoRelatorio> copia = CopiaDosLeiautesDeclarados.Copiar(LeiauteDeTeste.Declarar(null, "CHAMADA_REGULAR"), [doCatalogo]);
        dados.Add(LeiauteDeTeste.EmTodaLinha("COR_RACA"));

        copia.Single().Dados.Should().Equal(LeiauteDeTeste.EmTodaLinha("DATA_NASCIMENTO"));
    }

    [Fact(DisplayName = "A versão do leiaute declarada é a que se copia; a outra versão do mesmo código não entra")]
    public void CopiaASemVersaoDeclarada()
    {
        LeiauteDoRelatorio v1 = LeiauteDeTeste.Criar("CHAMADA_REGULAR");
        LeiauteDoRelatorio v2 = v1 with { Versao = "v2", Hash = "outro" };

        IReadOnlyList<LeiauteDoRelatorio> copia = CopiaDosLeiautesDeclarados.Copiar(LeiauteDeTeste.Declarar(null, "CHAMADA_REGULAR"), [v2, v1]);

        copia.Should().Equal(v1);
    }

    [Fact(DisplayName = "O leiaute que o catálogo não tem não entra na cópia")]
    public void LeiauteForaDoCatalogo_NaoEntra() =>
        CopiaDosLeiautesDeclarados.Copiar(LeiauteDeTeste.Declarar(null, "CHAMADA_REGULAR"), []).Should().BeEmpty();

    [Fact(DisplayName = "O leiaute traz o fato só quando a coluna vem em toda linha")]
    public void LeiauteTraz_SoColunaEmTodaLinha()
    {
        LeiauteDoRelatorio leiaute = LeiauteDeTeste.Criar("CHAMADA_REGULAR", LeiauteDeTeste.EmTodaLinha("DATA_NASCIMENTO"), LeiauteDeTeste.Opcional("ENDERECO"));

        leiaute.Traz("DATA_NASCIMENTO").Should().BeTrue();
        leiaute.Traz("ENDERECO").Should().BeFalse("a coluna opcional não conta como dado trazido");
        leiaute.Traz("COR_RACA").Should().BeFalse();
    }
}
