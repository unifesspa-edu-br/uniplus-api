namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio da revisão periódica dos acessos, declarados uma vez para que o domínio, os
/// handlers e o registro de erros da API não divirjam.
/// </summary>
public static class RevisaoErrorCodes
{
    public const string NaoEncontrada = "Revisao.NaoEncontrada";
    public const string ItemNaoEncontrado = "Revisao.ItemNaoEncontrado";
    public const string JaHaEmAndamento = "Revisao.JaHaEmAndamento";
    public const string ItemJaDecidido = "Revisao.ItemJaDecidido";
    public const string ConcessaoNaoAtiva = "Revisao.ConcessaoNaoAtiva";
    public const string DecisorEBeneficiario = "Revisao.DecisorEBeneficiario";
    public const string DecisorNaoDecideOItem = "Revisao.DecisorNaoDecideOItem";
    public const string SemPermissao = "Revisao.SemPermissao";
    public const string IdentidadeIncompleta = "Revisao.IdentidadeIncompleta";
    public const string AutorNaoIdentificado = "Revisao.AutorNaoIdentificado";
    public const string ConflitoDeConcorrencia = "Revisao.ConflitoDeConcorrencia";
}
