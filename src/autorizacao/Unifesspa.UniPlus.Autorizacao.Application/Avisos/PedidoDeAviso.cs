namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Quem recebe um aviso: a identidade, que o torna único, e o endereço de onde se manda.</summary>
public sealed record DestinatarioDoAviso(Identidade Pessoa, string Email);

/// <summary>O que o emissor precisa para avisar: o modelo, o fato e o marco que o identificam, quem recebe e quem fica de fora.</summary>
/// <param name="Modelo">O conteúdo do aviso.</param>
/// <param name="FatoId">O fato que gerou o aviso.</param>
/// <param name="Marco">Separa o que um fato emite mais de uma vez; vazio quando o fato emite uma só.</param>
/// <param name="Destinatarios">Quem pode receber; o emissor mantém uma mensagem por identidade.</param>
/// <param name="Excluidos">Quem praticou o ato e não recebe o aviso dele.</param>
public sealed record PedidoDeAviso(
    ModeloDeAviso Modelo,
    Guid FatoId,
    string Marco,
    IReadOnlyList<DestinatarioDoAviso> Destinatarios,
    IReadOnlyCollection<Identidade> Excluidos);
