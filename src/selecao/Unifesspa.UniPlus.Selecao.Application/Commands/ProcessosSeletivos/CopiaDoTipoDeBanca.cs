namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>A cópia do tipo de banca que a banca guarda, tirada do tipo que o cadastro tem agora.</summary>
internal static class CopiaDoTipoDeBanca
{
    internal static TipoDeBancaCopiado De(TipoBancaView tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return new TipoDeBancaCopiado(
            tipo.Id,
            tipo.Codigo,
            tipo.Nome,
            tipo.Papel,
            tipo.ExigeDuasOuMaisBancas,
            tipo.EncerraRecursoNaPrimeiraInstancia,
            tipo.TiposDeDecisaoJulgados);
    }
}
