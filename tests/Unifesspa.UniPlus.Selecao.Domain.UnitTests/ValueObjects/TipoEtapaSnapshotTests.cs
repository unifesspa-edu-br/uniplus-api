namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class TipoEtapaSnapshotTests
{
    private static readonly Guid OrigemId = Guid.CreateVersion7();

    [Fact(DisplayName = "Criar com dados válidos tem sucesso")]
    public void Criar_Valida_Sucesso()
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.OrigemId.Should().Be(OrigemId);
        resultado.Value!.Codigo.Should().Be("PROVA_OBJETIVA");
        resultado.Value!.Nome.Should().Be("Prova Objetiva");
    }

    [Theory(DisplayName = "Criar com espaços nas bordas remove-os (Trim)")]
    [InlineData(" PROVA_OBJETIVA ", "PROVA_OBJETIVA")]
    public void Criar_ComEspacos_Trima(string entrada, string esperado)
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, entrada, "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Codigo.Should().Be(esperado);
    }

    [Fact(DisplayName = "Criar normaliza para NFC — forma decomposta e forma composta do mesmo texto congelam para o mesmo código")]
    public void Criar_ComFormaDecomposta_NormalizaParaNfc()
    {
        // Mesmo texto, duas representações Unicode do "O" acentuado: NFC (Ó, um único
        // code point) e NFD ('O' base + COMBINING ACUTE ACCENT) — bytes diferentes,
        // mesmo significado. A forma NFD vem de Normalize(FormD) em runtime, não de
        // caractere literal no fonte — assim o teste não depende de qual normalização o
        // editor/terminal aplicaria ao salvar o arquivo-fonte. Sem normalizar em
        // TipoEtapaSnapshot.Criar, o código congelado mudaria de representação entre um
        // snapshot criado a partir de um e de outro, e um ciclo de retificação descartada
        // (que restaura via envelope, sempre NFC) quebraria a comparação ordinal em
        // AvaliadorConformidadeLegal mesmo sem o dado ter mudado.
        string codigoComposto = "CÓDIGO_ACENTUADO";
        string codigoDecomposto = codigoComposto.Normalize(System.Text.NormalizationForm.FormD);
        codigoDecomposto.Should().NotBe(codigoComposto, "pré-condição do teste: as duas formas têm bytes diferentes");

        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, codigoDecomposto, "Banca", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Codigo.Should().Be(codigoComposto);
    }

    [Fact(DisplayName = "Criar com OrigemId vazio falha")]
    public void Criar_OrigemIdVazio_Falha()
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(Guid.Empty, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.OrigemIdObrigatorio");
    }

    [Fact(DisplayName = "Criar com código vazio falha")]
    public void Criar_CodigoVazio_Falha()
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, "", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.CodigoObrigatorio");
    }

    [Fact(DisplayName = "Criar com nome vazio falha")]
    public void Criar_NomeVazio_Falha()
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, "PROVA_OBJETIVA", "", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.NomeObrigatorio");
    }

    [Fact(DisplayName = "Criar com caractere nulo no codigo falha")]
    public void Criar_CodigoComCaractereNulo_Falha()
    {
        string codigoComNulo = "PROVA" + '\0' + "OBJETIVA";

        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, codigoComNulo, "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.CaractereNulo");
    }

    [Fact(DisplayName = "Criar com caractere nulo no nome falha")]
    public void Criar_NomeComCaractereNulo_Falha()
    {
        string nomeComNulo = "Prova" + '\0' + "Objetiva";

        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, "PROVA_OBJETIVA", nomeComNulo, admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.CaractereNulo");
    }

    [Fact(DisplayName = "Criar com código acima de 64 caracteres falha")]
    public void Criar_CodigoAcimaDoLimite_Falha()
    {
        string codigoLongo = new('A', 65);

        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, codigoLongo, "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.TamanhoInvalido");
    }

    [Fact(DisplayName = "Criar com nome acima de 200 caracteres falha")]
    public void Criar_NomeAcimaDoLimite_Falha()
    {
        string nomeLongo = new('A', 201);

        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, "PROVA_OBJETIVA", nomeLongo, admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.TamanhoInvalido");
    }

    [Fact(DisplayName = "ToString devolve o codigo")]
    public void ToString_DevolveCodigo()
    {
        TipoEtapaSnapshot snapshot = TipoEtapaSnapshot.Criar(OrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

        snapshot.ToString().Should().Be("PROVA_OBJETIVA");
    }

    [Theory(DisplayName = "Criar congela cada sinalizador no próprio campo")]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Criar_CongelaOsSinalizadores(bool admitePontuacao, bool admiteEliminacao)
    {
        TipoEtapaSnapshot snapshot = TipoEtapaSnapshot.Criar(
            OrigemId, "ENTREVISTA", "Entrevista", admitePontuacao, admiteEliminacao, notaDeOrigemNoEnem: false).Value!;

        snapshot.AdmitePontuacao.Should().Be(admitePontuacao);
        snapshot.AdmiteEliminacao.Should().Be(admiteEliminacao);
    }

    [Fact(DisplayName = "Criar com tipo que não pontua nem elimina falha, como no cadastro")]
    public void Criar_SemCaraterAdmitido_Falha()
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(
            OrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: false, admiteEliminacao: false, notaDeOrigemNoEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.SemCaraterAdmitido");
    }

    [Fact(DisplayName = "ComSinalizadores troca só os sinalizadores e mantém a identidade congelada")]
    public void ComSinalizadores_MantemIdentidade()
    {
        TipoEtapaSnapshot congelado = TipoEtapaSnapshot.Criar(
            OrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: false, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

        TipoEtapaSnapshot relido = congelado.ComSinalizadores(admitePontuacao: true, admiteEliminacao: false);

        relido.Should().NotBeSameAs(congelado, "o snapshot é owned type: cada etapa precisa da própria instância");
        relido.OrigemId.Should().Be(congelado.OrigemId);
        relido.Codigo.Should().Be(congelado.Codigo);
        relido.Nome.Should().Be(congelado.Nome);
        relido.AdmitePontuacao.Should().BeTrue();
        relido.AdmiteEliminacao.Should().BeFalse();
    }

    [Fact(DisplayName = "ComSinalizadores sem caráter admitido lança, porque a vista do cadastro nunca o traz")]
    public void ComSinalizadores_SemCaraterAdmitido_Lanca()
    {
        TipoEtapaSnapshot congelado = TipoEtapaSnapshot.Criar(
            OrigemId, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

        Action refrescar = () => congelado.ComSinalizadores(admitePontuacao: false, admiteEliminacao: false);

        refrescar.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Criar congela a origem da nota no ENEM, e sem ela declarada o tipo não é do ENEM")]
    [InlineData(true)]
    [InlineData(false)]
    public void Criar_CongelaANotaDeOrigemNoEnem(bool notaDeOrigemNoEnem)
    {
        TipoEtapaSnapshot snapshot = TipoEtapaSnapshot.Criar(
            OrigemId, "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem).Value!;

        snapshot.NotaDeOrigemNoEnem.Should().Be(notaDeOrigemNoEnem, "é o atributo, e não o código, que declara a origem");
    }

    [Fact(DisplayName = "Criar com nota de origem no ENEM sem pontuação falha, como no cadastro")]
    public void Criar_NotaDeOrigemNoEnemSemPontuacao_Falha()
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(
            OrigemId, "NOTA_ENEM", "Nota do ENEM", admitePontuacao: false, admiteEliminacao: true, notaDeOrigemNoEnem: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.NotaDeOrigemNoEnemSemPontuacao");
    }

    [Fact(DisplayName = "ComSinalizadores preserva a origem da nota no ENEM, que é identidade")]
    public void ComSinalizadores_PreservaANotaDeOrigemNoEnem()
    {
        TipoEtapaSnapshot congelado = TipoEtapaSnapshot.Criar(
            OrigemId, "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!;

        TipoEtapaSnapshot relido = congelado.ComSinalizadores(admitePontuacao: true, admiteEliminacao: false);

        relido.NotaDeOrigemNoEnem.Should().BeTrue();
    }

    [Fact(DisplayName = "ComSinalizadores sem pontuação num tipo com nota do ENEM lança, porque o cadastro nunca o traz")]
    public void ComSinalizadores_NotaDeOrigemNoEnemSemPontuacao_Lanca()
    {
        TipoEtapaSnapshot congelado = TipoEtapaSnapshot.Criar(
            OrigemId, "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!;

        Action refrescar = () => congelado.ComSinalizadores(admitePontuacao: false, admiteEliminacao: true);

        refrescar.Should().Throw<ArgumentException>();
    }
}
