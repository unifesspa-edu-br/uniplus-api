namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que um teste decide sobre o congelamento de uma versão: o edital e o documento que se congela. O resto vem com valores
/// válidos — o ator de teste, o relógio do sistema, sem calendário, os fatos de modalidade do catálogo, sem tipo de ato único
/// e os corretores que o próprio processo declara —, e o teste troca só o que o cenário exige.
/// </summary>
internal sealed record CenarioDeCongelamento
{
    public required DadosEdital Dados { get; init; }

    public required DocumentoCongelado Documento { get; init; }

    public string AtorUsuarioContaId { get; init; } = "user-conta-123";

    public TimeProvider Relogio { get; init; } = TimeProvider.System;

    public ContextoDeContagemDePrazos Contagem { get; init; } = ContextoDeContagemDePrazos.SemCalendario;

    public FatosDeModalidade FatosDeModalidade { get; init; } = FatosDeModalidadeDeTeste.DoCatalogo;

    public TiposDeAtoUnicosPorObjeto TiposDeAtoUnicos { get; init; } = TiposDeAtoUnicosDeTeste.Nenhum;

    public CorretoresDasEtapas? Corretores { get; init; }
}
