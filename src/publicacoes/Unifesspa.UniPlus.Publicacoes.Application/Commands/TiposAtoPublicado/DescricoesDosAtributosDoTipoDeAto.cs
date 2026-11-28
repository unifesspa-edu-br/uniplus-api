namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

/// <summary>Textos publicados no contrato para os campos obrigatórios do corpo de criação e de alteração do tipo de ato.</summary>
internal static class DescricoesDosAtributosDoTipoDeAto
{
    private const string Recusa = " Obrigatório; ausente ou nulo é recusado (422).";

    public const string CongelaConfiguracao = "Se o ato deste tipo produz nova versão congelada da configuração." + Recusa;

    public const string UnicoPorObjeto = "Se o objeto do ato admite um único ato vivo deste tipo." + Recusa;

    public const string EfeitoIrreversivel = "Se a publicação de um ato deste tipo não pode ser desfeita." + Recusa;

    public const string EhResultado = "Se um ato deste tipo determina a situação do candidato." + Recusa;

    public const string PublicavelComoAvulso = "Se um ato deste tipo pode ser publicado como documento avulso de um processo seletivo." + Recusa;

    public const string ExigeAtoCorrigido = "Se um ato deste tipo corrige sempre outro ato publicado." + Recusa;

    public const string VigenciaInicio = "Primeiro dia em que esta versão do tipo vale, inclusivo." + Recusa;
}
