namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Errors;

/// <summary>Códigos do formulário portável importado de fora.</summary>
internal static class ErrosDoFormularioPortavel
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new(FormularioPortavelErrorCodes.ObrigatoriedadeInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.formulario_portavel.obrigatoriedade_invalida", "A obrigatoriedade é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele")),
        new(FormularioPortavelErrorCodes.OperacaoDeAgregadoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.formulario_portavel.operacao_de_agregado_invalida", "A operação do agregado é EXISTE ou VALORES_PRESENTES")),
        new(FormularioPortavelErrorCodes.DerivacaoInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.formulario_portavel.derivacao_invalida", "A derivação do formulário não forma regras válidas")),
        new(FormularioPortavelErrorCodes.EstruturaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.formulario_portavel.estrutura_invalida", "As regras não formam um formulário: elemento nulo, código repetido ou referência que não existe")),
        new(FormularioPortavelErrorCodes.OcorrenciaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.formulario_portavel.ocorrencia_invalida", "Ocorrência de grupo sem identidade própria no grupo")),
        new(FormularioPortavelErrorCodes.FusoDoEnvioDesconhecido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.formulario_portavel.fuso_do_envio_desconhecido", "A zona do envio simulado não é reconhecida")),
        new(FormularioPortavelErrorCodes.InstanteDoEnvioForaDoCalendario, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.formulario_portavel.instante_do_envio_fora_do_calendario", "O instante do envio simulado está ausente ou fora do calendário que os limites da faixa de data alcançam")),
    ];
}
