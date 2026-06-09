namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Instituição de ensino superior dona da plataforma — a Unifesspa no
/// deployment da Unifesspa (ADR-0055). Carrega a identificação regulatória
/// e-MEC (código, classificação, mantenedora, atos de credenciamento,
/// indicadores de qualidade) que serve de cabeçalho institucional exibido em
/// editais, comprovantes e telas administrativas.
/// </summary>
/// <remarks>
/// <para><strong>Singleton</strong> (UNI-REQ-0007 · ADR-0055): cada deployment
/// atende uma única instituição, logo há no máximo uma <c>Instituicao</c> viva
/// (não soft-deleted) por instância. O cardinal de um é garantido em duas
/// camadas — guard de domínio na criação (o handler consulta o repositório
/// antes de criar) e índice único parcial de banco sobre uma coluna sentinela
/// constante <c>WHERE is_deleted = false</c>. A remoção é lógica; uma
/// Instituição removida não conta para o limite, liberando o recadastramento.</para>
/// <para>Não há unicidade própria sobre <c>codigo_emec</c>: sob o singleton, o
/// cardinal de um já garante a identidade (o dicionário marca a coluna como
/// <c>text NOT NULL</c>, sem <c>UNIQUE</c>).</para>
/// <para>A vinculação com a <c>Unidade</c> raiz (reitoria) é por
/// <c>UnidadeRaizId</c> — FK intra-banco (ADR-0054). A conferência de que a
/// Unidade referenciada é viva e do tipo reitoria é responsabilidade do handler
/// (via repositório); esta entidade recebe o Id já validado.</para>
/// </remarks>
public sealed class Instituicao : EntityBase, IAuditableEntity
{
    private const int CodigoEmecMaxLength = 20;
    private const int NomeMaxLength = 250;
    private const int SiglaMaxLength = 50;
    private const int OrganizacaoAcademicaMaxLength = 100;
    private const int CategoriaAdministrativaMaxLength = 100;
    private const int CampoOpcionalCurtoMaxLength = 100;
    private const int CampoOpcionalMedioMaxLength = 255;
    private const int CampoOpcionalLongoMaxLength = 500;

