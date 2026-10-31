namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do pedido de concessão, declarados uma vez para que o domínio, os
/// handlers e o registro de erros da API não divirjam.
/// </summary>
public static class PedidoErrorCodes
{
    public const string NaoEncontrado = "Pedido.NaoEncontrado";
    public const string PerfilInativo = "Pedido.PerfilInativo";
    public const string EscopoInvalido = "Pedido.EscopoInvalido";
    public const string UnidadeInexistente = "Pedido.UnidadeInexistente";
    public const string JustificativaInvalida = "Pedido.JustificativaInvalida";
    public const string BeneficiarioInvalido = "Pedido.BeneficiarioInvalido";
    public const string SolicitanteNaoInstitucional = "Pedido.SolicitanteNaoInstitucional";
    public const string BeneficiarioSemNome = "Pedido.BeneficiarioSemNome";
    public const string PedidoDuplicado = "Pedido.PedidoDuplicado";
    public const string EtapaInvalida = "Pedido.EtapaInvalida";
    public const string SituacaoInvalida = "Pedido.SituacaoInvalida";
    public const string DecisorEParte = "Pedido.DecisorEParte";
    public const string MesmoDecisorNasDuasEtapas = "Pedido.MesmoDecisorNasDuasEtapas";
    public const string MotivoInvalido = "Pedido.MotivoInvalido";
    public const string ValidadeInvalida = "Pedido.ValidadeInvalida";
    public const string PrazoVencido = "Pedido.PrazoVencido";
    public const string JaTemConcessao = "Pedido.JaTemConcessao";
    public const string AutorNaoIdentificado = "Pedido.AutorNaoIdentificado";
    public const string NaoEOBeneficiario = "Pedido.NaoEOBeneficiario";
    public const string PerfilAlterado = "Pedido.PerfilAlterado";
    public const string AnexoNaoVerificado = "Pedido.AnexoNaoVerificado";
    public const string AnexoInvalido = "Pedido.AnexoInvalido";
    public const string SemPermissao = "Pedido.SemPermissao";
    public const string IdentidadeIncompleta = "Pedido.IdentidadeIncompleta";
    public const string ConflitoDeConcorrencia = "Pedido.ConflitoDeConcorrencia";
    public const string FilaInvalida = "Pedido.FilaInvalida";
    public const string CursorInvalido = "Pedido.CursorInvalido";
    public const string FiltroDeSituacaoInvalido = "Pedido.FiltroDeSituacaoInvalido";
}
