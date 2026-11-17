namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Errors;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O processo seletivo antes da primeira publicação: o documento da configuração, guardado como texto, com a revisão
/// que o identifica e a unidade que o possui. O <see cref="EntityBase.Id"/> é o do processo que a publicação cria.
/// </summary>
/// <remarks>
/// O agregado não interpreta o conteúdo: guarda texto, revisão e dono. Não é soft-deletable: a publicação o consome e a
/// exclusão apaga a linha. A coordenação entre editores é o <c>ETag</c>, conferido sob a trava da linha.
/// </remarks>
public sealed class RascunhoDeProcessoSeletivo : EntityBase
{
    /// <summary>Nasce em 1 e sobe a cada substituição aceita.</summary>
    public int Revisao { get; private set; }

    /// <summary>A unidade que possui o rascunho e decide quem o lê, grava, exclui e publica.</summary>
    public Guid UnidadeAdministradoraOrigemId { get; private set; }

    /// <summary>O texto JSON do documento da configuração.</summary>
    public string Documento { get; private set; } = string.Empty;

    /// <summary>O nome do processo, projetado do documento só para a listagem.</summary>
    public string? Nome { get; private set; }

    /// <summary>O ETag forte da revisão: identifica a revisão, não os bytes do documento.</summary>
    public string ETag => $"\"{Revisao}\"";

    private RascunhoDeProcessoSeletivo() { }

    /// <summary>A recusa de quem não informa a unidade que responde pelo processo: sem ela não há dono para decidir o acesso.</summary>
    public static DomainError UnidadeAdministradoraAusente { get; } = new(
        RascunhoDeProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente,
        "Informe a unidade administradora que responde pelo processo seletivo.");

    public static Result<RascunhoDeProcessoSeletivo> Criar(Guid unidadeAdministradoraOrigemId, DocumentoDoRascunho documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        if (unidadeAdministradoraOrigemId == Guid.Empty)
        {
            return Result<RascunhoDeProcessoSeletivo>.Failure(UnidadeAdministradoraAusente);
        }

        RascunhoDeProcessoSeletivo rascunho = new() { Revisao = 1 };
        rascunho.Adotar(documento, unidadeAdministradoraOrigemId);
        return Result<RascunhoDeProcessoSeletivo>.Success(rascunho);
    }

    /// <summary>A mutação exige o <c>If-Match</c>: sem ele nada se lê nem se trava, e a recusa é a precondição requerida.</summary>
    public static Result ExigirPrecondicao(PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        return precondicao.Presente
            ? Result.Success()
            : Result.Failure(new DomainError(
                PrecondicaoErrorCodes.Requerida,
                "Informe o If-Match com o ETag da revisão do rascunho que você leu."));
    }

    /// <summary>
    /// Confere o <c>If-Match</c> contra a revisão atual: ausente é a precondição requerida, e o que não casa é a
    /// precondição falha. É separada de <see cref="Substituir"/> para que quem muda a unidade decida o acesso à unidade
    /// nova depois desta conferência e antes de gravar.
    /// </summary>
    public Result ConferirPrecondicao(PrecondicaoIfMatch precondicao)
    {
        Result exigida = ExigirPrecondicao(precondicao);

        return exigida.IsFailure || precondicao.Casa(ETag)
            ? exigida
            : Result.Failure(new DomainError(
                PrecondicaoErrorCodes.Falhou,
                "O If-Match informado não corresponde à revisão atual do rascunho — releia o rascunho e refaça a operação."));
    }

    /// <summary>
    /// Troca o documento inteiro, o nome e a unidade, e sobe a revisão. A precondição vem antes da unidade: quem leu uma
    /// revisão velha recebe a recusa de concorrência, não a de um dado que a outra revisão pode ter corrigido.
    /// </summary>
    public Result Substituir(DocumentoDoRascunho documento, Guid unidadeAdministradoraOrigemId, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(documento);

        Result conferida = ConferirPrecondicaoEUnidade(unidadeAdministradoraOrigemId, precondicao);
        if (conferida.IsSuccess)
        {
            Adotar(documento, unidadeAdministradoraOrigemId);
            Revisao++;
        }

        return conferida;
    }

    private Result ConferirPrecondicaoEUnidade(Guid unidadeAdministradoraOrigemId, PrecondicaoIfMatch precondicao)
    {
        Result conferida = ConferirPrecondicao(precondicao);

        return conferida.IsFailure || unidadeAdministradoraOrigemId != Guid.Empty
            ? conferida
            : Result.Failure(UnidadeAdministradoraAusente);
    }

    private void Adotar(DocumentoDoRascunho documento, Guid unidadeAdministradoraOrigemId)
    {
        Documento = documento.Conteudo;
        Nome = documento.Nome;
        UnidadeAdministradoraOrigemId = unidadeAdministradoraOrigemId;
    }
}
