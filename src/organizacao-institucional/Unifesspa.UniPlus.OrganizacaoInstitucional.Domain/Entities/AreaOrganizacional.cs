namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Unidade institucional administradora de catálogos e governança (ADR-0055).
/// Os 5 codigos canônicos do roster inicial — CEPS, CRCA, PROEG, PROGEP,
/// PLATAFORMA — são adicionados via endpoint admin acompanhados de
/// <see cref="AdrReferenceCode"/>; novas áreas só entram com ADR
/// correspondente (closed roster, fitness test em F1.S3/#448).
/// </summary>
/// <remarks>
/// <para>Implementa <see cref="IAuditableEntity"/> porque registrar/editar uma
/// área é ato de governança auditado — <c>CreatedBy</c>/<c>UpdatedBy</c>
/// preenchidos pelo <c>AuditableInterceptor</c> a partir do <c>IUserContext</c>
/// do request.</para>
/// <para><strong>Não</strong> implementa <see cref="IAreaScopedEntity"/>:
/// <c>AreaOrganizacional</c> NÃO é "área-scoped"; ela é a própria dimensão de
/// governança. Não tem <c>Proprietario</c> nem <c>AreasDeInteresse</c>.</para>
/// </remarks>
public sealed partial class AreaOrganizacional : EntityBase, IAuditableEntity
{
    private const int NomeMaxLength = 120;
    private const int NomeMinLength = 2;
    private const int DescricaoMaxLength = 500;
    private const int AdrReferenceMaxLength = 200;

    /// <summary>
    /// Identificador strongly-typed da área (uppercase, alfanumérico + underscore).
    /// Imutável após criação (ADR-0057 §"Invariante 2") — renomear área exige criar
    /// outra com novo código e migrar referências.
    /// </summary>
    public AreaCodigo Codigo { get; private init; }

    public string Nome { get; private set; } = string.Empty;
    public TipoAreaOrganizacional Tipo { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public string AdrReferenceCode { get; private set; } = string.Empty;

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // Construtor sem parâmetros usado pelo EF Core (materialization).
    private AreaOrganizacional()
    {
    }

    /// <summary>
    /// Factory de criação. Valida formato de <paramref name="adrReferenceCode"/>
    /// (regex <c>^\d{4}-[a-z0-9]+(?:-[a-z0-9]+)*$</c>) — a validação "arquivo existe em
    /// <c>docs/adrs/</c>" é fitness test em CI (ADR-0055 §"Confirmação", Story #448).
    /// </summary>
    public static Result<AreaOrganizacional> Criar(
        AreaCodigo codigo,
        string nome,
        TipoAreaOrganizacional tipo,
        string descricao,
        string adrReferenceCode)
    {
        // Defesa em profundidade contra TipoAreaOrganizacional não definido (cast de
        // int fora do enum) ou `Nenhum` (sentinel default). Validator FluentValidation
        // cobre o caminho HTTP; este guard cobre construção via background job,
        // teste, ou command construído manualmente fora do pipeline padrão.
        if (!Enum.IsDefined(tipo) || tipo == TipoAreaOrganizacional.Nenhum)
        {
            return Result<AreaOrganizacional>.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.TipoInvalido,
                "Tipo de área organizacional inválido — use um valor definido em TipoAreaOrganizacional, diferente de Nenhum."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<AreaOrganizacional>.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.NomeObrigatorio,
                "Nome da área organizacional é obrigatório."));
        }

        string nomeNormalizado = nome.Trim();
        if (nomeNormalizado.Length is < NomeMinLength or > NomeMaxLength)
        {
            return Result<AreaOrganizacional>.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.NomeTamanho,
                $"Nome deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            return Result<AreaOrganizacional>.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.DescricaoObrigatoria,
                "Descrição da área organizacional é obrigatória."));
        }

        string descricaoNormalizada = descricao.Trim();
        if (descricaoNormalizada.Length > DescricaoMaxLength)
        {
            return Result<AreaOrganizacional>.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.DescricaoTamanho,
                $"Descrição não pode ter mais que {DescricaoMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(adrReferenceCode))
        {
            return Result<AreaOrganizacional>.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.AdrReferenceObrigatorio,
                "AdrReferenceCode é obrigatório — adicionar área exige ADR (closed roster, ADR-0055)."));
        }

        string adrNormalizado = adrReferenceCode.Trim();
        if (adrNormalizado.Length > AdrReferenceMaxLength
            || !AdrReferenceFormat().IsMatch(adrNormalizado))
        {
            return Result<AreaOrganizacional>.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.AdrReferenceFormatoInvalido,
                "AdrReferenceCode deve seguir o padrão 'NNNN-slug-em-kebab' (ex.: '0055-organizacao-institucional-bounded-context')."));
        }

        return Result<AreaOrganizacional>.Success(new AreaOrganizacional
        {
            Codigo = codigo,
            Nome = nomeNormalizado,
            Tipo = tipo,
            Descricao = descricaoNormalizada,
            AdrReferenceCode = adrNormalizado,
        });
    }

    /// <summary>
    /// Atualiza atributos editáveis. <see cref="Codigo"/> é imutável
    /// (ADR-0057 §"Invariante 2") — não há setter público nem rota admin
    /// para esse campo.
    /// </summary>
    public Result Atualizar(string nome, TipoAreaOrganizacional tipo, string descricao)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.NomeObrigatorio,
                "Nome da área organizacional é obrigatório."));
        }

        string nomeNormalizado = nome.Trim();
        if (nomeNormalizado.Length is < NomeMinLength or > NomeMaxLength)
        {
            return Result.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.NomeTamanho,
                $"Nome deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            return Result.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.DescricaoObrigatoria,
                "Descrição da área organizacional é obrigatória."));
        }

        string descricaoNormalizada = descricao.Trim();
        if (descricaoNormalizada.Length > DescricaoMaxLength)
        {
            return Result.Failure(new DomainError(
                AreaOrganizacionalErrorCodes.DescricaoTamanho,
                $"Descrição não pode ter mais que {DescricaoMaxLength} caracteres."));
        }

        Nome = nomeNormalizado;
        Tipo = tipo;
        Descricao = descricaoNormalizada;
        return Result.Success();
    }

    // Padrão estabelecido pela seção "Aceitação de novas ADRs" do guia
    // de ADRs: 4 dígitos numéricos + hífen + slug em kebab. Aceita
    // "0055-foo" e "0055-foo-bar"; rejeita "0055-", "0055--", "0055-foo-".
    // Validação só de FORMA — verificação de existência do arquivo em
    // docs/adrs/ é fitness test em CI, fora do domínio.
    [GeneratedRegex(@"^\d{4}-[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex AdrReferenceFormat();
}
