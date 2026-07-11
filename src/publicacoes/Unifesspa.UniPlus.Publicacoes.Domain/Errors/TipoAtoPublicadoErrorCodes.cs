namespace Unifesspa.UniPlus.Publicacoes.Domain.Errors;

public static class TipoAtoPublicadoErrorCodes
{
    public const string CodigoObrigatorio = "TipoAtoPublicado.CodigoObrigatorio";
    public const string CodigoTamanho = "TipoAtoPublicado.CodigoTamanho";
    public const string CodigoFormato = "TipoAtoPublicado.CodigoFormato";

    /// <summary>
    /// Tentou-se trocar o código de uma versão existente. O código é a identidade do
    /// tipo: a série de vigências agrupa-se por ele (exclusion constraint), e a vaga que
    /// um objeto reserva para uma linhagem de atos únicos por objeto é chaveada por ele
    /// (ADR-0107). Renomear é criar outro tipo, não editar este.
    /// </summary>
    public const string CodigoImutavel = "TipoAtoPublicado.CodigoImutavel";
    public const string NomeObrigatorio = "TipoAtoPublicado.NomeObrigatorio";
    public const string NomeTamanho = "TipoAtoPublicado.NomeTamanho";
    public const string BaseLegalTamanho = "TipoAtoPublicado.BaseLegalTamanho";
    public const string VigenciaFimAnteriorAoInicio = "TipoAtoPublicado.VigenciaFimAnteriorAoInicio";

    /// <summary>
    /// Já existe versão viva do mesmo código cuja janela de vigência intercepta a
    /// janela informada. Detectado pela exclusion constraint do banco.
    /// </summary>
    public const string VigenciaSobreposta = "TipoAtoPublicado.VigenciaSobreposta";

    public const string NaoEncontrado = "TipoAtoPublicado.NaoEncontrado";

    /// <summary>O identificador da URL não corresponde ao do corpo da requisição.</summary>
    public const string IdDivergente = "TipoAtoPublicado.IdDivergente";
}
