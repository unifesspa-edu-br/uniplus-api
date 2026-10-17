namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Os eixos que a versão publicada congela no item que coleta um fato: como o campo é desenhado,
/// de onde vêm as opções, o formato do texto e a proteção do dado.
/// </summary>
public sealed record EixosDoItemCongelado(string TipoRenderizacao, string OrigemValores, string? Formato, string ClassificacaoProtecao)
{
    public static EixosDoItemCongelado De(FatoColetado item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new(item.TipoRenderizacao.ToCodigo(), item.OrigemValores.ToString(), item.Formato, item.ClassificacaoProtecao);
    }

    internal IEnumerable<(string Eixo, string? Valor)> Eixos() =>
    [
        ("tipo de renderização", TipoRenderizacao),
        ("origem dos valores", OrigemValores),
        ("formato", Formato),
        ("classificação de proteção", ClassificacaoProtecao),
    ];
}

/// <summary>
/// Os eixos que a versão publicada congela do fato citado por condição de documento exigido: o
/// domínio, a cardinalidade e o ponto de resolução.
/// </summary>
public sealed record EixosDoMetadadoCongelado(string Dominio, string Cardinalidade, string PontoResolucao)
{
    internal IEnumerable<(string Eixo, string? Valor)> Eixos() =>
    [
        ("domínio", Dominio),
        ("cardinalidade", Cardinalidade),
        ("ponto de resolução", PontoResolucao),
    ];
}

/// <summary>O que uma versão publicada congelou além dos itens: o metadado dos fatos dos gatilhos e a operação de cada agregado.</summary>
/// <param name="Metadados">Por código do fato citado por condição de documento exigido.</param>
/// <param name="OperacaoDosAgregados">Por código do agregado, o token da operação.</param>
public sealed record EixosDeVersaoPublicada(
    IReadOnlyDictionary<string, EixosDoMetadadoCongelado> Metadados,
    IReadOnlyDictionary<string, string> OperacaoDosAgregados);

/// <summary>
/// Um eixo que a sessão de retificação congelaria diferente do que a versão publicada congelou, num
/// fato que o candidato já pode ter respondido (UNI-REQ-0144).
/// </summary>
public sealed record TrocaDeEixo(string Fato, string Eixo, string? Publicado, string? Novo)
{
    public const string Codigo = "RascunhoRetificacao.EixoDoFatoJaColetadoAlterado";

    public DomainError ParaErro() => new(
        Codigo,
        $"O fato '{Fato}' já foi coletado por versão publicada do edital e o eixo '{Eixo}' dele mudou de '{Publicado ?? "(vazio)"}' para '{Novo ?? "(vazio)"}'. "
        + "O fato já coletado mantém a definição da versão publicada: a retificação não a troca, e a mudança vale para processo novo.");

    internal static TrocaDeEixo? Primeira(string fato, IEnumerable<(string Eixo, string? Valor)> publicado, IEnumerable<(string Eixo, string? Valor)> novo) =>
        publicado.Zip(novo)
            .Where(static par => !string.Equals(par.First.Valor, par.Second.Valor, StringComparison.Ordinal))
            .Select(par => new TrocaDeEixo(fato, par.First.Eixo, par.First.Valor, par.Second.Valor))
            .FirstOrDefault();
}
