namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Mapeamento da <see cref="Providencia"/> para a resposta e para a trilha da concessão. A situação e o encerramento da
/// concessão são derivados no instante da leitura; o e-mail de quem solicitou nunca sai.
/// </summary>
public static class ProvidenciaMapping
{
    /// <summary>O detalhe, com o fundamento e o motivo do desfecho.</summary>
    /// <param name="leitor">Quem lê, para dizer se está impedido de responder; nulo quando o token não o identifica.</param>
    public static ProvidenciaDto ToDto(ProvidenciaComConcessao item, DateTimeOffset agora, Identidade? leitor)
    {
        ArgumentNullException.ThrowIfNull(item);

        (Providencia p, Concessao c) = (item.Providencia, item.Alvo.Concessao);
        SituacaoDaProvidencia situacao = p.SituacaoEm(c, agora);

        return new(
            p.Id, p.Tipo.ToCodigo(), p.Fundamento, situacao.ToCodigo(), p.AbertaEm, Pessoa(p.SolicitadaPor), Alvo(item.Alvo, agora),
            Encerramento(situacao, c, agora), Desfecho(p), Impedido(p, c, leitor));
    }

    /// <summary>O item da fila, sem o fundamento e sem o motivo do desfecho.</summary>
    public static ProvidenciaResumoDto ToResumo(ProvidenciaComConcessao item, DateTimeOffset agora, Identidade? leitor)
    {
        ArgumentNullException.ThrowIfNull(item);

        (Providencia p, Concessao c) = (item.Providencia, item.Alvo.Concessao);
        SituacaoDaProvidencia situacao = p.SituacaoEm(c, agora);

        return new(
            p.Id, p.Tipo.ToCodigo(), situacao.ToCodigo(), p.AbertaEm, Pessoa(p.SolicitadaPor), Alvo(item.Alvo, agora),
            Encerramento(situacao, c, agora), AtoDoDesfecho(p), Impedido(p, c, leitor));
    }

    /// <summary>Os eventos que a providência acrescenta à trilha da concessão: a solicitação e o desfecho, se houver.</summary>
    public static IEnumerable<EventoDaTrilhaDto> EventosDaTrilha(Providencia p)
    {
        ArgumentNullException.ThrowIfNull(p);

        yield return new EventoDaTrilhaDto("providencia_solicitada", p.AbertaEm, Pessoa(p.SolicitadaPor), $"{Rotulo(p.Tipo)}. Fundamento: {p.Fundamento}");

        if (p is { DesfechoEm: { } em, DesfechoPor: { } por })
        {
            string tipo = p.Estado == EstadoDaProvidencia.Atendida ? "providencia_atendida" : "providencia_respondida";
            yield return new EventoDaTrilhaDto(tipo, em, Pessoa(por), p.MotivoDoDesfecho);
        }
    }

    private static ConcessaoDaProvidenciaDto Alvo(ConcessaoDaProvidencia alvo, DateTimeOffset agora)
    {
        Concessao c = alvo.Concessao;

        return new(
            c.Id, Pessoa(c.Beneficiario), new PerfilResumoDto(c.PerfilId, alvo.PerfilNome), new EscopoDto(c.EscopoTipo.ToCodigo(), c.UnidadeId),
            c.ValidaAte, c.EstaAtiva(agora));
    }

    // Só a prejudicada diz o que encerrou a concessão: na atendida, o encerramento é o próprio atendimento.
    private static EncerramentoDaConcessaoDto? Encerramento(SituacaoDaProvidencia situacao, Concessao c, DateTimeOffset agora) =>
        situacao == SituacaoDaProvidencia.Prejudicada && EncerramentoDaConcessao.De(c, agora) is { } encerramento
            ? new EncerramentoDaConcessaoDto(encerramento.Causa.ToCodigo(), encerramento.Em)
            : null;

    private static DesfechoDaProvidenciaDto? Desfecho(Providencia p) =>
        p is { DesfechoEm: { } em, DesfechoPor: { } por, MotivoDoDesfecho: { } motivo } ? new(em, Pessoa(por), motivo) : null;

    private static AtoDoDesfechoDto? AtoDoDesfecho(Providencia p) =>
        p is { DesfechoEm: { } em, DesfechoPor: { } por } ? new(em, Pessoa(por)) : null;

    private static bool Impedido(Providencia p, Concessao c, Identidade? leitor) => leitor is not null && p.ImpedeResposta(leitor, c);

    private static IdentidadeDto Pessoa(Identidade pessoa) => new(pessoa.Emissor, pessoa.Sub, pessoa.Nome);

    private static string Rotulo(TipoDaProvidencia tipo) =>
        tipo == TipoDaProvidencia.Retirada ? "Retirada do acesso" : "Revisão do acesso";
}
