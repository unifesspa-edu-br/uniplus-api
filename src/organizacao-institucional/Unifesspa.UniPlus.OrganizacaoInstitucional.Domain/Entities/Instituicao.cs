namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
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
/// <para>O município da sede é uma <strong>referência de cidade do Geo</strong>
/// (ADR-0090): <c>CidadeCodigoIbge</c> (código IBGE de 7 dígitos) + display cache
/// (<c>CidadeNome</c>, <c>CidadeUf</c>), preenchido pelo frontend via composição
/// no cliente — sem FK cross-banco nem chamada ao Geo. É <strong>opcional</strong>
/// (all-or-nothing): ou o trio completo e coerente, ou ausente por completo.</para>
/// <para>O <see cref="Endereco"/> da sede é uma referência de endereço estruturado
/// ao Geo via CEP (<see cref="ReferenciaEnderecoGeo"/>, ADR-0096), opcional —
/// sucede o antigo <c>EnderecoSede</c> texto livre. O Geo <strong>modela</strong>
/// endereço pontual (lookup de CEP #676, busca de logradouro #707); quando há
/// endereço, a referência de cidade da sede é obrigatória e coerente com ele.</para>
/// </remarks>
public sealed class Instituicao : SoftDeletableEntity, IAuditableEntity
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

    // Referência de cidade do Geo (ADR-0090) — código + display cache, opcional
    // (all-or-nothing). Substitui o antigo MunicipioSede texto livre.
    public string? CidadeCodigoIbge { get; private set; }
    public string? CidadeNome { get; private set; }
    public string? CidadeUf { get; private set; }
    public string? CidadeOrigem { get; private set; }
    public DateTimeOffset? CidadeDisplayAtualizadoEm { get; private set; }

    // Endereço estruturado ao Geo via CEP (ADR-0096) — opcional, owned type.
    // Sucede o antigo EnderecoSede texto livre. Quando presente, exige a cidade
    // da sede preenchida e coerente.
    public ReferenciaEnderecoGeo? Endereco { get; private set; }

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
    /// Cria a Instituição. Valida apenas formato e domínio local (incluindo a
    /// referência de cidade opcional via <see cref="ReferenciaCidadeGeo"/>) — o
    /// invariante singleton (no máximo uma viva) e a conferência do tipo da
    /// Unidade raiz são responsabilidade do handler chamador (dependem do
    /// repositório). A proveniência/frescura do display cache
    /// (<paramref name="cidadeOrigem"/>, <paramref name="cidadeDisplayAtualizadoEm"/>)
    /// é carimbada server-side pelo handler (ADR-0090).
    /// </summary>
    public static Result<Instituicao> Criar(
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
        string? cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        Guid? unidadeRaizId)
    {
        Result validacao = ValidarCampos(
            codigoEmec, nome, sigla, organizacaoAcademica, categoriaAdministrativa,
            cnpj, mantenedora, codigoMantenedoraEmec, situacao, atoCredenciamento,
            atoRecredenciamento, conceitoInstitucional, igc, website, endereco,
            cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (validacao.IsFailure)
        {
            return Result<Instituicao>.ValidationFailure(validacao.Errors);
        }

        var instituicao = new Instituicao();
        instituicao.AplicarCampos(
            codigoEmec!, nome!, sigla!, organizacaoAcademica!, categoriaAdministrativa!,
            cnpj, mantenedora, codigoMantenedoraEmec, situacao, atoCredenciamento,
            atoRecredenciamento, conceitoInstitucional, igc, website, endereco,
            cidadeCodigoIbge, cidadeNome, cidadeUf, cidadeOrigem, cidadeDisplayAtualizadoEm,
            unidadeRaizId);

        return Result<Instituicao>.Success(instituicao);
    }

    /// <summary>
    /// Atualiza os dados regulatórios e o vínculo com a reitoria. A conferência
    /// do tipo da Unidade raiz informada é responsabilidade do handler, assim
    /// como o carimbo da proveniência/frescura do display cache da cidade.
    /// </summary>
    public Result Atualizar(
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
        string? cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
        Guid? unidadeRaizId)
    {
        Result validacao = ValidarCampos(
            codigoEmec, nome, sigla, organizacaoAcademica, categoriaAdministrativa,
            cnpj, mantenedora, codigoMantenedoraEmec, situacao, atoCredenciamento,
            atoRecredenciamento, conceitoInstitucional, igc, website, endereco,
            cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        AplicarCampos(
            codigoEmec!, nome!, sigla!, organizacaoAcademica!, categoriaAdministrativa!,
            cnpj, mantenedora, codigoMantenedoraEmec, situacao, atoCredenciamento,
            atoRecredenciamento, conceitoInstitucional, igc, website, endereco,
            cidadeCodigoIbge, cidadeNome, cidadeUf, cidadeOrigem, cidadeDisplayAtualizadoEm,
            unidadeRaizId);

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
        ReferenciaEnderecoGeo? endereco,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm,
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
        Endereco = endereco;
        AplicarReferenciaCidade(
            cidadeCodigoIbge, cidadeNome, cidadeUf, cidadeOrigem, cidadeDisplayAtualizadoEm);
        UnidadeRaizId = unidadeRaizId;
    }

    /// <summary>
    /// Normaliza a referência de cidade já validada (all-or-nothing): com cidade
    /// presente grava o trio normalizado (UF em caixa alta) + display cache; sem
    /// cidade, zera os cinco campos. O gate canônico é o código IBGE — a validação
    /// garante que, se qualquer fragmento veio, o trio completo veio.
    /// </summary>
    private void AplicarReferenciaCidade(
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf,
        string? cidadeOrigem,
        DateTimeOffset? cidadeDisplayAtualizadoEm)
    {
        bool temCidade = !string.IsNullOrWhiteSpace(cidadeCodigoIbge);
        CidadeCodigoIbge = temCidade ? cidadeCodigoIbge!.Trim() : null;
        CidadeNome = temCidade ? cidadeNome!.Trim() : null;
        CidadeUf = temCidade ? cidadeUf!.Trim().ToUpperInvariant() : null;
        CidadeOrigem = temCidade ? NormalizarOpcional(cidadeOrigem) : null;
        CidadeDisplayAtualizadoEm = temCidade ? cidadeDisplayAtualizadoEm : null;
    }

    private static string? NormalizarOpcional(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        return valor.Trim();
    }

    /// <summary>
    /// Valida todos os campos da Instituição, acumulando cada violação em vez de
    /// retornar na primeira (ADR-0125) — o array <c>errors[]</c> do contrato
    /// público (ADR-0023) precisa de todas as regras de campo violadas no mesmo
    /// lote, não só a primeira. Público para o handler de atualização poder
    /// validar o payload inteiro antes de buscar o registro por Id (validação
    /// sempre vence 404) — mesmo uso que o handler de criação faz antes do guard
    /// de singleton e do vínculo com a Unidade raiz.
    /// </summary>
    public static Result ValidarCampos(
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
            or CidadeReferenciaErrorCodes.NomeCaractereNulo
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
