namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Códigos de recusa da exigência quanto à fase e ao formulário em que o documento é coletado, inclusive pelo gatilho.</summary>
public static class DocumentoExigidoErrorCodes
{
    public const string FatoNaoColetadoPeloProcesso = "PredicadoDnf.FatoNaoColetadoPeloProcesso";
    public const string FatoDeFormularioNaoAlcancado = "DocumentoExigido.FatoDeFormularioNaoAlcancado";
    public const string FatoDeMembroForaDaRepeticao = "DocumentoExigido.FatoDeMembroForaDaRepeticao";
    public const string FatoResolvidoEmFasePosterior = "DocumentoExigido.FatoResolvidoEmFasePosterior";
    public const string PontoResolucaoForaDoCronograma = "DocumentoExigido.PontoResolucaoForaDoCronograma";
}
