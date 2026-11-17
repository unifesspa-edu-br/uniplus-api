namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

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

    private static readonly RegraDoCampoObrigatorio[] RegrasObrigatorias =
    [
        new("codigoEmec", static i => i.CodigoEmec, CodigoEmecMaxLength,
            new DomainError(InstituicaoErrorCodes.CodigoEmecObrigatorio, "Código e-MEC da Instituição é obrigatório."),
            new DomainError(InstituicaoErrorCodes.CodigoEmecTamanho, $"Código e-MEC deve ter no máximo {CodigoEmecMaxLength} caracteres.")),
        new("nome", static i => i.Nome, NomeMaxLength,
            new DomainError(InstituicaoErrorCodes.NomeObrigatorio, "Nome da Instituição é obrigatório."),
            new DomainError(InstituicaoErrorCodes.NomeTamanho, $"Nome da Instituição deve ter no máximo {NomeMaxLength} caracteres.")),
        new("sigla", static i => i.Sigla, SiglaMaxLength,
            new DomainError(InstituicaoErrorCodes.SiglaObrigatoria, "Sigla da Instituição é obrigatória."),
            new DomainError(InstituicaoErrorCodes.SiglaTamanho, $"Sigla da Instituição deve ter no máximo {SiglaMaxLength} caracteres.")),
        new("organizacaoAcademica", static i => i.OrganizacaoAcademica, OrganizacaoAcademicaMaxLength,
            new DomainError(InstituicaoErrorCodes.OrganizacaoAcademicaObrigatoria, "Organização acadêmica da Instituição é obrigatória."),
            new DomainError(
                InstituicaoErrorCodes.OrganizacaoAcademicaTamanho,
                $"Organização acadêmica deve ter no máximo {OrganizacaoAcademicaMaxLength} caracteres.")),
        new("categoriaAdministrativa", static i => i.CategoriaAdministrativa, CategoriaAdministrativaMaxLength,
            new DomainError(InstituicaoErrorCodes.CategoriaAdministrativaObrigatoria, "Categoria administrativa da Instituição é obrigatória."),
            new DomainError(
                InstituicaoErrorCodes.CategoriaAdministrativaTamanho,
                $"Categoria administrativa deve ter no máximo {CategoriaAdministrativaMaxLength} caracteres.")),
    ];

    private static readonly RegraDoCampoOpcional[] RegrasOpcionais =
    [
        new("cnpj", static i => i.Cnpj, CampoOpcionalCurtoMaxLength),
        new("mantenedora", static i => i.Mantenedora, NomeMaxLength),
        new("codigoMantenedoraEmec", static i => i.CodigoMantenedoraEmec, CodigoEmecMaxLength),
        new("situacao", static i => i.Situacao, CampoOpcionalCurtoMaxLength),
        new("atoCredenciamento", static i => i.AtoCredenciamento, CampoOpcionalLongoMaxLength),
        new("atoRecredenciamento", static i => i.AtoRecredenciamento, CampoOpcionalLongoMaxLength),
        new("conceitoInstitucional", static i => i.ConceitoInstitucional, CampoOpcionalCurtoMaxLength),
        new("igc", static i => i.Igc, CampoOpcionalCurtoMaxLength),
        new("website", static i => i.Website, CampoOpcionalMedioMaxLength),
    ];

    /// <summary>
    /// Confere todos os campos da Instituição, acumulando cada violação em vez de parar na
    /// primeira (ADR-0125) — o <c>errors[]</c> do contrato público (ADR-0023) precisa de todas as
    /// regras violadas no mesmo lote, na ordem: obrigatórios, cidade, coerência do endereço e
    /// opcionais. É o que <see cref="Instituicao.ValidarCampos"/> expõe ao handler.
    /// </summary>
    internal static Result Conferir(
        IdentificacaoRegulatoriaDaInstituicao identificacao,
        CidadeDaSede cidade,
        ReferenciaEnderecoGeo? endereco)
    {
        List<FieldError> erros = [.. ConferirObrigatorios(identificacao)];
        IReadOnlyList<FieldError> errosDaCidade = cidade.Validar();
        erros.AddRange(errosDaCidade);

        // A coerência do endereço só faz sentido com a referência de cidade já confirmada válida:
        // senão a causa raiz (formato do trio) ficaria mascarada por "incoerente" ou por "cidade obrigatória".
        if (errosDaCidade.Count == 0)
        {
            erros.AddRange(ConferirCoerenciaDoEndereco(endereco, cidade));
        }

        erros.AddRange(ConferirOpcionais(identificacao));

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    private static List<FieldError> ConferirObrigatorios(IdentificacaoRegulatoriaDaInstituicao identificacao) =>
        [.. RegrasObrigatorias.Select(regra => regra.Conferir(identificacao)).OfType<FieldError>()];

    /// <summary>
    /// Cada campo opcional contribui no máximo um erro à lista. Todos compartilham o código de domínio
    /// <see cref="InstituicaoErrorCodes.CampoOpcionalTamanho"/>, e cada ocorrência carrega o nome do
    /// campo real em <see cref="FieldError.Field"/>, para o cliente saber qual violou.
    /// </summary>
    private static List<FieldError> ConferirOpcionais(IdentificacaoRegulatoriaDaInstituicao identificacao) =>
        [.. RegrasOpcionais.Select(regra => regra.Conferir(identificacao)).OfType<FieldError>()];

    /// <summary>
    /// Como a cidade da sede é opcional all-or-nothing mas o CEP sempre resolve a uma cidade, um
    /// endereço presente exige a referência de cidade da sede preenchida e coerente com o snapshot
    /// de cidade do endereço — preservando a consistência do <c>InstituicaoView</c> cross-módulo,
    /// que expõe a cidade da sede.
    /// </summary>
    private static List<FieldError> ConferirCoerenciaDoEndereco(ReferenciaEnderecoGeo? endereco, CidadeDaSede cidade)
    {
        if (endereco is null)
        {
            return [];
        }

        Result coerencia = cidade.Informada
            ? ReferenciaEnderecoGeo.ValidarCoerencia(endereco.CidadeCodigoIbge, endereco.CidadeUf, cidade.CodigoIbge, cidade.Uf)
            : Result.Failure(new DomainError(
                EnderecoReferenciaErrorCodes.CidadeObrigatoriaComEndereco,
                "A cidade da sede é obrigatória quando há endereço estruturado."));

        return coerencia.Error is { } erro ? [new("endereco", erro)] : [];
    }

    private sealed record RegraDoCampoObrigatorio(
        string Campo,
        Func<IdentificacaoRegulatoriaDaInstituicao, string?> Valor,
        int TamanhoMaximo,
        DomainError Ausente,
        DomainError AcimaDoTamanho)
    {
        internal FieldError? Conferir(IdentificacaoRegulatoriaDaInstituicao identificacao)
        {
            string? valor = Valor(identificacao);
            if (string.IsNullOrWhiteSpace(valor))
            {
                return new(Campo, Ausente);
            }

            return valor.Trim().Length > TamanhoMaximo ? new(Campo, AcimaDoTamanho) : null;
        }
    }

    private sealed record RegraDoCampoOpcional(
        string Campo,
        Func<IdentificacaoRegulatoriaDaInstituicao, string?> Valor,
        int TamanhoMaximo)
    {
        internal FieldError? Conferir(IdentificacaoRegulatoriaDaInstituicao identificacao) =>
            Valor(identificacao) is { } valor && valor.Trim().Length > TamanhoMaximo
                ? new(Campo, new DomainError(
                    InstituicaoErrorCodes.CampoOpcionalTamanho,
                    $"Campo opcional excede o tamanho máximo de {TamanhoMaximo} caracteres."))
                : null;
    }
}
