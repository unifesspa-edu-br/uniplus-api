namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que um teste decide sobre o congelamento de uma versão: o edital e o documento que se congela. O resto vem com valores
/// válidos — o ator de teste, o relógio do sistema, sem calendário, os fatos de modalidade do catálogo, sem tipo de ato único
/// os corretores que o próprio processo declara e nenhum motivo escolhido —, e o teste troca só o que o cenário exige.
/// </summary>
internal sealed record CenarioDeCongelamento
{
    public required DadosEdital Dados { get; init; }

    public required DocumentoCongelado Documento { get; init; }

    public string AtorUsuarioContaId { get; init; } = "user-conta-123";

    public TimeProvider Relogio { get; init; } = TimeProvider.System;

    public ContextoDeContagemDePrazos Contagem { get; init; } = ContagemDePrazosDeTeste.ComCalendarioVigente;

    public FatosDeModalidade FatosDeModalidade { get; init; } = FatosDeModalidadeDeTeste.DoCatalogo;

    public TiposDeAtoUnicosPorObjeto TiposDeAtoUnicos { get; init; } = TiposDeAtoUnicosDeTeste.Nenhum;

    public CorretoresDasEtapas? Corretores { get; init; }

    public MotivosEscolhidos? MotivosEscolhidos { get; init; }

    public string? RegraAusenteDoCatalogo { get; init; }
}
