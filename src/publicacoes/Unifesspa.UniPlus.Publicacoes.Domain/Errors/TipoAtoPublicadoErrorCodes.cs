namespace Unifesspa.UniPlus.Publicacoes.Domain.Errors;

public static class TipoAtoPublicadoErrorCodes
{
    public const string CodigoObrigatorio = "TipoAtoPublicado.CodigoObrigatorio";
    public const string CodigoTamanho = "TipoAtoPublicado.CodigoTamanho";
    public const string CodigoFormato = "TipoAtoPublicado.CodigoFormato";
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
}
