namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

/// <summary>Autores, finalidade e instantes comuns aos testes do circuito de aprovação da proteção.</summary>
internal static class AprovacaoDeTeste
{
    internal const string Isabella = "isabella";
    internal const string Caroline = "caroline";
    internal const string Finalidade = "Comprovação documental do candidato.";
    internal static readonly DateTimeOffset Segunda = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset Terca = Segunda.AddDays(1);
    internal static readonly DateTimeOffset Quarta = Segunda.AddDays(2);
}
