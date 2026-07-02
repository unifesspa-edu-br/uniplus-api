namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Oferta de Curso — a instância <b>regulatória</b> da oferta acadêmica
/// (story #588, issue #749, ADR-0066): liga um <see cref="Curso"/> (matriz
/// curricular pura) a um <see cref="LocalOferta"/> e à unidade ofertante
/// (instituto/faculdade, snapshot-copy — ADR-0061), carregando os atributos
/// que variam por campus: código e-MEC, código no sistema de gestão acadêmica,
/// programa, formato pedagógico, turno, teto de vagas e-MEC e base legal.
/// O mesmo curso tem ofertas distintas por campus, com códigos e-MEC diferentes.
/// </summary>
/// <remarks>
/// <para><see cref="CursoId"/>, <see cref="LocalOfertaId"/> e
/// <see cref="UnidadeOfertante"/> são <b>imutáveis</b> pós-criação: mudar
/// curso×local×unidade não é editar a oferta, é <i>outra</i> oferta —
/// <see cref="Atualizar"/> não os recebe nem os altera. A unidade ofertante é
/// snapshot-copy (ADR-0061): congelada da Unidade viva no ato da criação, sem FK.</para>
/// <para><see cref="VagasAnuaisAutorizadas"/> é o teto autorizado no e-MEC —
/// <b>não</b> são as vagas de um certame (essas pertencem ao edital, módulo
/// Seleção). A <see cref="BaseLegal"/> é obrigatória quando
/// <see cref="ProgramaDeOferta"/> ≠ <see cref="Enums.ProgramaDeOferta.Regular"/>
/// (guard revalidado na criação E na atualização — ex.: transição
/// Regular→Parfor sem base legal é rejeitada).</para>
/// <para>Não há chave natural única entre ofertas vivas — a repetição
/// curso×local×unidade é admitida (ex.: turnos ou programas distintos). A
/// remoção é soft-delete simples e <b>não</b> é bloqueada por snapshots
/// congelados em outros módulos (as cópias de Seleção são desacopladas —
/// ADR-0061). Dado institucional sem PII (LGPD inaplicável).</para>
/// </remarks>
public sealed class OfertaCurso : SoftDeletableEntity, IAuditableEntity
{
    private const int EMecCodigoMaxLength = 20;
    private const int CodigoSgaMaxLength = 30;
    private const int BaseLegalMaxLength = 500;
    private const int AtoAutorizacaoMecMaxLength = 300;

    public Guid CursoId { get; private set; }
    public Guid LocalOfertaId { get; private set; }

    /// <summary>Snapshot-copy da unidade ofertante (ADR-0061) — imutável pós-criação.</summary>
    public UnidadeOfertante UnidadeOfertante { get; private set; } = null!;

    public ProgramaDeOferta ProgramaDeOferta { get; private set; }
    public FormatoPedagogico FormatoPedagogico { get; private set; }
    public TurnoOferta? Turno { get; private set; }

    /// <summary>Código e-MEC da oferta por campus-sede (opcional).</summary>
    public string? EMecCodigo { get; private set; }

    /// <summary>Código no sistema de gestão acadêmica (nome vendor-neutral, opcional).</summary>
    public string? CodigoSga { get; private set; }

    /// <summary>Teto de vagas anuais autorizadas no e-MEC (≥ 0) — não são vagas de certame.</summary>
    public int? VagasAnuaisAutorizadas { get; private set; }

    /// <summary>Base legal da oferta — obrigatória quando o programa não é Regular (ADR-0066).</summary>
    public string? BaseLegal { get; private set; }

