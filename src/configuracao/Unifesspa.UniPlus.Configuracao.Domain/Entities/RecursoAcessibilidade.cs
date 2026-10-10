namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Recurso de acessibilidade — cadastro institucional do <b>apoio concreto</b>
/// oferecido no atendimento especializado (UNI-REQ-0012, módulo Configuração):
/// nomeia o recurso disponibilizado ao candidato (ledor, tempo adicional, prova
/// ampliada, intérprete de Libras, sala reservada…), nunca uma regra material
/// sobre quando ou a quem se aplica — essas regras vivem na exigência/solicitação
/// de atendimento do edital (banco de Seleção).
/// </summary>
/// <remarks>
/// <para>O <see cref="Codigo"/> (value object <see cref="CodigoRecursoAcessibilidade"/>)
/// é a identidade semântica exigida por UNI-REQ-0061: é ele que a oferta do processo
/// congela junto com a origem e que o candidato escolhe no fato
/// <c>RECURSO_ACESSIBILIDADE</c>. É informado pelo operador, único entre recursos vivos
/// (índice único parcial <c>WHERE is_deleted = false</c>) e <b>editável</b>, pois o
/// consumo cross-módulo é por snapshot-copy desacoplado (ADR-0061): editar o código
/// vivo não altera o que já foi congelado numa oferta.</para>
/// <para>O <c>Nome</c> segue sendo o rótulo legível, também único entre recursos vivos
/// e editável. A unicidade de ambos é checada pelo handler, com proteção de corrida
/// pelos índices (a violação 23505 é traduzida em <c>CodigoJaExiste</c> ou
/// <c>NomeJaExiste</c> conforme a constraint).</para>
/// <para>Dado institucional sem PII (LGPD inaplicável). A remoção é sempre
/// soft-delete e nunca bloqueada por referência.</para>
/// </remarks>
public sealed class RecursoAcessibilidade : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public CodigoRecursoAcessibilidade Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private RecursoAcessibilidade()
    {
    }

    /// <summary>
    /// Valida e normaliza código, nome e descrição, acumulando toda violação
    /// independente em vez de parar na primeira — sem mutar nada. Existe para o
    /// handler validar o payload por inteiro antes de qualquer I/O (consultas de
    /// unicidade, leitura por Id).
    /// </summary>
    public static Result<(CodigoRecursoAcessibilidade Codigo, string Nome, string? Descricao)> ValidarCamposEditaveis(
        string? codigo, string? nome, string? descricao)
    {
        List<FieldError> erros = [];

        CodigoRecursoAcessibilidade? codigoValidado = null;
        Result<CodigoRecursoAcessibilidade> codigoResult = CodigoRecursoAcessibilidade.Criar(codigo);
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
                RecursoAcessibilidadeErrorCodes.NomeObrigatorio,
                "Nome do recurso de acessibilidade é obrigatório.")));
        }
        else
        {
            nomeNormalizado = nome.Trim();
            if (nomeNormalizado.Length is < NomeMinLength or > NomeMaxLength)
            {
                erros.Add(new("nome", new DomainError(
                    RecursoAcessibilidadeErrorCodes.NomeTamanho,
                    $"Nome do recurso de acessibilidade deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")));
                nomeNormalizado = null;
            }
        }

        string? descricaoNormalizada = NormalizarOpcional(descricao);
        if (descricaoNormalizada is not null && descricaoNormalizada.Length > DescricaoMaxLength)
        {
            erros.Add(new("descricao", new DomainError(
                RecursoAcessibilidadeErrorCodes.DescricaoTamanho,
                $"Descrição do recurso de acessibilidade deve ter no máximo {DescricaoMaxLength} caracteres.")));
            descricaoNormalizada = null;
        }

        if (erros.Count > 0)
        {
            return Result<(CodigoRecursoAcessibilidade, string, string?)>.ValidationFailure(erros);
        }

        return Result<(CodigoRecursoAcessibilidade, string, string?)>.Success(
            (codigoValidado!, nomeNormalizado!, descricaoNormalizada));
    }

    /// <summary>
    /// Cria um novo RecursoAcessibilidade. Revalida código, nome e descrição,
    /// acumulando toda violação no mesmo lote. A unicidade de <paramref name="codigo"/>
    /// e de <paramref name="nome"/> entre recursos vivos é responsabilidade do handler.
    /// </summary>
    public static Result<RecursoAcessibilidade> Criar(string? codigo, string? nome, string? descricao)
    {
        Result<(CodigoRecursoAcessibilidade Codigo, string Nome, string? Descricao)> campos =
            ValidarCamposEditaveis(codigo, nome, descricao);
        if (campos.IsFailure)
        {
            return Result<RecursoAcessibilidade>.ValidationFailure(campos.Errors);
        }

        var recurso = new RecursoAcessibilidade();
        recurso.AplicarCampos(campos.Value.Codigo, campos.Value.Nome, campos.Value.Descricao);

        return Result<RecursoAcessibilidade>.Success(recurso);
    }

    /// <summary>
    /// Atualiza os atributos do RecursoAcessibilidade. <c>Codigo</c> e <c>Nome</c> são
    /// editáveis; a unicidade de cada um (quando muda) é responsabilidade do handler.
    /// Revalida código, nome e descrição, acumulando toda violação no mesmo lote.
    /// </summary>
    public Result Atualizar(string? codigo, string? nome, string? descricao)
    {
        Result<(CodigoRecursoAcessibilidade Codigo, string Nome, string? Descricao)> campos =
            ValidarCamposEditaveis(codigo, nome, descricao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        AplicarCampos(campos.Value.Codigo, campos.Value.Nome, campos.Value.Descricao);

        return Result.Success();
    }

    private void AplicarCampos(CodigoRecursoAcessibilidade codigo, string nome, string? descricao)
    {
        Codigo = codigo;
        Nome = nome;
        Descricao = descricao;
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
