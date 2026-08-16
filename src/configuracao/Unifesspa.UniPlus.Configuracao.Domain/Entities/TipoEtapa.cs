namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Text;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cadastro institucional de tipos de etapa de processo seletivo (UNI-REQ-0015, UNI-REQ-0087).
/// </summary>
/// <remarks>
/// O código é chave natural imutável e reservado para sempre: desativar impede
/// novos vínculos, mas não libera o código. Consumidores cross-módulo recebem
/// somente itens ativos e congelam sua cópia por valor (ADR-0061).
/// </remarks>
public sealed class TipoEtapa : EntityBase, IAuditableEntity
{
    private const int CodigoMaxLength = 64;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    private const char CaractereNulo = (char)0;

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public bool Ativo { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    private TipoEtapa() { }

    /// <summary>
    /// Valida o código (formato + tamanho), sem mutar nada — existe para o handler
    /// de criação decidir se vale a pena consultar a unicidade antes mesmo de
    /// chamar <see cref="Criar"/>, que revalida por conta própria.
    /// </summary>
    public static Result<string> ValidarCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result<string>.ValidationFailure([new("codigo", new DomainError(
                TipoEtapaErrorCodes.CodigoObrigatorio, "Código do tipo de etapa é obrigatório."))]);
        }

        // NFC além do Trim: sem normalizar aqui, duas grafias Unicode do mesmo texto (forma
        // composta e decomposta) cadastrariam como códigos DIFERENTES — o índice único do banco
        // é ordinal/binário, então as duas colidiriam com a checagem de duplicidade e, pior,
        // um lookup por código (TipoEtapaReader) feito com a outra forma nunca encontraria este
        // registro, mesmo sendo textualmente "o mesmo" código.
        string codigoNormalizado = codigo.Trim().Normalize(NormalizationForm.FormC);
        if (codigoNormalizado.Contains(CaractereNulo))
        {
            return Result<string>.ValidationFailure([new("codigo", new DomainError(
                TipoEtapaErrorCodes.CodigoComCaractereNulo,
                "Código do tipo de etapa não pode conter o caractere nulo (U+0000)."))]);
        }

        if (codigoNormalizado.Length > CodigoMaxLength)
        {
            return Result<string>.ValidationFailure([new("codigo", new DomainError(
                TipoEtapaErrorCodes.CodigoTamanho,
                $"Código do tipo de etapa deve ter no máximo {CodigoMaxLength} caracteres."))]);
        }

        return Result<string>.Success(codigoNormalizado);
    }

    /// <summary>
    /// Valida nome e descrição (os dois campos editáveis), acumulando toda
    /// violação independente em vez de parar na primeira — sem mutar nada. O
    /// código não participa: é imutável, então <see cref="Atualizar"/> e seu
    /// handler nunca precisam revalidá-lo.
    /// </summary>
    public static Result<(string Nome, string? Descricao)> ValidarCamposEditaveis(string? nome, string? descricao)
    {
        List<FieldError> erros = [];

        string? nomeNormalizado = null;
        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                TipoEtapaErrorCodes.NomeObrigatorio, "Nome do tipo de etapa é obrigatório.")));
        }
        else
        {
            nomeNormalizado = nome.Trim().Normalize(NormalizationForm.FormC);
            if (nomeNormalizado.Contains(CaractereNulo))
            {
                erros.Add(new("nome", new DomainError(
                    TipoEtapaErrorCodes.NomeComCaractereNulo,
                    "Nome do tipo de etapa não pode conter o caractere nulo (U+0000).")));
                nomeNormalizado = null;
            }
            else if (nomeNormalizado.Length > NomeMaxLength)
            {
                erros.Add(new("nome", new DomainError(
                    TipoEtapaErrorCodes.NomeTamanho,
                    $"Nome do tipo de etapa deve ter no máximo {NomeMaxLength} caracteres.")));
                nomeNormalizado = null;
            }
        }

        string? descricaoNormalizada = NormalizarOpcional(descricao);
        if (descricaoNormalizada is not null)
        {
            if (descricaoNormalizada.Contains(CaractereNulo))
            {
                erros.Add(new("descricao", new DomainError(
                    TipoEtapaErrorCodes.DescricaoComCaractereNulo,
                    "Descrição do tipo de etapa não pode conter o caractere nulo (U+0000).")));
            }
            else if (descricaoNormalizada.Length > DescricaoMaxLength)
            {
                erros.Add(new("descricao", new DomainError(
                    TipoEtapaErrorCodes.DescricaoTamanho,
                    $"Descrição do tipo de etapa deve ter no máximo {DescricaoMaxLength} caracteres.")));
            }
        }

        if (erros.Count > 0)
        {
            return Result<(string, string?)>.ValidationFailure(erros);
        }

        return Result<(string, string?)>.Success((nomeNormalizado!, descricaoNormalizada));
    }

    /// <summary>
    /// Cria um novo TipoEtapa. Revalida código, nome e descrição, acumulando toda
    /// violação no mesmo lote. A unicidade do código é responsabilidade do
    /// handler.
    /// </summary>
    public static Result<TipoEtapa> Criar(string? codigo, string? nome, string? descricao)
    {
        List<FieldError> erros = [];

        Result<string> codigoResult = ValidarCodigo(codigo);
        if (codigoResult.IsFailure)
        {
            erros.AddRange(codigoResult.Errors);
        }

        Result<(string Nome, string? Descricao)> camposResult = ValidarCamposEditaveis(nome, descricao);
        if (camposResult.IsFailure)
        {
            erros.AddRange(camposResult.Errors);
        }

        if (erros.Count > 0)
        {
            return Result<TipoEtapa>.ValidationFailure(erros);
        }

        return Result<TipoEtapa>.Success(new TipoEtapa
        {
            Codigo = codigoResult.Value!,
            Nome = camposResult.Value.Nome,
            Descricao = camposResult.Value.Descricao,
            Ativo = true,
        });
    }

    /// <summary>Atualiza apenas os campos editáveis; o código permanece imutável.</summary>
    public Result Atualizar(string? nome, string? descricao)
    {
        Result<(string Nome, string? Descricao)> campos = ValidarCamposEditaveis(nome, descricao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        Nome = campos.Value.Nome;
        Descricao = campos.Value.Descricao;
        return Result.Success();
    }

    public Result Desativar()
    {
        if (!Ativo)
        {
            return Result.Failure(new DomainError(
                TipoEtapaErrorCodes.JaDesativado,
                "Tipo de etapa já está desativado."));
        }

        Ativo = false;
        return Result.Success();
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().Normalize(NormalizationForm.FormC);
}