    public string? AtoAutorizacaoMec { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private OfertaCurso()
    {
    }

    /// <summary>
    /// Cria uma nova Oferta de Curso. Os enums chegam como tokens textuais
    /// (UPPER_SNAKE): <paramref name="programaDeOferta"/> é obrigatório;
    /// <paramref name="formatoPedagogico"/> aplica default PRESENCIAL quando
    /// ausente; <paramref name="turno"/> é opcional (nulo aceito). O
    /// <paramref name="unidadeOfertante"/> já chega congelado (resolvido pelo
    /// handler via <c>IUnidadeReader</c>). A existência viva do curso e do local
    /// de oferta é responsabilidade do handler.
    /// </summary>
    public static Result<OfertaCurso> Criar(
        Guid cursoId,
        Guid localOfertaId,
        UnidadeOfertante unidadeOfertante,
        string? programaDeOferta,
        string? formatoPedagogico,
        string? turno,
        string? eMecCodigo,
        string? codigoSga,
        int? vagasAnuaisAutorizadas,
        string? baseLegal,
        string? atoAutorizacaoMec)
    {
        ArgumentNullException.ThrowIfNull(unidadeOfertante);

        Result<CamposResolvidos> camposResult = ValidarComuns(
            programaDeOferta, formatoPedagogico, turno, eMecCodigo, codigoSga,
            vagasAnuaisAutorizadas, baseLegal, atoAutorizacaoMec);
        if (camposResult.IsFailure)
        {
            return Result<OfertaCurso>.Failure(camposResult.Error!);
        }

        var oferta = new OfertaCurso
        {
            CursoId = cursoId,
            LocalOfertaId = localOfertaId,
            UnidadeOfertante = unidadeOfertante,
        };
        oferta.AplicarCampos(camposResult.Value!);

        return Result<OfertaCurso>.Success(oferta);
    }

    /// <summary>
    /// Atualiza os atributos editáveis da Oferta de Curso: programa, formato
    /// pedagógico, turno, códigos (e-MEC / SGA), teto de vagas, base legal e ato
    /// de autorização. <c>CursoId</c>, <c>LocalOfertaId</c> e
    /// <c>UnidadeOfertante</c> são <b>imutáveis</b> — mudar curso×local×unidade
    /// caracteriza outra oferta, não uma edição; este método não os recebe nem os
    /// altera. Revalida o guard condicional da base legal na transição (ex.:
    /// Regular→Parfor sem base legal é rejeitado).
    /// </summary>
    public Result Atualizar(
        string? programaDeOferta,
        string? formatoPedagogico,
        string? turno,
        string? eMecCodigo,
        string? codigoSga,
        int? vagasAnuaisAutorizadas,
        string? baseLegal,
        string? atoAutorizacaoMec)
    {
        Result<CamposResolvidos> camposResult = ValidarComuns(
            programaDeOferta, formatoPedagogico, turno, eMecCodigo, codigoSga,
            vagasAnuaisAutorizadas, baseLegal, atoAutorizacaoMec);
        if (camposResult.IsFailure)
        {
            return Result.Failure(camposResult.Error!);
        }

        AplicarCampos(camposResult.Value!);

        return Result.Success();
    }

    private void AplicarCampos(CamposResolvidos campos)
    {
        ProgramaDeOferta = campos.ProgramaDeOferta;
        FormatoPedagogico = campos.FormatoPedagogico;
        Turno = campos.Turno;
        EMecCodigo = campos.EMecCodigo;
        CodigoSga = campos.CodigoSga;
        VagasAnuaisAutorizadas = campos.VagasAnuaisAutorizadas;
        BaseLegal = campos.BaseLegal;
        AtoAutorizacaoMec = campos.AtoAutorizacaoMec;
    }

    private static Result<CamposResolvidos> ValidarComuns(
        string? programaDeOfertaToken,
        string? formatoPedagogicoToken,
        string? turnoToken,
        string? eMecCodigo,
        string? codigoSga,
        int? vagasAnuaisAutorizadas,
        string? baseLegal,
        string? atoAutorizacaoMec)
    {
        // ProgramaDeOferta — obrigatório, sem default: a ausência é inválida.
        if (!ProgramasDeOferta.TryAnalisar(programaDeOfertaToken, out ProgramaDeOferta programa))
        {
            return Falha(OfertaCursoErrorCodes.ProgramaDeOfertaInvalido,
                $"Programa de oferta deve ser um de: {string.Join(", ", ProgramasDeOferta.TokensCanonicos)}.");
        }

        // FormatoPedagogico — obrigatório, default PRESENCIAL quando ausente
        // (mesmo expediente do default AMPLA de NaturezasLegais).
        FormatoPedagogico formato;
        if (string.IsNullOrWhiteSpace(formatoPedagogicoToken))
        {
            formato = FormatoPedagogico.Presencial;
        }
        else if (!FormatosPedagogicos.TryAnalisar(formatoPedagogicoToken, out formato))
        {
            return Falha(OfertaCursoErrorCodes.FormatoPedagogicoInvalido,
                $"Formato pedagógico deve ser um de: {string.Join(", ", FormatosPedagogicos.TokensCanonicos)}.");
        }

        // Turno — opcional (null quando ausente); quando informado, um dos quatro tokens.
        TurnoOferta? turno = null;
        if (!string.IsNullOrWhiteSpace(turnoToken))
        {
            if (!TurnosOferta.TryAnalisar(turnoToken, out TurnoOferta turnoResolvido))
            {
                return Falha(OfertaCursoErrorCodes.TurnoInvalido,
                    $"Turno da oferta deve ser um de: {string.Join(", ", TurnosOferta.TokensCanonicos)}.");
            }

            turno = turnoResolvido;
        }

        if (vagasAnuaisAutorizadas is < 0)
        {
            return Falha(OfertaCursoErrorCodes.VagasAnuaisNegativas,
                "Vagas anuais autorizadas não podem ser negativas (zero é aceito).");
        }

        if (eMecCodigo is not null && eMecCodigo.Trim().Length > EMecCodigoMaxLength)
        {
            return Falha(OfertaCursoErrorCodes.EMecCodigoTamanho,
                $"Código e-MEC da oferta deve ter no máximo {EMecCodigoMaxLength} caracteres.");
        }

        if (codigoSga is not null && codigoSga.Trim().Length > CodigoSgaMaxLength)
        {
            return Falha(OfertaCursoErrorCodes.CodigoSgaTamanho,
                $"Código no sistema de gestão acadêmica deve ter no máximo {CodigoSgaMaxLength} caracteres.");
        }

        if (baseLegal is not null && baseLegal.Trim().Length > BaseLegalMaxLength)
        {
            return Falha(OfertaCursoErrorCodes.BaseLegalTamanho,
                $"Base legal da oferta deve ter no máximo {BaseLegalMaxLength} caracteres.");
        }

        if (atoAutorizacaoMec is not null && atoAutorizacaoMec.Trim().Length > AtoAutorizacaoMecMaxLength)
        {
            return Falha(OfertaCursoErrorCodes.AtoAutorizacaoMecTamanho,
                $"Ato de autorização MEC deve ter no máximo {AtoAutorizacaoMecMaxLength} caracteres.");
        }

        string? baseLegalNorm = NormalizarOpcional(baseLegal);

        // Guard condicional (ADR-0066): programa fora do Regular exige base legal —
        // revalidado também na atualização (transição Regular→Parfor sem base falha).
        if (programa != ProgramaDeOferta.Regular && baseLegalNorm is null)
        {
            return Falha(OfertaCursoErrorCodes.BaseLegalObrigatoriaParaProgramaNaoRegular,
                "Base legal é obrigatória quando o programa de oferta não é REGULAR.");
        }

        return Result<CamposResolvidos>.Success(new CamposResolvidos(
            programa,
            formato,
            turno,
            NormalizarOpcional(eMecCodigo),
            NormalizarOpcional(codigoSga),
            vagasAnuaisAutorizadas,
            baseLegalNorm,
            NormalizarOpcional(atoAutorizacaoMec)));
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static Result<CamposResolvidos> Falha(string code, string mensagem) =>
        Result<CamposResolvidos>.Failure(new DomainError(code, mensagem));

    private sealed record CamposResolvidos(
        ProgramaDeOferta ProgramaDeOferta,
        FormatoPedagogico FormatoPedagogico,
        TurnoOferta? Turno,
        string? EMecCodigo,
        string? CodigoSga,
        int? VagasAnuaisAutorizadas,
        string? BaseLegal,
        string? AtoAutorizacaoMec);
}
