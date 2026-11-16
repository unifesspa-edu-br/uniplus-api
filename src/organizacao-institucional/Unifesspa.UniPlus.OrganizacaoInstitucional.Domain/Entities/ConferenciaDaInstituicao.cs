namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// As conferências dos campos da <see cref="Instituicao"/> — formato e domínio local, com a
/// referência de cidade e a coerência do endereço —, separadas do agregado para ele ficar com
/// o estado e as transições.
/// </summary>
internal static class ConferenciaDaInstituicao
{
    private const int CodigoEmecMaxLength = 20;
    private const int NomeMaxLength = 250;
    private const int SiglaMaxLength = 50;
    private const int OrganizacaoAcademicaMaxLength = 100;
    private const int CategoriaAdministrativaMaxLength = 100;
    private const int CampoOpcionalCurtoMaxLength = 100;
    private const int CampoOpcionalMedioMaxLength = 255;
    private const int CampoOpcionalLongoMaxLength = 500;

    /// <summary>
    /// Confere todos os campos da Instituição, acumulando cada violação em vez de parar na
    /// primeira (ADR-0125) — o <c>errors[]</c> do contrato público (ADR-0023) precisa de todas as
    /// regras violadas no mesmo lote. É o que <see cref="Instituicao.ValidarCampos"/> expõe ao handler.
    /// </summary>
    internal static Result Conferir(
        string? codigoEmec,
        string? nome,
        string? sigla,
        string? organizacaoAcademica,
        string? categoriaAdministrativa,
        string? cnpj,
        string? mantenedora,
        string? codigoMantenedoraEmec,
        string? situacao,
        string? atoCredenciamento,
        string? atoRecredenciamento,
        string? conceitoInstitucional,
        string? igc,
        string? website,
        ReferenciaEnderecoGeo? endereco,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf)
    {
        List<FieldError> erros = [];

        if (string.IsNullOrWhiteSpace(codigoEmec))
        {
            erros.Add(new("codigoEmec", new DomainError(
                InstituicaoErrorCodes.CodigoEmecObrigatorio, "Código e-MEC da Instituição é obrigatório.")));
        }
        else if (codigoEmec.Trim().Length > CodigoEmecMaxLength)
        {
            erros.Add(new("codigoEmec", new DomainError(
                InstituicaoErrorCodes.CodigoEmecTamanho,
                $"Código e-MEC deve ter no máximo {CodigoEmecMaxLength} caracteres.")));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                InstituicaoErrorCodes.NomeObrigatorio, "Nome da Instituição é obrigatório.")));
        }
        else if (nome.Trim().Length > NomeMaxLength)
        {
            erros.Add(new("nome", new DomainError(
                InstituicaoErrorCodes.NomeTamanho,
                $"Nome da Instituição deve ter no máximo {NomeMaxLength} caracteres.")));
        }

        if (string.IsNullOrWhiteSpace(sigla))
        {
            erros.Add(new("sigla", new DomainError(
                InstituicaoErrorCodes.SiglaObrigatoria, "Sigla da Instituição é obrigatória.")));
        }
        else if (sigla.Trim().Length > SiglaMaxLength)
        {
            erros.Add(new("sigla", new DomainError(
                InstituicaoErrorCodes.SiglaTamanho,
                $"Sigla da Instituição deve ter no máximo {SiglaMaxLength} caracteres.")));
        }

        if (string.IsNullOrWhiteSpace(organizacaoAcademica))
        {
            erros.Add(new("organizacaoAcademica", new DomainError(
                InstituicaoErrorCodes.OrganizacaoAcademicaObrigatoria,
                "Organização acadêmica da Instituição é obrigatória.")));
        }
        else if (organizacaoAcademica.Trim().Length > OrganizacaoAcademicaMaxLength)
        {
            erros.Add(new("organizacaoAcademica", new DomainError(
                InstituicaoErrorCodes.OrganizacaoAcademicaTamanho,
                $"Organização acadêmica deve ter no máximo {OrganizacaoAcademicaMaxLength} caracteres.")));
        }

        if (string.IsNullOrWhiteSpace(categoriaAdministrativa))
        {
            erros.Add(new("categoriaAdministrativa", new DomainError(
                InstituicaoErrorCodes.CategoriaAdministrativaObrigatoria,
                "Categoria administrativa da Instituição é obrigatória.")));
        }
        else if (categoriaAdministrativa.Trim().Length > CategoriaAdministrativaMaxLength)
        {
            erros.Add(new("categoriaAdministrativa", new DomainError(
                InstituicaoErrorCodes.CategoriaAdministrativaTamanho,
                $"Categoria administrativa deve ter no máximo {CategoriaAdministrativaMaxLength} caracteres.")));
        }

        bool cidadeOk = true;
        Result cidade = ValidarReferenciaCidade(cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (cidade.IsFailure)
        {
            cidadeOk = false;

            // ValidarReferenciaCidade acumula toda violação independente do trio de
            // cidade — cada uma mapeada para o campo real que descreve.
            foreach (FieldError erroCidade in cidade.Errors)
            {
                erros.Add(new(CampoDaCidade(erroCidade.Error.Code), erroCidade.Error));
            }
        }

        // Coerência endereço↔cidade só faz sentido com a referência de cidade já
        // confirmada válida — senão a causa raiz (formato do trio) ficaria
        // mascarada por "incoerente" ou por "cidade obrigatória".
        if (cidadeOk)
        {
            Result enderecoCoerente = ValidarCoerenciaEndereco(endereco, cidadeCodigoIbge, cidadeUf);
            if (enderecoCoerente.IsFailure)
            {
                erros.Add(new("endereco", enderecoCoerente.Error!));
            }
        }

        erros.AddRange(ValidarOpcionais(
            cnpj, mantenedora, codigoMantenedoraEmec, situacao, atoCredenciamento,
            atoRecredenciamento, conceitoInstitucional, igc, website));

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    /// <summary>
    /// Mapeia o código interno de <see cref="ReferenciaCidadeGeo.Validar"/> para o
    /// campo do payload (camelCase, ADR-0023) a que ele se refere de fato — sem
    /// isso, todo erro de cidade seria rotulado com o mesmo campo em
    /// <c>errors[].field</c>, mesmo quando a causa é o nome ou a UF, não o código
    /// IBGE.
    /// </summary>
    private static string CampoDaCidade(string codigoErro) => codigoErro switch
    {
        CidadeReferenciaErrorCodes.NomeObrigatorio
            or CidadeReferenciaErrorCodes.NomeCaractereInvalido
            or CidadeReferenciaErrorCodes.NomeTamanho => "cidadeNome",
        CidadeReferenciaErrorCodes.UfObrigatoria
            or CidadeReferenciaErrorCodes.UfIncoerente => "cidadeUf",
        _ => "cidadeCodigoIbge",
    };

    /// <summary>
    /// Como a cidade da sede é opcional all-or-nothing mas o CEP sempre resolve a
    /// uma cidade, um endereço presente exige a referência de cidade da sede
    /// preenchida e coerente com o snapshot de cidade do endereço — preservando a
    /// consistência do <c>InstituicaoView</c> cross-módulo, que expõe a cidade da
    /// sede.
    /// </summary>
    private static Result ValidarCoerenciaEndereco(
        ReferenciaEnderecoGeo? endereco,
        string? cidadeCodigoIbge,
        string? cidadeUf)
    {
        if (endereco is null)
        {
            return Result.Success();
        }

        if (string.IsNullOrWhiteSpace(cidadeCodigoIbge))
        {
            return Result.Failure(new DomainError(
                EnderecoReferenciaErrorCodes.CidadeObrigatoriaComEndereco,
                "A cidade da sede é obrigatória quando há endereço estruturado."));
        }

        return ReferenciaEnderecoGeo.ValidarCoerencia(
            endereco.CidadeCodigoIbge, endereco.CidadeUf, cidadeCodigoIbge, cidadeUf);
    }

    /// <summary>
    /// Valida a referência de cidade do Geo (ADR-0090) como opcional
    /// <strong>all-or-nothing</strong>: ausente por completo é válido; qualquer
    /// fragmento presente exige o trio (código IBGE + nome + UF) com formato e
    /// coerência de UF — delegado ao <see cref="ReferenciaCidadeGeo"/>, que
    /// reporta o campo faltante quando o preenchimento é parcial.
    /// </summary>
    private static Result ValidarReferenciaCidade(
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf)
    {
        bool algumPresente = !string.IsNullOrWhiteSpace(cidadeCodigoIbge)
            || !string.IsNullOrWhiteSpace(cidadeNome)
            || !string.IsNullOrWhiteSpace(cidadeUf);

        return algumPresente
            ? ReferenciaCidadeGeo.Validar(cidadeCodigoIbge, cidadeNome, cidadeUf)
            : Result.Success();
    }

    /// <summary>
    /// Confere o tamanho dos nove campos opcionais, cada um contribuindo no
    /// máximo um erro à lista — todos compartilham o mesmo código de domínio
    /// <see cref="InstituicaoErrorCodes.CampoOpcionalTamanho"/> (assim desenhado
    /// desde a origem), mas cada ocorrência carrega o rótulo do campo real em
    /// <see cref="FieldError.Field"/> para o cliente saber qual violou.
    /// </summary>
    private static List<FieldError> ValidarOpcionais(
        string? cnpj,
        string? mantenedora,
        string? codigoMantenedoraEmec,
        string? situacao,
        string? atoCredenciamento,
        string? atoRecredenciamento,
        string? conceitoInstitucional,
        string? igc,
        string? website)
    {
        (string campo, string? valor, int max)[] opcionais =
        [
            ("cnpj", cnpj, CampoOpcionalCurtoMaxLength),
            ("mantenedora", mantenedora, NomeMaxLength),
            ("codigoMantenedoraEmec", codigoMantenedoraEmec, CodigoEmecMaxLength),
            ("situacao", situacao, CampoOpcionalCurtoMaxLength),
            ("atoCredenciamento", atoCredenciamento, CampoOpcionalLongoMaxLength),
            ("atoRecredenciamento", atoRecredenciamento, CampoOpcionalLongoMaxLength),
            ("conceitoInstitucional", conceitoInstitucional, CampoOpcionalCurtoMaxLength),
            ("igc", igc, CampoOpcionalCurtoMaxLength),
            ("website", website, CampoOpcionalMedioMaxLength),
        ];

        List<FieldError> erros = [];
        foreach ((string campo, string? valor, int max) in opcionais)
        {
            if (valor is not null && valor.Trim().Length > max)
            {
                erros.Add(new(campo, new DomainError(
                    InstituicaoErrorCodes.CampoOpcionalTamanho,
                    $"Campo opcional excede o tamanho máximo de {max} caracteres.")));
            }
        }

        return erros;
    }
}
