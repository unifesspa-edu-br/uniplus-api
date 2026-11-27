namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// A entidade de outro domínio de que o ato trata: o par opaco (tipo, id) que Publicações guarda sem
/// interpretar (ADR-0105).
/// </summary>
/// <param name="Tipo">Rótulo opaco do tipo da entidade, na forma canônica de <see cref="TipoDaEntidadeVinculada"/>.</param>
/// <param name="Id">Identificador da entidade, opaco para o módulo (sem chave estrangeira).</param>
public readonly record struct EntidadeVinculada(string Tipo, Guid Id)
{
    /// <summary>
    /// O par como o registro o grava, ou a exceção da primeira recusa de <see cref="Conferir"/>. Só a
    /// gravação confere; na leitura o par é filtro opaco.
    /// </summary>
    internal EntidadeVinculada Conferida() => Conferir().ValorOuLancar();

    /// <summary>
    /// Todas as recusas do par, nesta ordem: o rótulo na forma canônica e o identificador não vazio.
    /// Os campos são relativos ao vínculo (<c>entidadeTipo</c>, <c>entidadeId</c>).
    /// </summary>
    internal ParteConferida<EntidadeVinculada> Conferir()
    {
        EntidadeVinculada entidade = this;
        return ParteConferida<EntidadeVinculada>.Compor(
            [.. RecusasDoTipo(), .. RecusasDoIdentificador()],
            () => entidade);
    }

    private IEnumerable<FieldError> RecusasDoTipo() =>
        TipoDaEntidadeVinculada.Recusa(Tipo) is { } recusa ? [recusa] : [];

    private IEnumerable<FieldError> RecusasDoIdentificador() =>
        Id == Guid.Empty
            ? [new FieldError("entidadeId", new DomainError(
                AtoNormativoErrorCodes.VinculoSemIdentificadorDaEntidade,
                "O identificador da entidade vinculada não pode ser vazio."))]
            : [];
}
