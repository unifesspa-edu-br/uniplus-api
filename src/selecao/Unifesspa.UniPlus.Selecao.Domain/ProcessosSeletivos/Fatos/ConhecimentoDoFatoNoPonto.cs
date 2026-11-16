namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

/// <summary>Se um fato é conhecido no ponto de uso e, quando não é, qual das condições falha.</summary>
public enum ConhecimentoDoFatoNoPonto
{
    Conhecido = 1,

    /// <summary>Nenhum formulário produz o fato, o catálogo não o situa e ele não é derivado.</summary>
    ForaDoProcesso,

    /// <summary>A fase do catálogo para o fato é posterior ao momento do ponto.</summary>
    ConhecidoEmFasePosterior,

    /// <summary>O formulário que produz o fato não é o do ponto nem um que ele alcança.</summary>
    RespostaNaoAlcancada,

    /// <summary>O catálogo situa o fato numa fase que o cronograma do processo não tem.</summary>
    FaseDoCatalogoForaDoCronograma,

    /// <summary>O derivado tem alguma dependência desconhecida no ponto, ou depende de si mesmo.</summary>
    DependenciaDesconhecida,
}
