namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que uma semente declara de si: o nome que as recusas do domínio citam, quem consta como autor da
/// promoção dos termos que ela grava, que não passaram por pessoa, e o instante de criação das linhas.
/// </summary>
/// <param name="Nome">Como as recusas do domínio nomeiam a semente.</param>
/// <param name="Autor">Quem consta como autor da revisão e da promoção dos termos semeados.</param>
/// <param name="Instante">O instante de criação das linhas semeadas.</param>
internal sealed record DeclaracaoDaSemente(string Nome, string Autor, DateTimeOffset Instante)
{
    /// <summary>O valor do resultado, ou a recusa do domínio como exceção que nomeia a semente e o item.</summary>
    public T Exigir<T>(Result<T> resultado, string oQue) =>
        resultado.IsSuccess ? resultado.Value! : throw Recusa(resultado.Errors, oQue);

    /// <summary>A recusa do domínio como exceção que nomeia a semente e o item.</summary>
    public void Exigir(Result resultado, string oQue)
    {
        if (resultado.IsFailure)
        {
            throw Recusa(resultado.Errors, oQue);
        }
    }

    private InvalidOperationException Recusa(IEnumerable<FieldError> erros, string oQue) =>
        new($"A semente {Nome} tem {oQue} que o domínio recusa: "
            + string.Join("; ", erros.Select(static e => $"{e.Field}: {e.Error.Message}")));
}
