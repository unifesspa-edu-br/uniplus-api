namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Categoria de documento — cadastro institucional classificatório (UNI-REQ-0013,
/// módulo Configuração): organiza o catálogo de tipos de documento em blocos
/// navegáveis (renda, identificação, escolaridade…). É rótulo de organização, não
/// invariante de domínio: nada no fluxo de seleção depende de a categoria ser uma
/// de um conjunto fixo, e por isso ela é cadastro administrado, não vocabulário
/// fechado em código. Dado institucional sem PII (LGPD inaplicável).
/// </summary>
/// <remarks>
/// <para>O <see cref="Codigo"/> (value object <see cref="CodigoCategoriaDocumento"/>)
/// é a chave natural, única entre categorias vivas (índice único parcial
/// <c>WHERE is_deleted = false</c>) — e <b>editável</b>, pois o consumo
/// cross-módulo é por snapshot-copy desacoplado (ADR-0061): editar o código vivo
/// não altera o rótulo já congelado no envelope de um edital publicado. A
/// unicidade é checada pelo handler (com proteção de corrida via índice).</para>
/// <para>A <see cref="Ordem"/> é o critério de exibição do catálogo — permite ao
/// operador dispor as categorias na sequência que faz sentido para quem preenche
/// o cadastro, em vez de depender da ordem alfabética do código.</para>
/// <para>Nenhuma categoria é reservada: a remoção é sempre soft-delete e nunca
/// bloqueada por referência, e o soft-delete libera o código para novo cadastro.</para>
/// </remarks>
public sealed class CategoriaDocumento : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public CodigoCategoriaDocumento Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }

    /// <summary>Posição de exibição no catálogo — não negativa; ausente equivale a zero.</summary>
    public int Ordem { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private CategoriaDocumento()
    {
    }

    /// <summary>
    /// Valida e normaliza código, nome, descrição e ordem (os campos editáveis),
    /// acumulando toda violação independente em vez de parar na primeira — sem
    /// mutar nada. Existe para o handler validar o payload por inteiro antes de
    /// qualquer I/O (checagem de unicidade do código, leitura por Id).
    /// </summary>
    public static Result<(CodigoCategoriaDocumento Codigo, string Nome, string? Descricao, int Ordem)> ValidarCamposEditaveis(
        string? codigo, string? nome, string? descricao, int? ordem)
    {
        List<FieldError> erros = [];

        CodigoCategoriaDocumento? codigoValidado = null;
        Result<CodigoCategoriaDocumento> codigoResult = CodigoCategoriaDocumento.Criar(codigo);
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
                CategoriaDocumentoErrorCodes.NomeObrigatorio,
                "Nome da categoria de documento é obrigatório.")));
        }
        else
        {
            nomeNormalizado = nome.Trim();
            if (nomeNormalizado.Length is < NomeMinLength or > NomeMaxLength)
            {
                erros.Add(new("nome", new DomainError(
                    CategoriaDocumentoErrorCodes.NomeTamanho,
                    $"Nome da categoria deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")));
                nomeNormalizado = null;
            }
        }

        string? descricaoNormalizada = NormalizarOpcional(descricao);
        if (descricaoNormalizada is not null && descricaoNormalizada.Length > DescricaoMaxLength)
        {
            erros.Add(new("descricao", new DomainError(
                CategoriaDocumentoErrorCodes.DescricaoTamanho,
                $"Descrição da categoria deve ter no máximo {DescricaoMaxLength} caracteres.")));
            descricaoNormalizada = null;
        }

        // Ordem ausente equivale a zero: o operador só precisa informá-la quando
        // quer posicionar a categoria fora do fim natural do catálogo.
        int ordemNormalizada = ordem ?? 0;
        if (ordemNormalizada < 0)
        {
            erros.Add(new("ordem", new DomainError(
                CategoriaDocumentoErrorCodes.OrdemInvalida,
                "Ordem de exibição da categoria não pode ser negativa.")));
            ordemNormalizada = 0;
        }

        if (erros.Count > 0)
        {
            return Result<(CodigoCategoriaDocumento, string, string?, int)>.ValidationFailure(erros);
        }

        return Result<(CodigoCategoriaDocumento, string, string?, int)>.Success(
            (codigoValidado!, nomeNormalizado!, descricaoNormalizada, ordemNormalizada));
    }

    /// <summary>
    /// Cria uma nova categoria de documento. Revalida todos os campos editáveis,
    /// acumulando toda violação no mesmo lote. A unicidade do código entre
    /// categorias vivas é responsabilidade do handler.
    /// </summary>
    public static Result<CategoriaDocumento> Criar(string? codigo, string? nome, string? descricao, int? ordem)
    {
        Result<(CodigoCategoriaDocumento Codigo, string Nome, string? Descricao, int Ordem)> campos =
            ValidarCamposEditaveis(codigo, nome, descricao, ordem);
        if (campos.IsFailure)
        {
            return Result<CategoriaDocumento>.ValidationFailure(campos.Errors);
        }

        var categoria = new CategoriaDocumento();
        categoria.AplicarCampos(campos.Value.Codigo, campos.Value.Nome, campos.Value.Descricao, campos.Value.Ordem);

        return Result<CategoriaDocumento>.Success(categoria);
    }

    /// <summary>
    /// Atualiza os atributos da categoria. O <c>Codigo</c> é editável e sua
    /// unicidade (quando alterado) é responsabilidade do handler. Revalida todos
    /// os campos editáveis, acumulando toda violação no mesmo lote.
    /// </summary>
    public Result Atualizar(string? codigo, string? nome, string? descricao, int? ordem)
    {
        Result<(CodigoCategoriaDocumento Codigo, string Nome, string? Descricao, int Ordem)> campos =
            ValidarCamposEditaveis(codigo, nome, descricao, ordem);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        AplicarCampos(campos.Value.Codigo, campos.Value.Nome, campos.Value.Descricao, campos.Value.Ordem);

        return Result.Success();
    }

    private void AplicarCampos(CodigoCategoriaDocumento codigo, string nome, string? descricao, int ordem)
    {
        Codigo = codigo;
        Nome = nome;
        Descricao = descricao;
        Ordem = ordem;
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
