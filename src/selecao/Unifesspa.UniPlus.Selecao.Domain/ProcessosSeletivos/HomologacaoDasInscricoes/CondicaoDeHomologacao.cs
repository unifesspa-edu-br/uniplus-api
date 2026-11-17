namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Kernel.Results;
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

    private CondicaoDeHomologacao(
        TipoDeCondicaoDeHomologacao tipo, int edicaoDoExame, string motivoDesfavoravelCodigo, PredicadoDnf? enquadramento)
    {
        Tipo = tipo;
        EdicaoDoExame = edicaoDoExame;
        MotivoDesfavoravelCodigo = motivoDesfavoravelCodigo;
        Enquadramento = enquadramento;
    }

    public TipoDeCondicaoDeHomologacao Tipo { get; }

    /// <summary>A edição do exame cujo retorno do INEP a condição consulta.</summary>
    public int EdicaoDoExame { get; }

    public string MotivoDesfavoravelCodigo { get; }

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

    public static Result<CondicaoDeHomologacao> Criar(
        TipoDeCondicaoDeHomologacao tipo, int? edicaoDoExame, string? motivoDesfavoravelCodigo, PredicadoDnf? enquadramento)
    {
        DomainError? recusa = RecusaDoTipoOuDaEdicao(tipo, edicaoDoExame);
        Result<CodigoMotivoDecisao> motivo = CodigoMotivoDecisao.Criar(motivoDesfavoravelCodigo);
        List<FieldError> erros = [];
        if (recusa is not null)
        {
            erros.Add(new(null, recusa));
        }

        erros.AddRange(motivo.Errors);

        return erros.Count > 0
            ? Result<CondicaoDeHomologacao>.ValidationFailure(erros)
            : Result<CondicaoDeHomologacao>.Success(new CondicaoDeHomologacao(tipo, edicaoDoExame!.Value, motivo.Value!.Valor, enquadramento));
    }

    private static DomainError? RecusaDoTipoOuDaEdicao(TipoDeCondicaoDeHomologacao tipo, int? edicaoDoExame) => (tipo, edicaoDoExame) switch
    {
        _ when !Enum.IsDefined(tipo) => TiposDeCondicaoDeHomologacao.CondicaoNaoDisponivel("O tipo da condição ainda não é apurado pelo sistema."),
        (_, >= PrimeiraEdicaoDoExame and <= UltimaEdicaoAdmitida) => null,
        _ => new DomainError(
            HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida,
            $"A edição do exame deve ser um ano entre {PrimeiraEdicaoDoExame} e {UltimaEdicaoAdmitida}."),
    };
}