    public string CodigoEmec { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string Sigla { get; private set; } = string.Empty;
    public string OrganizacaoAcademica { get; private set; } = string.Empty;
    public string CategoriaAdministrativa { get; private set; } = string.Empty;
    public string? Cnpj { get; private set; }
    public string? Mantenedora { get; private set; }
    public string? CodigoMantenedoraEmec { get; private set; }
    public string? Situacao { get; private set; }
    public string? AtoCredenciamento { get; private set; }
    public string? AtoRecredenciamento { get; private set; }
    public string? ConceitoInstitucional { get; private set; }
    public string? Igc { get; private set; }
    public string? Website { get; private set; }
    public string? EnderecoSede { get; private set; }
    public string? MunicipioSede { get; private set; }
    public Guid? UnidadeRaizId { get; private set; }

    /// <summary>
    /// Sentinela constante (sempre <see langword="true"/>) — concern de
    /// persistência que materializa a salvaguarda física do singleton: o índice
    /// único parcial <c>WHERE is_deleted = false</c> sobre esta coluna admite no
    /// máximo um registro vivo (ADR-0055), e o CHECK de banco impede qualquer
    /// outro valor. Não é dado de negócio; não é exposto em DTO/View.
    /// </summary>
    public bool RegistroVivoSentinela { get; private set; } = true;

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private Instituicao()
    {
    }

    /// <summary>
    /// Cria a Instituição. Valida apenas formato e domínio local — o invariante
    /// singleton (no máximo uma viva) e a conferência do tipo da Unidade raiz são
    /// responsabilidade do handler chamador (dependem do repositório).
    /// </summary>
    public static Result<Instituicao> Criar(
        string codigoEmec,
        string nome,
        string sigla,
        string organizacaoAcademica,
        string categoriaAdministrativa,
        string? cnpj,
        string? mantenedora,
        string? codigoMantenedoraEmec,
        string? situacao,
        string? atoCredenciamento,
        string? atoRecredenciamento,
        string? conceitoInstitucional,
        string? igc,
        string? website,
        string? enderecoSede,
        string? municipioSede,
        Guid? unidadeRaizId)
    {
        ArgumentNullException.ThrowIfNull(codigoEmec);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(sigla);
        ArgumentNullException.ThrowIfNull(organizacaoAcademica);
        ArgumentNullException.ThrowIfNull(categoriaAdministrativa);

        Result validacao = ValidarCampos(
            codigoEmec, nome, sigla, organizacaoAcademica, categoriaAdministrativa,
            cnpj, mantenedora, codigoMantenedoraEmec, situacao, atoCredenciamento,
            atoRecredenciamento, conceitoInstitucional, igc, website, enderecoSede, municipioSede);
        if (validacao.IsFailure)
        {
            return Result<Instituicao>.Failure(validacao.Error!);
        }

        var instituicao = new Instituicao();
        instituicao.AplicarCampos(
            codigoEmec, nome, sigla, organizacaoAcademica, categoriaAdministrativa,
            cnpj, mantenedora, codigoMantenedoraEmec, situacao, atoCredenciamento,
            atoRecredenciamento, conceitoInstitucional, igc, website, enderecoSede,
            municipioSede, unidadeRaizId);

        return Result<Instituicao>.Success(instituicao);
    }

    /// <summary>
    /// Atualiza os dados regulatórios e o vínculo com a reitoria. A conferência
    /// do tipo da Unidade raiz informada é responsabilidade do handler.
    /// </summary>
    public Result Atualizar(
        string codigoEmec,
        string nome,
        string sigla,
        string organizacaoAcademica,
        string categoriaAdministrativa,
        string? cnpj,
        string? mantenedora,
        string? codigoMantenedoraEmec,
        string? situacao,
        string? atoCredenciamento,
        string? atoRecredenciamento,
        string? conceitoInstitucional,
        string? igc,
        string? website,
        string? enderecoSede,
        string? municipioSede,
        Guid? unidadeRaizId)
    {
        ArgumentNullException.ThrowIfNull(codigoEmec);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(sigla);
        ArgumentNullException.ThrowIfNull(organizacaoAcademica);
        ArgumentNullException.ThrowIfNull(categoriaAdministrativa);

        Result validacao = ValidarCampos(
            codigoEmec, nome, sigla, organizacaoAcademica, categoriaAdministrativa,
            cnpj, mantenedora, codigoMantenedoraEmec, situacao, atoCredenciamento,
            atoRecredenciamento, conceitoInstitucional, igc, website, enderecoSede, municipioSede);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        AplicarCampos(
            codigoEmec, nome, sigla, organizacaoAcademica, categoriaAdministrativa,
            cnpj, mantenedora, codigoMantenedoraEmec, situacao, atoCredenciamento,
            atoRecredenciamento, conceitoInstitucional, igc, website, enderecoSede,
            municipioSede, unidadeRaizId);

        return Result.Success();
    }

    private void AplicarCampos(
        string codigoEmec,
        string nome,
        string sigla,
        string organizacaoAcademica,
        string categoriaAdministrativa,
        string? cnpj,
        string? mantenedora,
        string? codigoMantenedoraEmec,
        string? situacao,
        string? atoCredenciamento,
        string? atoRecredenciamento,
        string? conceitoInstitucional,
        string? igc,
        string? website,
        string? enderecoSede,
        string? municipioSede,
        Guid? unidadeRaizId)
    {
        CodigoEmec = codigoEmec.Trim();
        Nome = nome.Trim();
        Sigla = sigla.Trim();
        OrganizacaoAcademica = organizacaoAcademica.Trim();
        CategoriaAdministrativa = categoriaAdministrativa.Trim();
        Cnpj = NormalizarOpcional(cnpj);
        Mantenedora = NormalizarOpcional(mantenedora);
        CodigoMantenedoraEmec = NormalizarOpcional(codigoMantenedoraEmec);
        Situacao = NormalizarOpcional(situacao);
        AtoCredenciamento = NormalizarOpcional(atoCredenciamento);
        AtoRecredenciamento = NormalizarOpcional(atoRecredenciamento);
        ConceitoInstitucional = NormalizarOpcional(conceitoInstitucional);
        Igc = NormalizarOpcional(igc);
        Website = NormalizarOpcional(website);
        EnderecoSede = NormalizarOpcional(enderecoSede);
        MunicipioSede = NormalizarOpcional(municipioSede);
        UnidadeRaizId = unidadeRaizId;
    }

    private static string? NormalizarOpcional(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        return valor.Trim();
    }

    private static Result ValidarCampos(
        string codigoEmec,
        string nome,
        string sigla,
        string organizacaoAcademica,
        string categoriaAdministrativa,
        string? cnpj,
        string? mantenedora,
        string? codigoMantenedoraEmec,
        string? situacao,
        string? atoCredenciamento,
        string? atoRecredenciamento,
        string? conceitoInstitucional,
        string? igc,
        string? website,
        string? enderecoSede,
        string? municipioSede)
    {
        if (string.IsNullOrWhiteSpace(codigoEmec))
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.CodigoEmecObrigatorio,
                "Código e-MEC da Instituição é obrigatório."));
        }

        if (codigoEmec.Trim().Length > CodigoEmecMaxLength)
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.CodigoEmecTamanho,
                $"Código e-MEC deve ter no máximo {CodigoEmecMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.NomeObrigatorio,
                "Nome da Instituição é obrigatório."));
        }

        if (nome.Trim().Length > NomeMaxLength)
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.NomeTamanho,
                $"Nome da Instituição deve ter no máximo {NomeMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(sigla))
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.SiglaObrigatoria,
                "Sigla da Instituição é obrigatória."));
        }

        if (sigla.Trim().Length > SiglaMaxLength)
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.SiglaTamanho,
                $"Sigla da Instituição deve ter no máximo {SiglaMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(organizacaoAcademica))
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.OrganizacaoAcademicaObrigatoria,
                "Organização acadêmica da Instituição é obrigatória."));
        }

        if (organizacaoAcademica.Trim().Length > OrganizacaoAcademicaMaxLength)
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.OrganizacaoAcademicaTamanho,
                $"Organização acadêmica deve ter no máximo {OrganizacaoAcademicaMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(categoriaAdministrativa))
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.CategoriaAdministrativaObrigatoria,
                "Categoria administrativa da Instituição é obrigatória."));
        }

        if (categoriaAdministrativa.Trim().Length > CategoriaAdministrativaMaxLength)
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.CategoriaAdministrativaTamanho,
                $"Categoria administrativa deve ter no máximo {CategoriaAdministrativaMaxLength} caracteres."));
        }

        return ValidarOpcionais(
            cnpj, mantenedora, codigoMantenedoraEmec, situacao, atoCredenciamento,
            atoRecredenciamento, conceitoInstitucional, igc, website, enderecoSede, municipioSede);
    }

    private static Result ValidarOpcionais(
        string? cnpj,
        string? mantenedora,
        string? codigoMantenedoraEmec,
        string? situacao,
        string? atoCredenciamento,
        string? atoRecredenciamento,
        string? conceitoInstitucional,
        string? igc,
        string? website,
        string? enderecoSede,
        string? municipioSede)
    {
        (string? valor, int max)[] opcionais =
        [
            (cnpj, CampoOpcionalCurtoMaxLength),
            (mantenedora, NomeMaxLength),
            (codigoMantenedoraEmec, CodigoEmecMaxLength),
            (situacao, CampoOpcionalCurtoMaxLength),
            (atoCredenciamento, CampoOpcionalLongoMaxLength),
            (atoRecredenciamento, CampoOpcionalLongoMaxLength),
            (conceitoInstitucional, CampoOpcionalCurtoMaxLength),
            (igc, CampoOpcionalCurtoMaxLength),
            (website, CampoOpcionalMedioMaxLength),
            (enderecoSede, CampoOpcionalLongoMaxLength),
            (municipioSede, CampoOpcionalCurtoMaxLength),
        ];

        foreach ((string? valor, int max) in opcionais)
        {
            if (valor is not null && valor.Trim().Length > max)
            {
                return Result.Failure(new DomainError(
                    InstituicaoErrorCodes.CampoOpcionalTamanho,
                    $"Um dos campos opcionais excede o tamanho máximo de {max} caracteres."));
            }
        }

        return Result.Success();
    }
}
