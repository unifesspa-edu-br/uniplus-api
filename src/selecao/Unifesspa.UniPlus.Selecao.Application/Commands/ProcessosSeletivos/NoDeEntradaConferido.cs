namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

/// <summary>Um item enviado, sua declaração e recusas, com seus descendentes e posição de origem.</summary>
internal sealed record NoDeEntradaConferido(
    NoExigenciaInput Entrada,
    DeclaracaoDoNoDeExigencia Declaracao,
    string Caminho,
    int Indice,
    IReadOnlyList<FieldError> Recusas,
    IReadOnlyList<NoDeEntradaConferido> Filhos)
{
    internal static bool RepeticaoRecusada(NoDeEntradaConferido no) =>
        no.Recusas.Any(static recusa => recusa.Error.Code == "NoExigencia.TipoEntidadeInvalido");
}
