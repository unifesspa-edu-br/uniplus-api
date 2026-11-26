namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// A cópia do tipo de etapa que a etapa guarda, tirada do tipo que o cadastro tem agora. O domínio confere a cópia, então
/// a recusa dele volta como resultado.
/// </summary>
internal static class CopiaDoTipoDeEtapa
{
    internal static Result<TipoEtapaSnapshot> De(TipoEtapaView tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return TipoEtapaSnapshot.Criar(
            tipo.Id,
            tipo.Codigo,
            tipo.Nome,
            tipo.Avaliacao.AdmitePontuacao,
            tipo.Avaliacao.AdmiteEliminacao,
            tipo.Avaliacao.NotaDeOrigemNoEnem,
            tipo.Avaliacao.ExigeCorrecaoIndependente);
    }
}
