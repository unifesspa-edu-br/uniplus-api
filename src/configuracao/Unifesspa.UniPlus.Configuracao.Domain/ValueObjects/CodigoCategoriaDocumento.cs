namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Código da categoria de documento (UNI-REQ-0013, módulo Configuração) — chave
/// natural classificatória (por exemplo: <c>RENDA</c>, <c>DOCUMENTO_PROCESSUAL</c>,
/// <c>TITULACAO_EXPERIENCIA</c>). Código no formato de
/// <see cref="FormatoDoCodigo"/> — inicia com letra maiúscula, segue com letras
/// maiúsculas, dígitos e sublinhado, total de no máximo 50 caracteres. Persistido como
/// <c>varchar</c> via conversor de valor (falha imediata na reidratação).
/// </summary>
/// <remarks>
/// Mesma convenção de <see cref="CodigoCondicao"/> e dos demais códigos de
/// cadastro do módulo: o número é permitido em qualquer posição exceto a
/// primeira, o que impede numeração sequencial de voltar como identidade sem
/// proibir código semântico que carregue número (exercício fiscal, número de
/// lei). Nenhuma categoria é reservada — todas são editáveis e removíveis.
/// </remarks>
public sealed record CodigoCategoriaDocumento
{
    private const int TamanhoMaximo = 50;

    public string Valor { get; }

    private CodigoCategoriaDocumento(string valor) => Valor = valor;

    /// <summary>
    /// Cria um <see cref="CodigoCategoriaDocumento"/> validando o formato fechado.
    /// Valor nulo/em branco retorna <c>CodigoObrigatorio</c>; fora do formato
    /// retorna <c>CodigoFormatoInvalido</c>. O valor é conferido como recebido.
    /// </summary>
    public static Result<CodigoCategoriaDocumento> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<CodigoCategoriaDocumento>.Failure(new DomainError(
                CategoriaDocumentoErrorCodes.CodigoObrigatorio,
                "Código da categoria de documento é obrigatório."));
        }

        return EhValido(valor)
            ? Result<CodigoCategoriaDocumento>.Success(new CodigoCategoriaDocumento(valor!))
            : Result<CodigoCategoriaDocumento>.Failure(new DomainError(
                CategoriaDocumentoErrorCodes.CodigoFormatoInvalido,
                "Código da categoria de documento deve iniciar com letra maiúscula e conter apenas "
                + $"letras maiúsculas, dígitos e sublinhado, com no máximo {TamanhoMaximo} "
                + "caracteres (ex.: RENDA, DOCUMENTO_PROCESSUAL, RACA_ETNIA)."));
    }

    /// <summary>Indica se <paramref name="valor"/> respeita o formato fechado, sem criar o objeto.</summary>
    public static bool EhValido(string? valor) =>
        valor is not null && valor.Length <= TamanhoMaximo && FormatoDoCodigo.TemFormatoValido(valor);

    public override string ToString() => Valor;
}
