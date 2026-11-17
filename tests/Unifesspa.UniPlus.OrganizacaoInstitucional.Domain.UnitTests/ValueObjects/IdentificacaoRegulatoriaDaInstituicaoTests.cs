namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As regras de campo da identificação regulatória e-MEC, conferidas pela validação do agregado
/// (ADR-0125): os cinco obrigatórios, cada um com o código de ausência e o de tamanho, e os nove
/// opcionais, que partilham o código de tamanho e rotulam o campo real.
/// </summary>
public sealed class IdentificacaoRegulatoriaDaInstituicaoTests
{
    private static readonly Dictionary<string, (int Maximo, string CodigoObrigatorio, string CodigoTamanho)> Obrigatorios = new()
    {
        ["codigoEmec"] = (20, InstituicaoErrorCodes.CodigoEmecObrigatorio, InstituicaoErrorCodes.CodigoEmecTamanho),
        ["nome"] = (250, InstituicaoErrorCodes.NomeObrigatorio, InstituicaoErrorCodes.NomeTamanho),
        ["sigla"] = (50, InstituicaoErrorCodes.SiglaObrigatoria, InstituicaoErrorCodes.SiglaTamanho),
        ["organizacaoAcademica"] = (100, InstituicaoErrorCodes.OrganizacaoAcademicaObrigatoria, InstituicaoErrorCodes.OrganizacaoAcademicaTamanho),
        ["categoriaAdministrativa"] = (100, InstituicaoErrorCodes.CategoriaAdministrativaObrigatoria, InstituicaoErrorCodes.CategoriaAdministrativaTamanho),
    };

    private static IdentificacaoRegulatoriaDaInstituicao ComObrigatorio(string campo, string? valor) => campo switch
    {
        "codigoEmec" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { CodigoEmec = valor },
        "nome" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { Nome = valor },
        "sigla" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { Sigla = valor },
        "organizacaoAcademica" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { OrganizacaoAcademica = valor },
        _ => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { CategoriaAdministrativa = valor },
    };

    private static IdentificacaoRegulatoriaDaInstituicao ComOpcional(string campo, string? valor) => campo switch
    {
        "cnpj" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { Cnpj = valor },
        "mantenedora" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { Mantenedora = valor },
        "codigoMantenedoraEmec" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { CodigoMantenedoraEmec = valor },
        "situacao" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { Situacao = valor },
        "atoCredenciamento" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { AtoCredenciamento = valor },
        "atoRecredenciamento" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { AtoRecredenciamento = valor },
        "conceitoInstitucional" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { ConceitoInstitucional = valor },
        "igc" => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { Igc = valor },
        _ => InstituicaoDeTeste.IdentificacaoDaUnifesspa with { Website = valor },
    };

    private static Result Validar(IdentificacaoRegulatoriaDaInstituicao identificacao) =>
        Instituicao.ValidarCampos(identificacao, CidadeDaSede.Nenhuma, endereco: null);

    [Theory(DisplayName = "Campo obrigatório nulo, vazio ou em branco recusa com o código de ausência do campo")]
    [InlineData("codigoEmec", null)]
    [InlineData("codigoEmec", "")]
    [InlineData("codigoEmec", "   ")]
    [InlineData("nome", null)]
    [InlineData("nome", "")]
    [InlineData("nome", "   ")]
    [InlineData("sigla", null)]
    [InlineData("sigla", "")]
    [InlineData("sigla", "   ")]
    [InlineData("organizacaoAcademica", null)]
    [InlineData("organizacaoAcademica", "")]
    [InlineData("organizacaoAcademica", "   ")]
    [InlineData("categoriaAdministrativa", null)]
    [InlineData("categoriaAdministrativa", "")]
    [InlineData("categoriaAdministrativa", "   ")]
    public void Obrigatorio_AusenteOuEmBranco_RecusaComCodigoDeAusencia(string campo, string? valor)
    {
        Result resultado = Validar(ComObrigatorio(campo, valor));

        FieldError erro = resultado.Errors.Should().ContainSingle().Which;
        erro.Field.Should().Be(campo);
        erro.Error.Code.Should().Be(Obrigatorios[campo].CodigoObrigatorio);
    }

