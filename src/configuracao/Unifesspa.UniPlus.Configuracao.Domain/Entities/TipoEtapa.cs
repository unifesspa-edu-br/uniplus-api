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

    public static Result<TipoEtapa> Criar(string codigo, string nome, string? descricao)
    {
        Result<Campos> campos = ValidarCampos(codigo, nome, descricao);
        if (campos.IsFailure)
        {
            return Result<TipoEtapa>.Failure(campos.Error!);
        }

        return Result<TipoEtapa>.Success(new TipoEtapa
        {
            Codigo = campos.Value!.Codigo,
            Nome = campos.Value.Nome,
            Descricao = campos.Value.Descricao,
            Ativo = true,
        });
    }

    /// <summary>Atualiza apenas os campos editáveis; o código permanece imutável.</summary>
    public Result Atualizar(string nome, string? descricao)
    {
        Result<Campos> campos = ValidarCampos(Codigo, nome, descricao);
        if (campos.IsFailure)
        {
            return Result.Failure(campos.Error!);
        }

        Nome = campos.Value!.Nome;
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

    private static Result<Campos> ValidarCampos(string codigo, string nome, string? descricao)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Falha(TipoEtapaErrorCodes.CodigoObrigatorio, "Código do tipo de etapa é obrigatório.");
        }

        // NFC além do Trim: sem normalizar aqui, duas grafias Unicode do mesmo texto (forma
        // composta e decomposta) cadastrariam como códigos DIFERENTES — o índice único do banco
        // é ordinal/binário, então as duas colidiriam com a checagem de duplicidade e, pior,
        // um lookup por código (TipoEtapaReader) feito com a outra forma nunca encontraria este
        // registro, mesmo sendo textualmente "o mesmo" código.
        string codigoNormalizado = codigo.Trim().Normalize(NormalizationForm.FormC);
        if (codigoNormalizado.Contains(CaractereNulo))
        {
            return Falha(TipoEtapaErrorCodes.CodigoComCaractereNulo,
                "Código do tipo de etapa não pode conter o caractere nulo (U+0000).");
        }

        if (codigoNormalizado.Length > CodigoMaxLength)
        {
            return Falha(TipoEtapaErrorCodes.CodigoTamanho,
                $"Código do tipo de etapa deve ter no máximo {CodigoMaxLength} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Falha(TipoEtapaErrorCodes.NomeObrigatorio, "Nome do tipo de etapa é obrigatório.");
        }

        string nomeNormalizado = nome.Trim().Normalize(NormalizationForm.FormC);
        if (nomeNormalizado.Contains(CaractereNulo))
        {
            return Falha(TipoEtapaErrorCodes.NomeComCaractereNulo,
                "Nome do tipo de etapa não pode conter o caractere nulo (U+0000).");
        }

        if (nomeNormalizado.Length > NomeMaxLength)
        {
            return Falha(TipoEtapaErrorCodes.NomeTamanho,
                $"Nome do tipo de etapa deve ter no máximo {NomeMaxLength} caracteres.");
        }

        string? descricaoNormalizada = NormalizarOpcional(descricao);
        if (descricaoNormalizada is not null && descricaoNormalizada.Contains(CaractereNulo))
        {
            return Falha(TipoEtapaErrorCodes.DescricaoComCaractereNulo,
                "Descrição do tipo de etapa não pode conter o caractere nulo (U+0000).");
        }

        if (descricaoNormalizada is { Length: > DescricaoMaxLength })
        {
            return Falha(TipoEtapaErrorCodes.DescricaoTamanho,
                $"Descrição do tipo de etapa deve ter no máximo {DescricaoMaxLength} caracteres.");
        }

        return Result<Campos>.Success(new Campos(codigoNormalizado, nomeNormalizado, descricaoNormalizada));
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().Normalize(NormalizationForm.FormC);

    private static Result<Campos> Falha(string code, string message) =>
        Result<Campos>.Failure(new DomainError(code, message));

    private sealed record Campos(string Codigo, string Nome, string? Descricao);
}
