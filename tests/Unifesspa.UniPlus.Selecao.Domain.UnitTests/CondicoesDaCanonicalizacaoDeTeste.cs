namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>As condições da canonicalização, lida pela primeira causa: o que cada regra isolada recusa no processo do teste.</summary>
internal static class CondicoesDaCanonicalizacaoDeTeste
{
    public static DomainError? PrimeiraCausa(ProcessoSeletivo processo, CadastrosDaConfiguracao? cadastros = null) =>
        CondicoesDaCanonicalizacao.Causas(processo.Declaracao(), cadastros ?? CadastrosDeTeste.Padrao) is [var primeira, ..]
            ? primeira.Error
            : null;
}
