namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CalendarioDiasUteisTests
{
    private static readonly DateOnly DataBase = new(2027, 1, 1);

    private static DiaNaoUtilCriacao Nacional(DateOnly? data = null) =>
        new("NACIONAL", null, null, null, data ?? DataBase, "Confraternização Universal");

    private static DiaNaoUtilCriacao Estadual(DateOnly? data = null, string uf = "PA") =>
        new("ESTADUAL", null, null, null, data ?? DataBase.AddDays(1), "Data magna do estado", uf);

    private static DiaNaoUtilCriacao Municipal(
        DateOnly? data = null,
        string? municipioIbge = "1501402",
        string? municipioNome = "Belém",
        string? municipioUf = "PA") =>
        new(
            "MUNICIPAL",
            municipioIbge,
            municipioNome,
            municipioUf,
            data ?? DataBase.AddDays(2),
            "Aniversário do município");

    private static DiaNaoUtilCriacao Institucional(DateOnly? data = null) =>
        new("INSTITUCIONAL", null, null, null, data ?? DataBase.AddDays(3), "Recesso da Unifesspa");

    // ── Criar ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "Criar com dados válidos e abrangências variadas nasce não vigente")]
    public void Criar_DadosValidos_NasceNaoVigente()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [Nacional(), Estadual(), Municipal(), Institucional()]);

        resultado.IsSuccess.Should().BeTrue();
        CalendarioDiasUteis calendario = resultado.Value!;
        calendario.Id.Should().NotBe(Guid.Empty);
        calendario.VersaoDataset.Should().Be("2027.1");
        calendario.Vigente.Should().BeFalse();
        calendario.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar preenche DiasNaoUteis com a contagem e os campos esperados")]
    public void Criar_DadosValidos_PreencheDiasNaoUteis()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [Nacional(), Municipal()]);

        resultado.IsSuccess.Should().BeTrue();
        CalendarioDiasUteis calendario = resultado.Value!;
        calendario.DiasNaoUteis.Should().HaveCount(2);

        calendario.DiasNaoUteis.Should().Contain(d =>
            d.Abrangencia == Abrangencia.Nacional
            && d.MunicipioIbge == null
            && d.Data == DataBase
            && d.Descricao == "Confraternização Universal");

        calendario.DiasNaoUteis.Should().Contain(d =>
            d.Abrangencia == Abrangencia.Municipal
            && d.MunicipioIbge == "1501402"
            && d.MunicipioNome == "Belém"
            && d.MunicipioUf == "PA"
            && d.Uf == null
            && d.Data == DataBase.AddDays(2)
            && d.Descricao == "Aniversário do município");
    }

    [Fact(DisplayName = "Criar normaliza código, nome e UF do snapshot municipal")]
    public void Criar_SnapshotMunicipalComEspacosENormalizacao_PersisteNormalizado()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1",
            [new DiaNaoUtilCriacao(
                "MUNICIPAL",
                " 1501402 ",
                "  Belém  ",
                " pa ",
                DataBase,
                "Aniversário do município")]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.DiasNaoUteis.Should().Contain(d =>
            d.MunicipioIbge == "1501402"
            && d.MunicipioNome == "Belém"
            && d.MunicipioUf == "PA");
    }

    [Theory(DisplayName = "Criar com versão do dataset ausente ou em branco falha")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_VersaoDatasetVazia_Falha(string versaoDataset)
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(versaoDataset, [Nacional()]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.VersaoDatasetObrigatoria);
    }

    [Fact(DisplayName = "Criar com versão do dataset acima de 60 caracteres falha")]
    public void Criar_VersaoDatasetMuitoLonga_Falha()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(new string('A', 61), [Nacional()]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.VersaoDatasetTamanho);
    }

    [Fact(DisplayName = "Criar sem nenhum dia não útil falha")]
    public void Criar_SemDiaNaoUtil_Falha()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar("2027.1", []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.SemDiaNaoUtil);
    }

    [Fact(DisplayName = "Criar com token de abrangência fora do domínio fechado falha")]
    public void Criar_AbrangenciaInvalida_Falha()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [new DiaNaoUtilCriacao("INVALIDO", null, null, null, DataBase, "Dia qualquer")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.AbrangenciaInvalida);
    }

    [Theory(DisplayName = "Criar com snapshot municipal ausente, parcial ou incoerente falha")]
    [InlineData(null, "Belém", "PA", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio)]
    [InlineData("1501402", null, "PA", CidadeReferenciaErrorCodes.NomeObrigatorio)]
    [InlineData("1501402", "Belém", null, CidadeReferenciaErrorCodes.UfObrigatoria)]
    [InlineData("1501402", "Belém", "SP", CidadeReferenciaErrorCodes.UfIncoerente)]
    public void Criar_SnapshotMunicipalInvalido_Falha(
        string? municipioIbge,
        string? municipioNome,
        string? municipioUf,
        string codigoEsperado)
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [Municipal(municipioIbge: municipioIbge, municipioNome: municipioNome, municipioUf: municipioUf)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigoEsperado);
    }

    [Theory(DisplayName = "Criar com campo do snapshot municipal fora da abrangência municipal falha")]
    [InlineData("NACIONAL", "1501402", null, null)]
    [InlineData("ESTADUAL", null, "Belém", null)]
    [InlineData("INSTITUCIONAL", null, null, "PA")]
    [InlineData("NACIONAL", "1501402", "Belém", "PA")]
    public void Criar_SnapshotMunicipalForaDeMunicipal_Falha(
        string abrangencia,
        string? municipioIbge,
        string? municipioNome,
        string? municipioUf)
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1",
            [new DiaNaoUtilCriacao(
                abrangencia,
                municipioIbge,
                municipioNome,
                municipioUf,
                DataBase,
                "Dia qualquer")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.SnapshotMunicipalApenasParaMunicipal);
    }

    [Fact(DisplayName = "Criar com abrangência estadual sem UF falha")]
    public void Criar_EstadualSemUf_Falha()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [new DiaNaoUtilCriacao("ESTADUAL", null, null, null, DataBase, "Data magna do estado")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.UfObrigatoriaParaEstadual);
    }

    [Theory(DisplayName = "Criar com UF em formato inválido falha")]
    [InlineData("P")]
    [InlineData("PAR")]
    [InlineData("P1")]
    [InlineData("ZZ")]
    public void Criar_UfFormatoInvalido_Falha(string uf)
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [new DiaNaoUtilCriacao("ESTADUAL", null, null, null, DataBase, "Data magna do estado", uf)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.UfFormatoInvalido);
    }

    [Theory(DisplayName = "Criar com UF fora da abrangência estadual falha")]
    [InlineData("NACIONAL")]
    [InlineData("INSTITUCIONAL")]
    public void Criar_UfForaDeEstadual_Falha(string abrangencia)
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [new DiaNaoUtilCriacao(abrangencia, null, null, null, DataBase, "Dia qualquer", "PA")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.UfApenasParaEstadual);
    }

    [Fact(DisplayName = "Criar com UF em minúsculas é normalizada para maiúsculas")]
    public void Criar_UfMinuscula_Normaliza()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [new DiaNaoUtilCriacao("ESTADUAL", null, null, null, DataBase, "Data magna do estado", "pa")]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.DiasNaoUteis.Should().Contain(d => d.Uf == "PA");
    }

    [Fact(DisplayName = "Criar com mesma data e UFs diferentes não é duplicata")]
    public void Criar_MesmaDataUfsDiferentes_NaoEDuplicata()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1",
            [
                new DiaNaoUtilCriacao("ESTADUAL", null, null, null, DataBase, "Feriado do Pará", "PA"),
                new DiaNaoUtilCriacao("ESTADUAL", null, null, null, DataBase, "Feriado de São Paulo", "SP"),
            ]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.DiasNaoUteis.Should().HaveCount(2);
    }

    [Theory(DisplayName = "Criar com descrição ausente ou em branco falha")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_DescricaoVazia_Falha(string descricao)
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [new DiaNaoUtilCriacao("NACIONAL", null, null, null, DataBase, descricao)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.DescricaoObrigatoria);
    }

    [Fact(DisplayName = "Criar com descrição acima de 200 caracteres falha")]
    public void Criar_DescricaoMuitoLonga_Falha()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [new DiaNaoUtilCriacao("NACIONAL", null, null, null, DataBase, new string('A', 201))]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.DescricaoTamanho);
    }

    [Theory(DisplayName = "Criar com código IBGE do município em formato inválido falha")]
    [InlineData("123456")]
    [InlineData("123456789")]
    [InlineData("150140A")]
    [InlineData("0000000")]
    public void Criar_MunicipioIbgeFormatoInvalido_Falha(string municipioIbge)
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [Municipal(municipioIbge: municipioIbge)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "Criar com item de dia não útil nulo na lista falha")]
    public void Criar_DiaNaoUtilNulo_Falha()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1", [Nacional(), null!]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.DiaNaoUtilNulo);
    }

    [Fact(DisplayName = "Criar com mesma data, abrangência e município duplicados falha")]
    public void Criar_DataDuplicadaMesmaAbrangenciaEMunicipio_Falha()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1",
            [
                new DiaNaoUtilCriacao("MUNICIPAL", "1501402", "Belém", "PA", DataBase, "Aniversário do município"),
                new DiaNaoUtilCriacao("MUNICIPAL", "1501402", "Belém", "PA", DataBase, "Duplicata"),
            ]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset);
    }

    [Fact(DisplayName = "Criar com mesma data e abrangências diferentes não é duplicata")]
    public void Criar_MesmaDataAbrangenciasDiferentes_NaoEDuplicata()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1",
            [
                new DiaNaoUtilCriacao("NACIONAL", null, null, null, DataBase, "Feriado nacional"),
                new DiaNaoUtilCriacao("ESTADUAL", null, null, null, DataBase, "Feriado estadual", "PA"),
            ]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.DiasNaoUteis.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Criar com mesma data e municípios diferentes não é duplicata")]
    public void Criar_MesmaDataMunicipiosDiferentes_NaoEDuplicata()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(
            "2027.1",
            [
                new DiaNaoUtilCriacao("MUNICIPAL", "1501402", "Belém", "PA", DataBase, "Aniversário de Belém"),
                new DiaNaoUtilCriacao("MUNICIPAL", "1500107", "Abaetetuba", "PA", DataBase, "Aniversário de Abaetetuba"),
            ]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.DiasNaoUteis.Should().HaveCount(2);
    }

    // ── MarcarVigente / MarcarNaoVigente ──────────────────────────────

    [Fact(DisplayName = "MarcarVigente em dataset recém-criado tem sucesso")]
    public void MarcarVigente_DatasetRecemCriado_Sucesso()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;

        Result resultado = calendario.MarcarVigente();

        resultado.IsSuccess.Should().BeTrue();
        calendario.Vigente.Should().BeTrue();
    }

    [Fact(DisplayName = "MarcarVigente chamado duas vezes falha na segunda chamada (JaVigente)")]
    public void MarcarVigente_ChamadoDuasVezes_FalhaNaSegunda()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;
        calendario.MarcarVigente();

        Result resultado = calendario.MarcarVigente();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.JaVigente);
        calendario.Vigente.Should().BeTrue();
    }

    [Fact(DisplayName = "MarcarNaoVigente volta Vigente para false")]
    public void MarcarNaoVigente_VoltaParaFalse()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;
        calendario.MarcarVigente();

        calendario.MarcarNaoVigente();

        calendario.Vigente.Should().BeFalse();
    }
}
