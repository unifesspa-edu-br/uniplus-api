namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// O vocabulário fechado de <c>tipo</c> do item de resposta administrativo — como ler o valor — e a sua derivação dos campos
/// congelados do fato (ADR-0153).
/// </summary>
public static class TiposDeItem
{
    /// <summary>String.</summary>
    public const string Texto = "texto";

    /// <summary>ISO 8601 só a data, sem fuso.</summary>
    public const string Data = "data";

    /// <summary>ISO 8601 completo, com o deslocamento explícito.</summary>
    public const string DataHora = "data-hora";

    /// <summary>Hora e minuto, <c>HH:mm</c>.</summary>
    public const string Hora = "hora";

    /// <summary>Verdadeiro ou falso.</summary>
    public const string Booleano = "booleano";

    /// <summary>String decimal exata, sem ponto flutuante.</summary>
    public const string Numero = "numero";

    /// <summary>Um objeto <c>{ codigo, rotulo }</c>.</summary>
    public const string Opcao = "opcao";

    /// <summary>Uma lista de objetos <c>{ codigo, rotulo }</c>.</summary>
    public const string Opcoes = "opcoes";

    /// <summary>Um objeto de endereço.</summary>
    public const string Endereco = "endereco";

    /// <summary>O tipo do fato coletado, pelo tipo de renderização que o processo congelou.</summary>
    public static string De(TipoRenderizacao renderizacao) => renderizacao switch
    {
        TipoRenderizacao.Numero => Numero,
        TipoRenderizacao.Booleano => Booleano,
        TipoRenderizacao.SelecaoUnica or TipoRenderizacao.Municipio => Opcao,
        TipoRenderizacao.SelecaoMultipla => Opcoes,
        TipoRenderizacao.Data => Data,
        TipoRenderizacao.Endereco => Endereco,
        _ => Texto,
    };

    /// <summary>O tipo do fato derivado, pelo domínio e pela cardinalidade que a publicação congelou.</summary>
    public static string De(string dominio, string cardinalidade) => dominio switch
    {
        "CATEGORICO" => string.Equals(cardinalidade, "MULTIVALORADO", StringComparison.Ordinal) ? Opcoes : Opcao,
        "NUMERICO" => Numero,
        "BOOLEANO" => Booleano,
        "DATA" => Data,
        "ENDERECO" => Endereco,
        _ => Texto,
    };
}

/// <summary>O vocabulário de <c>componente</c> do item de resposta administrativo — como exibir o valor.</summary>
public static class ComponentesDeItem
{
    /// <summary>Exibir pelo tipo.</summary>
    public const string Padrao = "padrao";

    /// <summary>O CPF mascarado.</summary>
    public const string Cpf = "cpf";

    /// <summary>Um identificador mascarado.</summary>
    public const string Identificador = "identificador";

    /// <summary>
    /// O componente do item, nesta ordem de prioridade: o declarado no cadastro do fato e congelado na publicação; o
    /// formato; a classificação; senão o padrão.
    /// </summary>
    public static string De(string? declarado, bool formatoCpf, bool identificador)
    {
        if (!string.IsNullOrWhiteSpace(declarado))
        {
            return declarado.Trim().ToLowerInvariant();
        }

        if (formatoCpf)
        {
            return Cpf;
        }

        return identificador ? Identificador : Padrao;
    }
}