    [Theory(DisplayName = "Campo obrigatório com exatamente o tamanho máximo é aceito")]
    [InlineData("codigoEmec")]
    [InlineData("nome")]
    [InlineData("sigla")]
    [InlineData("organizacaoAcademica")]
    [InlineData("categoriaAdministrativa")]
    public void Obrigatorio_NoLimite_Aceita(string campo)
    {
        Result resultado = Validar(ComObrigatorio(campo, new string('a', Obrigatorios[campo].Maximo)));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Campo obrigatório acima do tamanho máximo recusa com o código de tamanho do campo")]
    [InlineData("codigoEmec")]
    [InlineData("nome")]
    [InlineData("sigla")]
    [InlineData("organizacaoAcademica")]
    [InlineData("categoriaAdministrativa")]
    public void Obrigatorio_AcimaDoLimite_RecusaComCodigoDeTamanho(string campo)
    {
        Result resultado = Validar(ComObrigatorio(campo, new string('a', Obrigatorios[campo].Maximo + 1)));

        FieldError erro = resultado.Errors.Should().ContainSingle().Which;
        erro.Field.Should().Be(campo);
        erro.Error.Code.Should().Be(Obrigatorios[campo].CodigoTamanho);
    }

    [Fact(DisplayName = "O tamanho do campo obrigatório é medido depois de aparar os espaços")]
    public void Obrigatorio_NoLimiteComEspacosNasPontas_Aceita()
    {
        string noLimiteComEspacos = $"  {new string('a', 20)}  ";

        Result resultado = Validar(ComObrigatorio("codigoEmec", noLimiteComEspacos));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A mensagem de tamanho cita o campo e o limite")]
    public void Obrigatorio_AcimaDoLimite_CitaOCampoEOLimiteNaMensagem()
    {
        Result resultado = Validar(ComObrigatorio("codigoEmec", new string('a', 21)));

        resultado.Error!.Message.Should().Be("Código e-MEC deve ter no máximo 20 caracteres.");
    }

    [Theory(DisplayName = "Campo opcional com exatamente o tamanho máximo é aceito")]
    [InlineData("cnpj", 100)]
    [InlineData("mantenedora", 250)]
    [InlineData("codigoMantenedoraEmec", 20)]
    [InlineData("situacao", 100)]
    [InlineData("atoCredenciamento", 500)]
    [InlineData("atoRecredenciamento", 500)]
    [InlineData("conceitoInstitucional", 100)]
    [InlineData("igc", 100)]
    [InlineData("website", 255)]
    public void Opcional_NoLimite_Aceita(string campo, int maximo)
    {
        Result resultado = Validar(ComOpcional(campo, new string('a', maximo)));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Campo opcional acima do tamanho máximo recusa com o campo real e o limite na mensagem")]
    [InlineData("cnpj", 100)]
    [InlineData("mantenedora", 250)]
    [InlineData("codigoMantenedoraEmec", 20)]
    [InlineData("situacao", 100)]
    [InlineData("atoCredenciamento", 500)]
    [InlineData("atoRecredenciamento", 500)]
    [InlineData("conceitoInstitucional", 100)]
    [InlineData("igc", 100)]
    [InlineData("website", 255)]
    public void Opcional_AcimaDoLimite_RecusaComOCampoReal(string campo, int maximo)
    {
        Result resultado = Validar(ComOpcional(campo, new string('a', maximo + 1)));

        FieldError erro = resultado.Errors.Should().ContainSingle().Which;
        erro.Field.Should().Be(campo);
        erro.Error.Code.Should().Be(InstituicaoErrorCodes.CampoOpcionalTamanho);
        erro.Error.Message.Should().Be($"Campo opcional excede o tamanho máximo de {maximo} caracteres.");
    }

    [Theory(DisplayName = "Campo opcional nulo ou em branco é aceito")]
    [InlineData("cnpj", null)]
    [InlineData("mantenedora", "   ")]
    [InlineData("codigoMantenedoraEmec", "")]
    [InlineData("situacao", null)]
    [InlineData("atoCredenciamento", "   ")]
    [InlineData("atoRecredenciamento", "")]
    [InlineData("conceitoInstitucional", null)]
    [InlineData("igc", "   ")]
    [InlineData("website", "")]
    public void Opcional_NuloOuEmBranco_Aceita(string campo, string? valor)
    {
        Result resultado = Validar(ComOpcional(campo, valor));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar converte em nulo os nove opcionais em branco")]
    public void Criar_ComOpcionaisEmBranco_ConverteOsNoveEmNulo()
    {
        Instituicao instituicao = InstituicaoDeTeste.Criar(ComTodosOsOpcionais("   ")).Value!;

        Opcionais(instituicao).Should().HaveCount(9).And.OnlyContain(valor => valor == null);
    }

    [Fact(DisplayName = "Criar apara os espaços das pontas dos nove opcionais preenchidos")]
    public void Criar_ComOpcionaisComEspacosNasPontas_AparaOsNove()
    {
        Instituicao instituicao = InstituicaoDeTeste.Criar(ComTodosOsOpcionais("  valor  ")).Value!;

        Opcionais(instituicao).Should().HaveCount(9).And.OnlyContain(valor => valor == "valor");
    }

    private static IdentificacaoRegulatoriaDaInstituicao ComTodosOsOpcionais(string valor) =>
        InstituicaoDeTeste.IdentificacaoDaUnifesspa with
        {
            Cnpj = valor,
            Mantenedora = valor,
            CodigoMantenedoraEmec = valor,
            Situacao = valor,
            AtoCredenciamento = valor,
            AtoRecredenciamento = valor,
            ConceitoInstitucional = valor,
            Igc = valor,
            Website = valor,
        };

    private static string?[] Opcionais(Instituicao instituicao) =>
    [
        instituicao.Cnpj,
        instituicao.Mantenedora,
        instituicao.CodigoMantenedoraEmec,
        instituicao.Situacao,
        instituicao.AtoCredenciamento,
        instituicao.AtoRecredenciamento,
        instituicao.ConceitoInstitucional,
        instituicao.Igc,
        instituicao.Website,
    ];
}
