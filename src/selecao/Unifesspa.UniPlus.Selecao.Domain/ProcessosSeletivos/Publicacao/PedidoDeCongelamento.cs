namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os parâmetros comuns às três transições que congelam uma versão da configuração. Não valida
/// no construtor: cada transição confere os campos na posição e na ordem que sempre conferiu,
/// porque conferir antes mudaria a precedência entre a exceção e a recusa.
/// </summary>
public sealed record PedidoDeCongelamento
{
    public required DadosEdital Dados { get; init; }

    public required DocumentoCongelado Documento { get; init; }

    /// <summary>ContaId do usuário autenticado (via <c>IUserContext</c>), nunca input do comando.</summary>
    public required string AtorUsuarioContaId { get; init; }

    /// <summary>
    /// O instante em que o comando publica, lido uma só vez do relógio injetado (ADR-0068) e nunca relido pelo
    /// agregado: vale a vigência da versão e o id do ato que ela cria.
    /// </summary>
    public required DateTimeOffset Instante { get; init; }

    /// <summary>A configuração do processo que a conferência julgou sem impedimento, nesta mesma unidade de trabalho.</summary>
    public required ConfiguracaoAptaACongelar ConfiguracaoApta { get; init; }
}

/// <summary>
/// O documento canônico que a versão congela: os bytes produzidos pelo canonicalizador da Application
/// (o Domain não os produz, ADR-0042), a versão do conjunto de blocos, o algoritmo de hash e o hash
/// do documento publicado, que é o do ato criador.
/// </summary>
public readonly record struct DocumentoCongelado(byte[] Canonico, string SchemaVersion, string AlgoritmoHash, string HashDocumento);
