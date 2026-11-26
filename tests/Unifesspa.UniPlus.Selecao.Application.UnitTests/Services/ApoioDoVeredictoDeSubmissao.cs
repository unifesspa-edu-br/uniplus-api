namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A montagem que os testes do resultado da submissão compartilham: avaliação pronta, termos exigidos e aceites.</summary>
internal static class ApoioDoVeredictoDeSubmissao
{
    internal static readonly IReadOnlyDictionary<TermoNoFormulario, TermoAdmitido> SemTermos = new Dictionary<TermoNoFormulario, TermoAdmitido>();
    internal static readonly IReadOnlyList<AceiteDeTermo> SemAceites = [];
    internal static readonly DateOnly DiaDoEnvio = new(2026, 10, 10);

    internal static AvaliacaoFormulario AvaliacaoCom(
        IReadOnlyDictionary<string, FatoResolvido> fatos,
        IReadOnlyList<AvaliacaoItem>? itens = null,
        IReadOnlyList<AvaliacaoTermo>? termos = null,
        IReadOnlyList<AvaliacaoGrupo>? grupos = null) =>
        new(fatos, itens ?? [], termos ?? [], grupos ?? [], []);

    internal static Dictionary<TermoNoFormulario, TermoAdmitido> TermoExigido(
        string codigo, Guid versaoId, string hash, string formulario = FormularioDeTeste.CodigoDaInscricao) =>
        new Dictionary<TermoNoFormulario, TermoAdmitido> { [new TermoNoFormulario(formulario, codigo)] = new TermoAdmitido(versaoId, hash) };

    internal static Dictionary<TermoNoFormulario, TermoAdmitido> TermoExigidoEm(Guid versaoId, params string[] formularios) =>
        formularios.ToDictionary(
            static formulario => new TermoNoFormulario(formulario, "DECLARACAO_VERACIDADE"), _ => new TermoAdmitido(versaoId, new string('a', 64)));

    internal static AvaliacaoFormulario AvaliacaoComTermoEm(params string[] formularios) => AvaliacaoCom(
        new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
        termos: [.. formularios.Select(static formulario => new AvaliacaoTermo(CodigoPrefixadoDe(formulario, "DECLARACAO_VERACIDADE"), Ternario.Verdadeiro, Ternario.Verdadeiro))]);

    /// <summary>
    /// avaliacao.Termos usa o código PREFIXADO pelo formulário (DefinicaoDoProcesso.CodigoDoTermo) —
    /// os termos exigidos e os aceites citam o formulário e o código nu que o candidato conhece e envia,
    /// e é nele que a recusa sai.
    /// </summary>
    internal static readonly string CodigoPrefixado = CodigoPrefixadoDe(FormularioDeTeste.CodigoDaInscricao, "DECLARACAO_VERACIDADE");

    internal static string CodigoPrefixadoDe(string formulario, string termo) =>
        DefinicaoDoProcesso.CodigoDoTermo(CodigoDoFormulario.Criar(formulario).Value!, termo);
}
