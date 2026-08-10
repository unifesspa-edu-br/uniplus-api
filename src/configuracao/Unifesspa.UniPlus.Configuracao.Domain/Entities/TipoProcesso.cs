namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cadastro institucional de tipos de processo seletivo (UNI-REQ-0098).
/// </summary>
/// <remarks>
/// O código é chave natural imutável e reservado para sempre: desativar impede
/// novos vínculos, mas não libera o código. Consumidores cross-módulo recebem
/// somente itens ativos e congelam sua cópia por valor (ADR-0061).
/// </remarks>
public sealed class TipoProcesso : EntityBase, IAuditableEntity
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

    private TipoProcesso() { }

    public static Result<TipoProcesso> Criar(string codigo, string nome, string? descricao)
    {
        Result<Campos> campos = ValidarCampos(codigo, nome, descricao);
        if (campos.IsFailure)
        {
            return Result<TipoProcesso>.Failure(campos.Error!);
        }

        return Result<TipoProcesso>.Success(new TipoProcesso
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
                TipoProcessoErrorCodes.JaDesativado,
                "Tipo de processo seletivo já está desativado."));
        }

        Ativo = false;
        return Result.Success();
    }

    private static Result<Campos> ValidarCampos(string codigo, string nome, string? descricao)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Falha(TipoProcessoErrorCodes.CodigoObrigatorio, "Código do tipo de processo seletivo é obrigatório.");
        }

        string codigoNormalizado = codigo.Trim();
        if (codigoNormalizado.Contains(CaractereNulo))
        {
            return Falha(TipoProcessoErrorCodes.CodigoComCaractereNulo,
                "Código do tipo de processo seletivo não pode conter o caractere nulo (U+0000).");
        }

        if (codigoNormalizado.Length > CodigoMaxLength)
        {
            return Falha(TipoProcessoErrorCodes.CodigoTamanho,
                $"Código do tipo de processo seletivo deve ter no máximo {CodigoMaxLength} caracteres.");
        }

        if (codigoNormalizado == "*")
        {
            return Falha(TipoProcessoErrorCodes.CodigoReservado,
                "O código '*' é reservado para obrigatoriedade legal universal.");
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Falha(TipoProcessoErrorCodes.NomeObrigatorio, "Nome do tipo de processo seletivo é obrigatório.");
        }

        string nomeNormalizado = nome.Trim();
        if (nomeNormalizado.Contains(CaractereNulo))
        {
            return Falha(TipoProcessoErrorCodes.NomeComCaractereNulo,
                "Nome do tipo de processo seletivo não pode conter o caractere nulo (U+0000).");
        }

        if (nomeNormalizado.Length > NomeMaxLength)
        {
            return Falha(TipoProcessoErrorCodes.NomeTamanho,
                $"Nome do tipo de processo seletivo deve ter no máximo {NomeMaxLength} caracteres.");
        }

        string? descricaoNormalizada = NormalizarOpcional(descricao);
        if (descricaoNormalizada is not null && descricaoNormalizada.Contains(CaractereNulo))
        {
            return Falha(TipoProcessoErrorCodes.DescricaoComCaractereNulo,
                "Descrição do tipo de processo seletivo não pode conter o caractere nulo (U+0000).");
        }

        if (descricaoNormalizada is { Length: > DescricaoMaxLength })
        {
            return Falha(TipoProcessoErrorCodes.DescricaoTamanho,
                $"Descrição do tipo de processo seletivo deve ter no máximo {DescricaoMaxLength} caracteres.");
        }

        return Result<Campos>.Success(new Campos(codigoNormalizado, nomeNormalizado, descricaoNormalizada));
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static Result<Campos> Falha(string code, string message) =>
        Result<Campos>.Failure(new DomainError(code, message));

    private sealed record Campos(string Codigo, string Nome, string? Descricao);
}
