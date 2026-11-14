namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
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

    /// <summary>Sub do usuário autenticado (via <c>IUserContext</c>), nunca input do comando.</summary>
    public required string AtorUsuarioSub { get; init; }

    /// <summary>
    /// O instante em que o comando publica, lido uma só vez do relógio injetado (ADR-0068) e nunca relido pelo
    /// agregado: vale a vigência da versão e o id do ato que ela cria.
    /// </summary>
    public required DateTimeOffset Instante { get; init; }

    public required ContextoDeContagemDePrazos Contexto { get; init; }

    public required FatosDeModalidade FatosDeModalidade { get; init; }

    public required TiposDeAtoUnicosPorObjeto TiposDeAtoUnicos { get; init; }

    /// <summary>Os corretores que a regra de consolidação de cada etapa exige, para o mínimo de integrantes das bancas.</summary>
    public CorretoresDasEtapas Corretores { get; init; } = CorretoresDasEtapas.Nenhum;
}

/// <summary>
/// O documento canônico que a versão congela: os bytes produzidos pelo canonicalizador da Application
/// (o Domain não os produz, ADR-0042), a versão do conjunto de blocos, o algoritmo de hash e o hash
/// do documento publicado, que é o do ato criador.
/// </summary>
public readonly record struct DocumentoCongelado(byte[] Canonico, string SchemaVersion, string AlgoritmoHash, string HashDocumento);
