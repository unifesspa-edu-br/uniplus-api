namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

/// <summary>
/// O que a camada de aplicação leu dos cadastros vivos, uma vez e como valor, para julgar a configuração: o domínio não
/// conhece os cadastros (ADR-0042). É classe, e não registro, porque igualdade de valor não faz sentido para uma leitura de
/// cadastros feita uma única vez.
/// </summary>
public sealed class CadastrosDaConfiguracao
{
    public CadastrosDaConfiguracao(
        ContextoDeContagemDePrazos contagemDePrazos,
        FatosDeModalidade fatosDeModalidade,
        TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos,
        CorretoresDasEtapas corretores,
        ProtecaoVigenteDasExigencias protecao,
        MotivosEscolhidos motivosEscolhidos,
        FatosDasVersoesPublicadas? fatosDasVersoesPublicadas = null)
    {
        ArgumentNullException.ThrowIfNull(contagemDePrazos);
        ArgumentNullException.ThrowIfNull(fatosDeModalidade);
        ArgumentNullException.ThrowIfNull(tiposDeAtoUnicos);
        ArgumentNullException.ThrowIfNull(corretores);
        ArgumentNullException.ThrowIfNull(protecao);
        ArgumentNullException.ThrowIfNull(motivosEscolhidos);

        ContagemDePrazos = contagemDePrazos;
        FatosDeModalidade = fatosDeModalidade;
        TiposDeAtoUnicos = tiposDeAtoUnicos;
        Corretores = corretores;
        Protecao = protecao;
        MotivosEscolhidos = motivosEscolhidos;
        FatosDasVersoesPublicadas = fatosDasVersoesPublicadas;
    }

    /// <summary>O calendário vigente e o fuso institucional.</summary>
    public ContextoDeContagemDePrazos ContagemDePrazos { get; }

    public FatosDeModalidade FatosDeModalidade { get; }

    public TiposDeAtoUnicosPorObjeto TiposDeAtoUnicos { get; }

    public CorretoresDasEtapas Corretores { get; }

    /// <summary>A proteção efetiva das exigências, a mesma em todo caminho que julga a configuração.</summary>
    public ProtecaoVigenteDasExigencias Protecao { get; }

    /// <summary>Os motivos de decisão que o processo escolheu, com a situação de cada um no cadastro, para a versão que nasce.</summary>
    public MotivosEscolhidos MotivosEscolhidos { get; }

    /// <summary>O que os formulários coletaram nas versões publicadas; nulo antes da primeira publicação.</summary>
    public FatosDasVersoesPublicadas? FatosDasVersoesPublicadas { get; }
}
