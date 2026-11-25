namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma condição da regra de homologação: o que se apura da inscrição, de que edição do exame, o
/// enquadramento opcional dos candidatos a que ela se aplica e o código do motivo desfavorável que a
/// inscrição recebe quando a condição não é cumprida.
/// </summary>
public sealed record CondicaoDeHomologacao
{
    private const int PrimeiraEdicaoDoExame = 1998;
    private const int UltimaEdicaoAdmitida = 2100;
    private const string CampoDoEnquadramento = "enquadramento";
    private const string CampoDoTipo = "tipo";
    private const string CampoDaEdicaoDoExame = "edicaoDoExame";
    private const string CampoDoMotivoDesfavoravel = "motivoDesfavoravelCodigo";

    private CondicaoDeHomologacao(
        TipoDeCondicaoDeHomologacao tipo, int edicaoDoExame, string? motivoDesfavoravelCodigo, PredicadoDnf? enquadramento)
    {
        Tipo = tipo;
        EdicaoDoExame = edicaoDoExame;
        MotivoDesfavoravelCodigo = motivoDesfavoravelCodigo;
        Enquadramento = enquadramento;
    }

    public TipoDeCondicaoDeHomologacao Tipo { get; }

    /// <summary>A edição do exame cujo retorno do INEP a condição consulta.</summary>
    public int EdicaoDoExame { get; }

    /// <summary>O motivo da inscrição quando a condição não é cumprida; ausente quando a condição não escolhe um.</summary>
    public string? MotivoDesfavoravelCodigo { get; }

    /// <summary>Quais candidatos a condição alcança; ausente quando vale para todos.</summary>
    public PredicadoDnf? Enquadramento { get; }

    /// <summary>Se a outra condição apura o mesmo, da mesma edição, com o mesmo motivo e o mesmo enquadramento.</summary>
    public bool MesmoConteudo(CondicaoDeHomologacao outra)
    {
        ArgumentNullException.ThrowIfNull(outra);
        return Tipo == outra.Tipo
            && EdicaoDoExame == outra.EdicaoDoExame
            && string.Equals(MotivoDesfavoravelCodigo, outra.MotivoDesfavoravelCodigo, StringComparison.Ordinal)
            && (Enquadramento is null ? outra.Enquadramento is null : outra.Enquadramento is not null && Enquadramento.MesmoConteudo(outra.Enquadramento));
    }

    /// <summary>
    /// Confere a forma da condição e acumula as recusas (ADR-0125), cada uma no campo da condição: o tipo precisa
    /// ser um que o sistema apura, a edição do exame um ano plausível e o motivo desfavorável, quando informado,
    /// um código bem formado. O motivo ausente é aceito aqui. O enquadramento, quando informado, precisa ter ao
    /// menos uma cláusula.
    /// </summary>
    public static Result<CondicaoDeHomologacao> Declarar(
        string? tipoCodigo, int? edicaoDoExame, string? motivoDesfavoravelCodigo, PredicadoDnf? enquadramento)
    {
        Result<TipoDeCondicaoDeHomologacao> tipo = LerTipo(tipoCodigo);
        Result<CodigoMotivoDecisao>? motivo = string.IsNullOrWhiteSpace(motivoDesfavoravelCodigo)
            ? null
            : CodigoMotivoDecisao.Criar(motivoDesfavoravelCodigo);

        List<FieldError> recusas = [.. RecusaDoTipo(tipo), .. RecusaDaEdicaoDoExame(edicaoDoExame), .. RecusaDoMotivoDesfavoravel(motivo), .. RecusaDoEnquadramento(enquadramento)];
        return recusas.Count > 0
            ? Result<CondicaoDeHomologacao>.ValidationFailure(recusas)
            : Result<CondicaoDeHomologacao>.Success(new CondicaoDeHomologacao(tipo.Value, edicaoDoExame!.Value, motivo?.Value!.Valor, enquadramento));
    }

    /// <summary>Declara a condição acumulando as recusas do enquadramento recebido.</summary>
    public static Result<CondicaoDeHomologacao> DeclararComOEnquadramentoRecebido(
        string? tipoCodigo, int? edicaoDoExame, string? motivoDesfavoravelCodigo, Result<PredicadoDnf?> enquadramento)
    {
        ArgumentNullException.ThrowIfNull(enquadramento);
        Result<CondicaoDeHomologacao> condicao = Declarar(tipoCodigo, edicaoDoExame, motivoDesfavoravelCodigo, enquadramento.Value);
        return condicao.IsFailure || enquadramento.IsFailure
            ? Result<CondicaoDeHomologacao>.ValidationFailure([.. condicao.Errors, .. enquadramento.Errors.Select(causa => causa.NoCaminhoDoItem(CampoDoEnquadramento))])
            : condicao;
    }

    private static Result<TipoDeCondicaoDeHomologacao> LerTipo(string? codigo) =>
        CatalogoDe<TipoDeCondicaoDeHomologacao>.TentarObter(codigo, out TipoDeCondicaoDeHomologacao tipo)
            ? Result<TipoDeCondicaoDeHomologacao>.Success(tipo)
            : Result<TipoDeCondicaoDeHomologacao>.Failure(new DomainError(
                HomologacaoDasInscricoesErrorCodes.CondicaoNaoDisponivel,
                string.IsNullOrWhiteSpace(codigo)
                    ? "O tipo da condição precisa ser informado."
                    : "A condição indicada ainda não é apurada pelo sistema."));

    private static IEnumerable<FieldError> RecusaDoTipo(Result<TipoDeCondicaoDeHomologacao> tipo)
    {
        if (tipo.IsFailure)
        {
            yield return new FieldError(CampoDoTipo, tipo.Error!);
        }
    }

    private static IEnumerable<FieldError> RecusaDaEdicaoDoExame(int? edicaoDoExame)
    {
        if (edicaoDoExame is not (>= PrimeiraEdicaoDoExame and <= UltimaEdicaoAdmitida))
        {
            yield return new FieldError(CampoDaEdicaoDoExame, new DomainError(
                HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida,
                $"A edição do exame deve ser um ano entre {PrimeiraEdicaoDoExame} e {UltimaEdicaoAdmitida}."));
        }
    }

    private static IEnumerable<FieldError> RecusaDoMotivoDesfavoravel(Result<CodigoMotivoDecisao>? motivo)
    {
        if (motivo is { IsFailure: true })
        {
            yield return new FieldError(CampoDoMotivoDesfavoravel, motivo.Error!);
        }
    }

    private static IEnumerable<FieldError> RecusaDoEnquadramento(PredicadoDnf? enquadramento)
    {
        if (enquadramento is { Clausulas.Count: 0 })
        {
            yield return new FieldError(CampoDoEnquadramento, new DomainError(
                PredicadoDnfJson.FormaInvalida, "O enquadramento deve ter ao menos uma cláusula."));
        }
    }
}
