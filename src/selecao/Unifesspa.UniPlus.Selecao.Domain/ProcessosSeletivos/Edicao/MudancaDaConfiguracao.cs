namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O que uma edição muda nas coleções do processo. Coleção sem troca informada não muda. A raiz aplica
/// a mudança e vincula ao processo o que entra. Os planos a calculam antes de qualquer efeito; a exceção
/// é <c>ReposicaoDaConfiguracao.Repor</c>, que atualiza no lugar as partes vivas que continuam e só
/// devolve a troca que falta aplicar.
/// </summary>
internal sealed record MudancaDaConfiguracao
{
    internal Troca<EtapaProcesso> Etapas { get; init; }

    internal Troca<FaseCronograma> CronogramaFases { get; init; }

    internal Troca<DocumentoExigido> DocumentosExigidos { get; init; }

    internal Troca<NoExigencia> NosExigencia { get; init; }

    internal Troca<FatoColetado> Campos { get; init; }

    internal Troca<GrupoColetado> GruposColetados { get; init; }

    internal Troca<FormularioProcesso> Formularios { get; init; }

    internal Troca<TermoExigidoFormulario> TermosExigidos { get; init; }

    internal Troca<OpcaoDeclaradaFato> OpcoesDeclaradas { get; init; }

    internal Troca<ConfiguracaoDerivacaoFato> RegrasDerivacao { get; init; }

    internal Troca<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas { get; init; }

    internal Troca<CriterioDesempate> CriteriosDesempate { get; init; }
}
