namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

/// <summary>
/// A lista de recusas da Instituição é o <c>errors[]</c> do contrato (ADR-0023), e a ADR-0125 fixa que ela acumula
/// todas as violações numa ordem estável: os cinco obrigatórios, a cidade da sede, a coerência do endereço (só com a
/// cidade válida), os nove opcionais e, por último, o formato do endereço.
/// </summary>
public sealed class OrdemDasRecusasDaInstituicaoTests
{
    private static readonly string AcimaDeQualquerLimite = new('x', 501);

    private static readonly (string? Campo, string Codigo)[] RecusasDosOpcionais =
    [
        ("cnpj", InstituicaoErrorCodes.CampoOpcionalTamanho),
        ("mantenedora", InstituicaoErrorCodes.CampoOpcionalTamanho),
        ("codigoMantenedoraEmec", InstituicaoErrorCodes.CampoOpcionalTamanho),
        ("situacao", InstituicaoErrorCodes.CampoOpcionalTamanho),
        ("atoCredenciamento", InstituicaoErrorCodes.CampoOpcionalTamanho),
        ("atoRecredenciamento", InstituicaoErrorCodes.CampoOpcionalTamanho),
        ("conceitoInstitucional", InstituicaoErrorCodes.CampoOpcionalTamanho),
        ("igc", InstituicaoErrorCodes.CampoOpcionalTamanho),
        ("website", InstituicaoErrorCodes.CampoOpcionalTamanho),
    ];

    private static readonly (string? Campo, string Codigo)[] RecusasDeTudoInvalido =
    [
        ("codigoEmec", InstituicaoErrorCodes.CodigoEmecObrigatorio),
        ("nome", InstituicaoErrorCodes.NomeObrigatorio),
        ("sigla", InstituicaoErrorCodes.SiglaObrigatoria),
        ("organizacaoAcademica", InstituicaoErrorCodes.OrganizacaoAcademicaObrigatoria),
        ("categoriaAdministrativa", InstituicaoErrorCodes.CategoriaAdministrativaObrigatoria),
        ("cidadeNome", CidadeReferenciaErrorCodes.NomeObrigatorio),
        ("cidadeUf", CidadeReferenciaErrorCodes.UfIncoerente),
        .. RecusasDosOpcionais,
        ("endereco", EnderecoReferenciaErrorCodes.CepFormatoInvalido),
    ];

    private readonly IInstituicaoRepository _repositorio = Substitute.For<IInstituicaoRepository>();

    [Fact(DisplayName = "Na criação, tudo inválido: obrigatórios, cidade, opcionais e o formato do endereço por último")]
    public async Task Criar_TudoInvalido_RecusaNaOrdemDoContrato()
    {
        Result<Guid> resultado = await CriarAsync(new CriarInstituicaoCommand(
            null, " ", null, "", null,
            AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite,
            AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite,
            EnderecoEmMaraba(cep: "123"), "1504208", null, "SP", null));

        Recusas(resultado.Errors).Should().Equal(RecusasDeTudoInvalido);
    }

    [Fact(DisplayName = "Na criação, a coerência do endereço vem depois da cidade válida e antes dos opcionais")]
    public async Task Criar_EnderecoIncoerente_RecusaEntreACidadeEOsOpcionais()
    {
        Result<Guid> resultado = await CriarAsync(new CriarInstituicaoCommand(
            AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite,
            null, null, null, null, null, null, null, null, AcimaDeQualquerLimite,
            EnderecoEmMaraba(cep: "68507590"), "1500107", "Abaetetuba", "PA", null));

        Recusas(resultado.Errors).Should().Equal(
            ("codigoEmec", InstituicaoErrorCodes.CodigoEmecTamanho),
            ("nome", InstituicaoErrorCodes.NomeTamanho),
            ("sigla", InstituicaoErrorCodes.SiglaTamanho),
            ("organizacaoAcademica", InstituicaoErrorCodes.OrganizacaoAcademicaTamanho),
            ("categoriaAdministrativa", InstituicaoErrorCodes.CategoriaAdministrativaTamanho),
            ("endereco", EnderecoReferenciaErrorCodes.CidadeIncoerente),
            ("website", InstituicaoErrorCodes.CampoOpcionalTamanho));
    }

    [Fact(DisplayName = "Na criação, o endereço sem a cidade da sede é recusado antes dos opcionais")]
    public async Task Criar_EnderecoSemCidade_RecusaAntesDosOpcionais()
    {
        Result<Guid> resultado = await CriarAsync(new CriarInstituicaoCommand(
            "3990", "Universidade Federal do Sul e Sudeste do Pará", "Unifesspa", "Universidade", "Pública Federal",
            AcimaDeQualquerLimite, null, null, null, null, null, null, null, null,
            EnderecoEmMaraba(cep: "68507590"), null, null, null, null));

        Recusas(resultado.Errors).Should().Equal(
            ("endereco", EnderecoReferenciaErrorCodes.CidadeObrigatoriaComEndereco),
            ("cnpj", InstituicaoErrorCodes.CampoOpcionalTamanho));
    }

    [Fact(DisplayName = "Na atualização, tudo inválido recusa na mesma ordem, antes de buscar a Instituição")]
    public async Task Atualizar_TudoInvalido_RecusaNaOrdemDoContratoSemBuscar()
    {
        Result resultado = await AtualizarInstituicaoCommandHandler.Handle(
            new AtualizarInstituicaoCommand(
                Guid.CreateVersion7(), null, " ", null, "", null,
                AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite,
                AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite, AcimaDeQualquerLimite,
                EnderecoEmMaraba(cep: "123"), "1504208", null, "SP", null),
            new ConferenciaDoCadastroDaInstituicao(_repositorio, Substitute.For<IUnidadeRepository>()),
            new GravacaoDaInstituicao(_repositorio, Substitute.For<IUnitOfWork>(), Substitute.For<IInstituicaoCacheInvalidator>()),
            TimeProvider.System, CancellationToken.None);

        Recusas(resultado.Errors).Should().Equal(RecusasDeTudoInvalido);
        await _repositorio.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    private Task<Result<Guid>> CriarAsync(CriarInstituicaoCommand command) =>
        CriarInstituicaoCommandHandler.Handle(
            command,
            new ConferenciaDoCadastroDaInstituicao(_repositorio, Substitute.For<IUnidadeRepository>()),
            new GravacaoDaInstituicao(_repositorio, Substitute.For<IUnitOfWork>(), Substitute.For<IInstituicaoCacheInvalidator>()),
            TimeProvider.System, CancellationToken.None);

    private static EnderecoGeoInput EnderecoEmMaraba(string cep) =>
        new(cep, "Folha 31", "s/n", null, "Nova Marabá", null,
            new CidadeReferenciaInput("1504208", "Marabá", "PA"),
            -5.3m, -49.1m, NivelResolucaoEndereco.Logradouro, "logradouro");

    private static (string? Campo, string Codigo)[] Recusas(IEnumerable<FieldError> erros) =>
        [.. erros.Select(static erro => (erro.Field, erro.Error.Code))];
}
