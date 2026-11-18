namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed record OpcoesDeCursoDaInscricao
{
    private const string CampoDaOferta = "ofertaCursoId";
    private const string CampoDaSegundaOpcao = "ofertaCursoSegundaOpcaoId";

    private OpcoesDeCursoDaInscricao(Guid? primeira, Guid? segunda)
    {
        Primeira = primeira;
        Segunda = segunda;
    }

    public Guid? Primeira { get; }
    public Guid? Segunda { get; }

    public static OpcoesDeCursoDaInscricao Reidratar(Guid? primeira, Guid? segunda) => new(primeira, segunda);

    public static Result<OpcoesDeCursoDaInscricao> Escolher(Guid? primeira, Guid? segunda, RespostasAdmitidas admitidas)
    {
        ArgumentNullException.ThrowIfNull(admitidas);
        OpcoesDeCursoDaInscricao opcoes = new(primeira, segunda);
        IReadOnlyList<FieldError> recusas = [.. opcoes.RecusasAoSalvar(admitidas)];
        return recusas.Count > 0
            ? Result<OpcoesDeCursoDaInscricao>.ValidationFailure(recusas)
            : Result<OpcoesDeCursoDaInscricao>.Success(opcoes);
    }

    public IEnumerable<FieldError> RecusasAoSalvar(RespostasAdmitidas admitidas)
    {
        ArgumentNullException.ThrowIfNull(admitidas);
        return RecusasDaPrimeira(admitidas).Concat(RecusasDaSegunda(admitidas));
    }

    public IEnumerable<FieldError> RecusasAoEnviar(RespostasAdmitidas admitidas)
    {
        ArgumentNullException.ThrowIfNull(admitidas);
        return RecusasDaPrimeiraAoEnviar(admitidas).Concat(RecusasDaSegundaAoEnviar(admitidas));
    }

    private IEnumerable<FieldError> RecusasDaPrimeira(RespostasAdmitidas admitidas) =>
        Primeira is { } escolhida && !admitidas.OfertasDeCurso.Contains(escolhida)
            ? [new FieldError(CampoDaOferta, new DomainError(InscricaoErrorCodes.OfertaForaDoProcesso,
                "A oferta de curso escolhida não é deste processo seletivo."))]
            : [];

    private IEnumerable<FieldError> RecusasDaPrimeiraAoEnviar(RespostasAdmitidas admitidas) =>
        Primeira is null && admitidas.OfertasDeCurso.Count > 0
            ? [new FieldError(CampoDaOferta, new DomainError(InscricaoErrorCodes.OfertaObrigatoria,
                "Escolha a oferta de curso para enviar a inscrição."))]
            : RecusasDaPrimeira(admitidas);

    private IEnumerable<FieldError> RecusasDaSegunda(RespostasAdmitidas admitidas)
    {
        if (Segunda is not { } outra)
        {
            return [];
        }

        if (admitidas.NumeroDeOpcoesDeCurso < 2)
        {
            return [new FieldError(CampoDaSegundaOpcao, new DomainError(InscricaoErrorCodes.SegundaOpcaoNaoAdmitida,
                "Este processo seletivo admite uma só opção de curso. Retire a 2ª opção."))];
        }

        return RecusasDaSegundaAdmitida(outra, admitidas);
    }

    private IEnumerable<FieldError> RecusasDaSegundaAdmitida(Guid outra, RespostasAdmitidas admitidas)
    {
        if (outra == Primeira)
        {
            yield return new FieldError(CampoDaSegundaOpcao, new DomainError(InscricaoErrorCodes.SegundaOpcaoIgualAPrimeira,
                "A 2ª opção de curso tem de ser diferente da 1ª."));
        }

        if (!admitidas.OfertasDeCurso.Contains(outra))
        {
            yield return new FieldError(CampoDaSegundaOpcao, new DomainError(InscricaoErrorCodes.OfertaForaDoProcesso,
                "A oferta de curso escolhida não é deste processo seletivo."));
        }
    }

    private IEnumerable<FieldError> RecusasDaSegundaAoEnviar(RespostasAdmitidas admitidas)
    {
        foreach (FieldError recusa in RecusasDaSegunda(admitidas))
        {
            yield return recusa;
        }

        if (Segunda is not null && Primeira is null && admitidas.NumeroDeOpcoesDeCurso >= 2)
        {
            yield return new FieldError(CampoDaSegundaOpcao, new DomainError(InscricaoErrorCodes.SegundaOpcaoSemPrimeira,
                "Escolha a 1ª opção de curso antes da 2ª, ou retire a 2ª opção."));
        }
    }

    public static bool SegundaAdmissivel(Guid? primeira, Guid? segunda, RespostasAdmitidas admitidas)
    {
        ArgumentNullException.ThrowIfNull(admitidas);
        return segunda is not null && primeira is { } escolhida && admitidas.OfertasDeCurso.Contains(escolhida)
            && !new OpcoesDeCursoDaInscricao(primeira, segunda).RecusasAoSalvar(admitidas)
                .Any(static r => r.Field == CampoDaSegundaOpcao);
    }
}
