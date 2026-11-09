namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio da janela de migração das concessões, declarados uma vez para que o domínio e o registro
/// de erros da API não divirjam.
/// </summary>
public static class MigracaoErrorCodes
{
    public const string JanelaNaoEncontrada = "Migracao.JanelaNaoEncontrada";
    public const string JanelaEncerrada = "Migracao.JanelaEncerrada";
    public const string JanelaEmEncerramento = "Migracao.JanelaEmEncerramento";
    public const string PrazoEncerrado = "Migracao.PrazoEncerrado";
    public const string LoteJaEnviado = "Migracao.LoteJaEnviado";
    public const string LoteIncompleto = "Migracao.LoteIncompleto";
    public const string LoteInvalido = "Migracao.LoteInvalido";
    public const string LoteNaoEnviado = "Migracao.LoteNaoEnviado";
    public const string DecisaoDaPropriaConcessao = "Migracao.DecisaoDaPropriaConcessao";
    public const string MesmaPessoaNasDuasEtapas = "Migracao.MesmaPessoaNasDuasEtapas";
    public const string ConcessaoForaDaEtapa = "Migracao.ConcessaoForaDaEtapa";
    public const string ConcessaoJaDecidida = "Migracao.ConcessaoJaDecidida";
    public const string ExclusaoNaoConfirmada = "Migracao.ExclusaoNaoConfirmada";
    public const string MotivoInvalido = "Migracao.MotivoInvalido";
    public const string SemPermissaoNaEtapa = "Migracao.SemPermissaoNaEtapa";
}
