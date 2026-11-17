namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Testes de unidade do agregado <see cref="Instituicao"/> (Story #585 · #686):
/// validação de formato na factory e na atualização, incluindo a referência de
/// cidade do Geo (ADR-0090) opcional all-or-nothing. O invariante singleton e a
/// conferência do tipo da Unidade raiz são responsabilidade do handler e estão
/// cobertos nos testes de Application/Integration.
/// </summary>
public sealed class InstituicaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 22, 17, 10, 0, TimeSpan.Zero);

    /// <summary>A identificação regulatória de uma IES credenciada, com CNPJ, conceito, IGC e sítio.</summary>
    private static readonly IdentificacaoRegulatoriaDaInstituicao UfrCredenciada = new(
        "4000", "Universidade Federal Renomeada", "UFR", "Universidade", "Pública Federal",
        Cnpj: "12.345.678/0001-99", Situacao: "Credenciada", AtoCredenciamento: "Portaria 123",
        ConceitoInstitucional: "4", Igc: "4", Website: "https://unifesspa.edu.br");

    private static readonly CidadeDaSede SedeEmMarabaPeloGeo =
        InstituicaoDeTeste.SedeEmMaraba with { Origem = ReferenciaCidadeGeo.OrigemGeoApi };

    private static ReferenciaEnderecoGeo Endereco(string cidadeCodigoIbge = "1504208", string cidadeUf = "PA") =>
        ReferenciaEnderecoGeo.Criar(
            "68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            cidadeCodigoIbge, "Marabá", cidadeUf, -5.3m, -49.1m,
            NivelResolucaoEndereco.Logradouro, "logradouro", Agora).Value!;

    [Fact(DisplayName = "Criar com campos válidos retorna sucesso com Id Guid v7 não vazio")]
    public void Criar_ComCamposValidos_RetornaSucessoComGuidV7()
    {
        Result<Instituicao> resultado = InstituicaoDeTeste.Criar();

        resultado.IsSuccess.Should().BeTrue();
        Instituicao instituicao = resultado.Value!;
        instituicao.Id.Should().NotBe(Guid.Empty);
        instituicao.Id.Version.Should().Be(7);
        instituicao.CodigoEmec.Should().Be("3990");
        instituicao.Sigla.Should().Be("Unifesspa");
        instituicao.IsDeleted.Should().BeFalse();
    }

    [Theory(DisplayName = "Criar sem um campo obrigatório retorna o erro correspondente")]
    [InlineData("", "Nome", "SIGLA", "Org", "Cat", InstituicaoErrorCodes.CodigoEmecObrigatorio)]
    [InlineData("3990", "", "SIGLA", "Org", "Cat", InstituicaoErrorCodes.NomeObrigatorio)]
    [InlineData("3990", "Nome", "", "Org", "Cat", InstituicaoErrorCodes.SiglaObrigatoria)]
    [InlineData("3990", "Nome", "SIGLA", "", "Cat", InstituicaoErrorCodes.OrganizacaoAcademicaObrigatoria)]
    [InlineData("3990", "Nome", "SIGLA", "Org", "", InstituicaoErrorCodes.CategoriaAdministrativaObrigatoria)]
    public void Criar_SemCampoObrigatorio_RetornaErro(
        string codigoEmec,
        string nome,
        string sigla,
        string organizacaoAcademica,
        string categoriaAdministrativa,
        string codigoEsperado)
    {
        Result<Instituicao> resultado = InstituicaoDeTeste.Criar(new IdentificacaoRegulatoriaDaInstituicao(
            codigoEmec, nome, sigla, organizacaoAcademica, categoriaAdministrativa));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigoEsperado);
    }

    [Fact(DisplayName = "Criar normaliza Trim dos campos e converte opcionais em branco para null")]
    public void Criar_NormalizaTrimEOpcionaisVazios()
    {
        Result<Instituicao> resultado = InstituicaoDeTeste.Criar(
            new IdentificacaoRegulatoriaDaInstituicao("  3990  ", "  Universidade  ", "  Unifesspa  ", "  Universidade  ", "  Pública Federal  ",
                Cnpj: "   ", Mantenedora: "  Fundação  "),
            new CidadeDaSede("1504208", "  Marabá  ", "pa", null, null));

        resultado.IsSuccess.Should().BeTrue();
        Instituicao instituicao = resultado.Value!;
        instituicao.CodigoEmec.Should().Be("3990");
        instituicao.Nome.Should().Be("Universidade");
        instituicao.Cnpj.Should().BeNull("string só com espaços vira null");
        instituicao.Mantenedora.Should().Be("Fundação");
        instituicao.CidadeCodigoIbge.Should().Be("1504208");
        instituicao.CidadeNome.Should().Be("Marabá");
        instituicao.CidadeUf.Should().Be("PA", "a UF é normalizada para caixa alta");
    }

    [Fact(DisplayName = "Criar sem referência de cidade é válido e deixa o trio nulo (opcional)")]
    public void Criar_SemReferenciaDeCidade_DeixaTrioNulo()
    {
        Instituicao instituicao = InstituicaoDeTeste.Criar().Value!;

        instituicao.CidadeCodigoIbge.Should().BeNull();
        instituicao.CidadeNome.Should().BeNull();
        instituicao.CidadeUf.Should().BeNull();
        instituicao.CidadeOrigem.Should().BeNull();
        instituicao.CidadeDisplayAtualizadoEm.Should().BeNull();
    }

    [Theory(DisplayName = "Criar com referência de cidade parcial retorna o campo faltante (all-or-nothing)")]
    [InlineData(null, "Marabá", "PA", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio)]
    [InlineData("1504208", null, "PA", CidadeReferenciaErrorCodes.NomeObrigatorio)]
    [InlineData("1504208", "Marabá", null, CidadeReferenciaErrorCodes.UfObrigatoria)]
    public void Criar_ComCidadeParcial_RetornaErro(
        string? cidadeCodigoIbge, string? cidadeNome, string? cidadeUf, string codigoEsperado)
    {
        Result<Instituicao> resultado = InstituicaoDeTeste.Criar(
            cidade: new CidadeDaSede(cidadeCodigoIbge, cidadeNome, cidadeUf, null, null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigoEsperado);
    }

    [Fact(DisplayName = "Criar com UF incoerente com o prefixo do código IBGE é rejeitado (15=PA, não SP)")]
    public void Criar_ComCidadeIncoerente_RetornaUfIncoerente()
    {
        Result<Instituicao> resultado = InstituicaoDeTeste.Criar(
            cidade: new CidadeDaSede("1504208", "Marabá", "SP", null, null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "Criar com nome de cidade acima do limite retorna erro de domínio (422), não estoura na persistência")]
    public void Criar_ComCidadeNomeMuitoLongo_RetornaNomeTamanho()
    {
        string nomeLongo = new('A', ReferenciaCidadeGeo.NomeMaxLength + 1);

        Result<Instituicao> resultado = InstituicaoDeTeste.Criar(
            cidade: new CidadeDaSede("1504208", nomeLongo, "PA", null, null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Atualizar com campos válidos altera o estado regulatório e o vínculo")]
    public void Atualizar_ComCamposValidos_AlteraEstado()
    {
        Instituicao instituicao = InstituicaoDeTeste.Criar().Value!;
        Guid novaRaiz = Guid.CreateVersion7();

        Result resultado = instituicao.Atualizar(
            UfrCredenciada, SedeEmMarabaPeloGeo, endereco: null, unidadeRaizId: novaRaiz);

        resultado.IsSuccess.Should().BeTrue();
        instituicao.CodigoEmec.Should().Be("4000");
        instituicao.Sigla.Should().Be("UFR");
        instituicao.Situacao.Should().Be("Credenciada");
        instituicao.CidadeCodigoIbge.Should().Be("1504208");
        instituicao.CidadeUf.Should().Be("PA");
        instituicao.UnidadeRaizId.Should().Be(novaRaiz);
    }

    [Fact(DisplayName = "Atualizar sem nome retorna NomeObrigatorio e preserva o estado anterior")]
    public void Atualizar_SemNome_RetornaErro()
    {
        Instituicao instituicao = InstituicaoDeTeste.Criar().Value!;

        Result resultado = instituicao.Atualizar(
            InstituicaoDeTeste.IdentificacaoDaUnifesspa with { Nome = "" },
            cidade: default, endereco: null, unidadeRaizId: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NomeObrigatorio);
        instituicao.Nome.Should().Be("Universidade Federal do Sul e Sudeste do Pará");
    }

    [Fact(DisplayName = "Criar com endereço estruturado + cidade coerente persiste o endereço")]
    public void Criar_ComEnderecoECidadeCoerente_PersisteEndereco()
    {
        Result<Instituicao> resultado = InstituicaoDeTeste.Criar(
            cidade: InstituicaoDeTeste.SedeEmMaraba, endereco: Endereco());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Endereco!.Cep.Should().Be("68507590");
    }

    [Fact(DisplayName = "Criar com endereço mas sem cidade da sede falha (cidade obrigatória com endereço)")]
    public void Criar_ComEnderecoSemCidade_RetornaCidadeObrigatoria()
    {
        Result<Instituicao> resultado = InstituicaoDeTeste.Criar(endereco: Endereco());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.CidadeObrigatoriaComEndereco);
    }

    [Fact(DisplayName = "Criar com endereço de cidade incoerente com a cidade da sede falha")]
    public void Criar_ComEnderecoCidadeIncoerente_RetornaCidadeIncoerente()
    {
        Result<Instituicao> resultado = InstituicaoDeTeste.Criar(
            cidade: InstituicaoDeTeste.SedeEmMaraba,
            endereco: Endereco(cidadeCodigoIbge: "1501402"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.CidadeIncoerente);
    }

    [Fact(DisplayName = "Criar com três violações independentes acumula as três no mesmo lote")]
    public void Criar_TresViolacoesIndependentes_AcumulaAsTres()
    {
        Result<Instituicao> resultado = InstituicaoDeTeste.Criar(
            new IdentificacaoRegulatoriaDaInstituicao(null, null, "Unifesspa", "Universidade", "Pública Federal"),
            cidade: new CidadeDaSede("1504208", "Marabá", "SP", null, null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors.Should().Contain(e => e.Field == "codigoEmec" && e.Error.Code == InstituicaoErrorCodes.CodigoEmecObrigatorio);
        resultado.Errors.Should().Contain(e => e.Field == "nome" && e.Error.Code == InstituicaoErrorCodes.NomeObrigatorio);
        resultado.Errors.Should().Contain(e => e.Field == "cidadeUf" && e.Error.Code == CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "Criar com dois campos opcionais acima do limite rotula cada um com o campo real")]
    public void Criar_ComDoisOpcionaisMuitoLongos_RotulaCadaCampo()
    {
        string valorLongo = new('A', 600);

        Result<Instituicao> resultado = InstituicaoDeTeste.Criar(
            new IdentificacaoRegulatoriaDaInstituicao("3990", "Universidade", "Unifesspa", "Universidade", "Pública Federal",
                Cnpj: valorLongo, AtoCredenciamento: valorLongo));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors.Should().Contain(e => e.Field == "cnpj" && e.Error.Code == InstituicaoErrorCodes.CampoOpcionalTamanho);
        resultado.Errors.Should().Contain(e => e.Field == "atoCredenciamento" && e.Error.Code == InstituicaoErrorCodes.CampoOpcionalTamanho);
    }

    [Fact(DisplayName = "ValidarCamposDoPayload replica o resultado de Criar sem instanciar o agregado")]
    public void ValidarCampos_MesmoPayloadDeCriarComFalha_RetornaMesmosErros()
    {
        Result validacao = Instituicao.ValidarCampos(
            new IdentificacaoRegulatoriaDaInstituicao(null, null, "Unifesspa", "Universidade", "Pública Federal"),
            cidade: default, endereco: null);

        validacao.IsFailure.Should().BeTrue();
        validacao.Errors.Should().ContainSingle(e => e.Field == "codigoEmec");
        validacao.Errors.Should().ContainSingle(e => e.Field == "nome");
    }

    [Fact(DisplayName = "CidadeDaSede devolve a cidade das colunas planas, com a proveniência e o carimbo gravados")]
    public void CidadeDaSede_AposCriarComCidadeCarimbada_DevolveACidadeGravada()
    {
        CidadeDaSede carimbada = SedeEmMarabaPeloGeo with { DisplayAtualizadoEm = Agora };

        Instituicao instituicao = InstituicaoDeTeste.Criar(cidade: carimbada).Value!;

        instituicao.CidadeDaSede.Should().Be(carimbada);
    }

    [Fact(DisplayName = "CidadeDaSede sem cidade informada é a ausência de cidade")]
    public void CidadeDaSede_SemCidadeInformada_EhNenhuma()
    {
        Instituicao instituicao = InstituicaoDeTeste.Criar().Value!;

        instituicao.CidadeDaSede.Should().Be(CidadeDaSede.Nenhuma);
    }

    [Fact(DisplayName = "Criar com endereço e cidade inválida acusa só a cidade, sem mascará-la com a coerência do endereço")]
    public void Criar_ComEnderecoECidadeInvalida_AcusaSoACidade()
    {
        Result<Instituicao> resultado = InstituicaoDeTeste.Criar(
            cidade: new CidadeDaSede("1504208", "Marabá", "SP", null, null), endereco: Endereco());

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("cidadeUf");
    }
}
