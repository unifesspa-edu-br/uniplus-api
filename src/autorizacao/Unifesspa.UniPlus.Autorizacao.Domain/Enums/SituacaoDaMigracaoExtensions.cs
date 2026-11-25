namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Regras da situação da migração.</summary>
public static class SituacaoDaMigracaoExtensions
{
    /// <summary>A situação que não muda mais.</summary>
    public static bool EhFinal(this SituacaoDaMigracao situacao) =>
        situacao is not (SituacaoDaMigracao.Retida or SituacaoDaMigracao.AguardandoProtecaoDeDados);
}
