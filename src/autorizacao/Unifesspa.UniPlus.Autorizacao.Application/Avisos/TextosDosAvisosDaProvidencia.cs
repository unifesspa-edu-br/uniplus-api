namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

/// <summary>
/// Os textos dos avisos da providência sobre concessão. Nenhum leva fundamento, motivo, nome de pessoa nem e-mail
/// (UNI-REQ-0375). O aviso ao beneficiário da revogação pelo atendimento não diz quem pediu nem quem fez a remoção.
/// </summary>
internal static class TextosDosAvisosDaProvidencia
{
    public static IEnumerable<KeyValuePair<TipoDeAviso, TextoDoAviso>> Todos { get; } =
    [
        TextoDoAviso.Para(
            TipoDeAviso.ProvidenciaSolicitada,
            "Solicitação de providência sobre concessão de acesso",
            "Uma solicitação de providência sobre uma concessão de acesso aguarda resposta de um gestor de acessos ou de um Representante do controlador."),
        TextoDoAviso.Para(
            TipoDeAviso.ProvidenciaRespondida,
            "Providência respondida",
            "A solicitação de providência que você fez foi respondida."),
        TextoDoAviso.Para(
            TipoDeAviso.ProvidenciaAtendida,
            "Providência atendida",
            "A solicitação de providência que você fez foi atendida, e a concessão foi revogada."),
        TextoDoAviso.Para(
            TipoDeAviso.ConcessaoRevogadaNoAtendimento,
            "Concessão de acesso revogada",
            "Uma concessão de acesso sua no Uni+ foi revogada e deixou de valer. Se você tiver motivos para manter este perfil de acesso, faça uma nova solicitação comprovando a necessidade."),
    ];
}
