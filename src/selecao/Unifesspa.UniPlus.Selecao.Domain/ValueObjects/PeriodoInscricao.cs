namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.SharedKernel.Results;

public sealed record PeriodoInscricao
{
    public DateTimeOffset Inicio { get; }
    public DateTimeOffset Fim { get; }

    private PeriodoInscricao(DateTimeOffset inicio, DateTimeOffset fim)
    {
        Inicio = inicio;
        Fim = fim;
    }

    public static Result<PeriodoInscricao> Criar(DateTimeOffset inicio, DateTimeOffset fim)
    {
        if (fim <= inicio)
            return Result<PeriodoInscricao>.Failure(new DomainError("PeriodoInscricao.Invalido", "Data de fim deve ser posterior à data de início."));

        return Result<PeriodoInscricao>.Success(new PeriodoInscricao(inicio, fim));
    }

    public bool EstaAberto(DateTimeOffset agora) => agora >= Inicio && agora <= Fim;
}
