namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Tipo de deficiência — cadastro institucional do tipo de deficiência reconhecido
/// (UNI-REQ-0012, módulo Configuração): Visual, Auditiva, TEA, Física, Intelectual…
/// É um cadastro classificatório simples (código + nome + descrição); a
/// solicitação concreta de atendimento especializado e os recursos de
/// acessibilidade são entidades distintas (vocabulário INEP/Edital ENEM 52/2025).
/// </summary>
/// <remarks>
/// <para>O <see cref="Codigo"/> (value object <see cref="CodigoTipoDeficiencia"/>)
/// é a identidade semântica exigida por UNI-REQ-0061 — é ele que os fatos de
/// atendimento congelam junto com a origem. É informado pelo operador (não há
/// geração automática no backend), único entre tipos vivos (índice único parcial
/// <c>WHERE is_deleted = false</c>) e <b>editável</b>, pois o consumo cross-módulo
/// é por snapshot-copy desacoplado (ADR-0061): editar o código vivo não altera o
/// que já foi congelado numa oferta.</para>
/// <para>O <c>Nome</c> segue sendo o rótulo legível, também único entre tipos
/// vivos e editável. A unicidade de ambos é checada pelo handler, com proteção de
/// corrida pelos índices (a violação 23505 é traduzida em <c>CodigoJaExiste</c> ou
/// <c>NomeJaExiste</c> conforme a constraint).</para>
/// <para>Dado institucional sem PII (LGPD inaplicável). A remoção é sempre
/// soft-delete e nunca bloqueada por referência.</para>
/// <para>
/// <c>Descricao</c> é <b>obrigatória</b> (ADR-0116): serve também como a
/// descrição por valor exigida pela spec para o fato <c>TIPO_DEFICIENCIA</c>
/// (<c>DECLARADO</c>), exposta via <c>ITipoDeficienciaReader</c>. <c>Permanente</c>
/// é anulável — <see langword="null"/> significa "ainda não classificado pelo
/// CEPS", distinto de <see langword="false"/> ("classificado como
/// não-permanente"); a taxonomia concreta é refinamento residual que não bloqueia
/// este modelo.
/// </para>
/// </remarks>
public sealed class TipoDeficiencia : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public CodigoTipoDeficiencia Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public bool? Permanente { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private TipoDeficiencia()
    {
    }

    /// <summary>
    /// Valida e normaliza código, nome e descrição (os três campos textuais
    /// editáveis), acumulando toda violação independente em vez de parar na
    /// primeira — sem mutar nada. Existe para o handler validar o payload por
    /// inteiro antes de qualquer I/O (consultas de unicidade, leitura por Id).
    /// </summary>
    public static Result<(CodigoTipoDeficiencia Codigo, string Nome, string Descricao)> ValidarCamposEditaveis(
        string? codigo, string? nome, string? descricao)
    {
        List<FieldError> erros = [];

        CodigoTipoDeficiencia? codigoValidado = null;
        Result<CodigoTipoDeficiencia> codigoResult = CodigoTipoDeficiencia.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            erros.Add(new("codigo", codigoResult.Error!));
        }
        else
        {
            codigoValidado = codigoResult.Value;
        }

        string? nomeNormalizado = null;
        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                TipoDeficienciaErrorCodes.NomeObrigatorio, "Nome do tipo de deficiência é obrigatório.")));
        }
        else
        {
            nomeNormalizado = nome.Trim();
            if (nomeNormalizado.Length is < NomeMinLength or > NomeMaxLength)
            {
                erros.Add(new("nome", new DomainError(
                    TipoDeficienciaErrorCodes.NomeTamanho,
                    $"Nome do tipo de deficiência deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")));
                nomeNormalizado = null;
            }
        }

        string? descricaoNormalizada = null;
        if (string.IsNullOrWhiteSpace(descricao))
        {
            erros.Add(new("descricao", new DomainError(
                TipoDeficienciaErrorCodes.DescricaoObrigatoria, "Descrição do tipo de deficiência é obrigatória.")));
        }
        else
        {
            descricaoNormalizada = descricao.Trim();
            if (descricaoNormalizada.Length > DescricaoMaxLength)
            {
                erros.Add(new("descricao", new DomainError(
                    TipoDeficienciaErrorCodes.DescricaoTamanho,
                    $"Descrição do tipo de deficiência deve ter no máximo {DescricaoMaxLength} caracteres.")));
                descricaoNormalizada = null;
            }
        }

        if (erros.Count > 0)
        {
            return Result<(CodigoTipoDeficiencia, string, string)>.ValidationFailure(erros);
        }

        return Result<(CodigoTipoDeficiencia, string, string)>.Success(
            (codigoValidado!, nomeNormalizado!, descricaoNormalizada!));
    }

    /// <summary>
    /// Cria um novo TipoDeficiencia. Revalida código, nome e descrição, acumulando
    /// toda violação no mesmo lote. A unicidade de <paramref name="codigo"/> e de
    /// <paramref name="nome"/> entre tipos vivos é responsabilidade do handler.
    /// </summary>
    public static Result<TipoDeficiencia> Criar(
        string? codigo, string? nome, string? descricao, bool? permanente = null)
    {
        Result<(CodigoTipoDeficiencia Codigo, string Nome, string Descricao)> campos =
            ValidarCamposEditaveis(codigo, nome, descricao);
        if (campos.IsFailure)
        {
            return Result<TipoDeficiencia>.ValidationFailure(campos.Errors);
        }

        var tipo = new TipoDeficiencia();
        tipo.AplicarCampos(campos.Value.Codigo, campos.Value.Nome, campos.Value.Descricao, permanente);

        return Result<TipoDeficiencia>.Success(tipo);
    }

    /// <summary>
    /// Atualiza os atributos do TipoDeficiencia. <c>Codigo</c> e <c>Nome</c> são
    /// editáveis; a unicidade de cada um (quando muda) é responsabilidade do
    /// handler. Revalida código, nome e descrição, acumulando toda violação no
    /// mesmo lote.
    /// </summary>
    public Result Atualizar(string? codigo, string? nome, string? descricao, bool? permanente = null)
    {
        Result<(CodigoTipoDeficiencia Codigo, string Nome, string Descricao)> campos =
            ValidarCamposEditaveis(codigo, nome, descricao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        AplicarCampos(campos.Value.Codigo, campos.Value.Nome, campos.Value.Descricao, permanente);

        return Result.Success();
    }

    private void AplicarCampos(CodigoTipoDeficiencia codigo, string nome, string descricao, bool? permanente)
    {
        Codigo = codigo;
        Nome = nome;
        Descricao = descricao;
        Permanente = permanente;
    }
}
