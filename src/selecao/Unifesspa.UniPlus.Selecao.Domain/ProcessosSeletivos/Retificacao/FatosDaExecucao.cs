namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a execução do processo já fez e que a retificação compara com o documento novo, lido uma só vez, depois
/// da trava, no mesmo <see cref="Instante"/>. Quem monta os fatos os calcula todos nesse instante: as regras nunca
/// leem o relógio nem o cadastro.
/// </summary>
public sealed record FatosDaExecucao
{
    /// <summary>O instante em que todos os fatos foram lidos.</summary>
    public required DateTimeOffset Instante { get; init; }

    /// <summary>As fases e etapas da versão publicada vigente que já começaram, com a unidade publicada.</summary>
    public required FasesIniciadas FasesIniciadas { get; init; }

    /// <summary>Os itens do edital que já tiveram a primeira publicação (ato publicado, ou ainda pendente de confirmação).</summary>
    public IReadOnlySet<IdentidadeDoItemDoEdital> ItensPublicados { get; init; } = new HashSet<IdentidadeDoItemDoEdital>();

    /// <summary>Os formulários que já receberam ao menos um envio de candidato; o rascunho não conta.</summary>
    public IReadOnlySet<CodigoDoFormulario> FormulariosComEnvio { get; init; } = new HashSet<CodigoDoFormulario>();
}
