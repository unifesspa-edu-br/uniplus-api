namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class CoerenciaDaConsequenciaDoDescumprimento
{
    /// <summary>
    /// Coerência entre a consequência de indeferimento declarada em cada
    /// <see cref="DocumentoExigido"/> e a ação da vaga, lida de
    /// <see cref="ModalidadeSelecionada.AcaoQuandoIndeferido"/>, sem campo duplicado em
    /// <see cref="DocumentoExigido"/>. Avaliada depois de
    /// <see cref="PendenciasDasExigencias.PendenciaDasExigenciasDocumentais"/> e sem cache: uma mudança de
    /// gatilho ou de distribuição de vagas entre versões é sempre reavaliada.
    /// </summary>
    /// <remarks>
    /// Duas checagens:
    /// <list type="bullet">
    /// <item><c>RETIRA_O_ACRESCIMO</c> exige o Fator oferecido no processo — o Fator modelado é <see cref="ProcessoSeletivo.BonusRegional"/> (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015; só existe quando
    /// <see cref="ProcessoSeletivo.AplicaBonusRegional"/> é verdadeiro).</item>
    /// <item>Para cada modalidade que a exigência alcança pelas condições sobre os fatos cujos
    /// valores são modalidades (os mesmos da avaliação de conformidade, <see cref="Services.AvaliadorConformidadeLegal"/>),
    /// quando essa modalidade declara <c>AcaoQuandoIndeferido</c>, a consequência precisa
    /// ser idêntica — mesmo vocabulário fechado dos dois lados.</item>
    /// </list>
    /// </remarks>
    internal static DomainError? PendenciaDeCoerenciaDaConsequenciaDeIndeferimento(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatosDeModalidade)
    {
        foreach (DocumentoExigido exigencia in configuracao.Exigencias.DocumentosExigidos)
        {
            if (RecusaDaExigencia(configuracao, exigencia, fatosDeModalidade) is { } recusa)
            {
                return recusa;
            }
        }
        return RecusaDosGrupos(configuracao, fatosDeModalidade);
    }

    private static DomainError? RecusaDosGrupos(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatosDeModalidade)
    {
        foreach (RegraDeComprovacao grupo in configuracao.Exigencias.RegrasDeComprovacao.Where(static regra => regra.Forma == FormaDeComprovacao.QuantidadeMinimaDeItens && regra.Consequencia is not null))
        {
            if (RecusaDoGrupo(configuracao, grupo, fatosDeModalidade) is { } recusa)
            {
                return recusa;
            }
        }
        return null;
    }

    private static DomainError? RecusaDaExigencia(ConfiguracaoDeclarada configuracao, DocumentoExigido exigencia, FatosDeModalidade fatosDeModalidade)
    {
        if (exigencia.ConsequenciaIndeferimento is not { } consequencia)
        {
            return null;
        }
        if (ConsequenciaAcrescimoNaoOferecido(configuracao, consequencia))
        {
            return new DomainError("ConsequenciasDoDescumprimento.AcrescimoNaoOferecido", $"A exigência '{exigencia.TipoDocumentoNome}' determina retirar o acréscimo, mas o Fator de Atenuação das Desigualdades de Acesso não é oferecido.");
        }
        ModalidadeSelecionada? modalidade = ModalidadeIncoerenteComConsequencia(consequencia, ModalidadesAlcancadasPor(configuracao, exigencia, fatosDeModalidade));
        return modalidade is null ? null : new DomainError("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga",
            $"A consequência da exigência '{exigencia.TipoDocumentoNome}' não corresponde à ação de indeferimento da modalidade {DescricaoDaModalidade(modalidade)}.");
    }

    private static DomainError? RecusaDoGrupo(ConfiguracaoDeclarada configuracao, RegraDeComprovacao grupo, FatosDeModalidade fatosDeModalidade)
    {
        ConsequenciaDoDescumprimento consequencia = grupo.Consequencia!.Value;
        if (ConsequenciaAcrescimoNaoOferecido(configuracao, consequencia))
        {
            return new DomainError("ConsequenciasDoDescumprimento.AcrescimoNaoOferecido", "O grupo determina retirar o acréscimo, mas o Fator de Atenuação das Desigualdades de Acesso não é oferecido.");
        }
        ModalidadeSelecionada? modalidade = ModalidadeIncoerenteComConsequencia(consequencia, ModalidadesAlcancadasPor(configuracao, grupo, fatosDeModalidade));
        return modalidade is null ? null : new DomainError("RegraDeComprovacao.ConsequenciaIncoerenteComAcaoDaVaga",
            $"A consequência do grupo não corresponde à ação de indeferimento da modalidade {DescricaoDaModalidade(modalidade)}.");
    }

    private static string DescricaoDaModalidade(ModalidadeSelecionada modalidade) =>
        string.IsNullOrWhiteSpace(modalidade.Descricao) ? "selecionada" : modalidade.Descricao;

    /// <summary>A consequência é RETIRA_O_ACRESCIMO sem nenhum acréscimo oferecido no processo (bônus regional, Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015).</summary>
    internal static bool ConsequenciaAcrescimoNaoOferecido(ConfiguracaoDeclarada configuracao, ConsequenciaDoDescumprimento? consequencia) =>
        consequencia == ConsequenciaDoDescumprimento.RetiraOAcrescimo && configuracao.Vagas.BonusRegional is null;

    /// <summary>
    /// A primeira modalidade alcançada cuja <c>AcaoQuandoIndeferido</c> declarada é incoerente com a
    /// consequência — ou <see langword="null"/> quando todas as que declaram ação concordam. Devolve a
    /// modalidade para que a mensagem e a verificação de conformidade usem a mesma busca.
    /// </summary>
    internal static ModalidadeSelecionada? ModalidadeIncoerenteComConsequencia(
        ConsequenciaDoDescumprimento consequencia, IEnumerable<ModalidadeSelecionada> modalidadesAlcancadas)
    {
        return modalidadesAlcancadas.FirstOrDefault(modalidade => modalidade.AcaoQuandoIndeferido is { } acao
            && !string.Equals(acao, consequencia.Codigo(), StringComparison.Ordinal));
    }

    /// <summary>Existe <see cref="DocumentoExigido"/> com RETIRA_O_ACRESCIMO sem o Fator oferecido.</summary>
    internal static bool ExisteExigenciaComAcrescimoNaoOferecido(ConfiguracaoDeclarada configuracao) =>
        configuracao.Exigencias.DocumentosExigidos.Any(e => ConsequenciaAcrescimoNaoOferecido(configuracao, e.ConsequenciaIndeferimento));

    /// <summary>Existe <see cref="DocumentoExigido"/> com consequência incoerente com a ação de alguma modalidade alcançada.</summary>
    internal static bool ExisteExigenciaConsequenciaIncoerenteComAcaoDaVaga(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatosDeModalidade) =>
        configuracao.Exigencias.DocumentosExigidos.Any(e => e.ConsequenciaIndeferimento is { } c
            && ModalidadeIncoerenteComConsequencia(c, ModalidadesAlcancadasPor(configuracao, e, fatosDeModalidade)) is not null);

    /// <summary>Existe grupo que exige uma quantidade mínima de itens com consequência própria RETIRA_O_ACRESCIMO sem o Fator oferecido.</summary>
    internal static bool ExisteGrupoComAcrescimoNaoOferecido(ConfiguracaoDeclarada configuracao) =>
        configuracao.Exigencias.RegrasDeComprovacao.Any(regra => regra.Forma == FormaDeComprovacao.QuantidadeMinimaDeItens && ConsequenciaAcrescimoNaoOferecido(configuracao, regra.Consequencia));

    /// <summary>Existe grupo que exige uma quantidade mínima de itens com consequência própria incoerente com a ação de alguma modalidade alcançada.</summary>
    internal static bool ExisteGrupoConsequenciaIncoerenteComAcaoDaVaga(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatosDeModalidade) =>
        configuracao.Exigencias.RegrasDeComprovacao.Any(regra => regra.Forma == FormaDeComprovacao.QuantidadeMinimaDeItens && regra.Consequencia is { } consequencia
            && ModalidadeIncoerenteComConsequencia(consequencia, ModalidadesAlcancadasPor(configuracao, regra, fatosDeModalidade)) is not null);

    /// <summary>
    /// Modalidades ofertadas que o gatilho da exigência PODE alcançar — verificação
    /// estrutural (<see cref="DocumentoExigido.PodeAlcancarModalidade"/>), não a avaliação
    /// factual de <see cref="DocumentoExigido.AplicavelPara"/> que a avaliação de conformidade
    /// legal usa (<see cref="Services.AvaliadorConformidadeLegal"/>) — aqui não há
    /// candidato real, só a pergunta "existe combinação de fatos em que este documento se
    /// torna exigido para alguém nesta modalidade?".
    /// </summary>
    internal static IEnumerable<ModalidadeSelecionada> ModalidadesAlcancadasPor(ConfiguracaoDeclarada configuracao, DocumentoExigido exigencia, FatosDeModalidade fatosDeModalidade) =>
        configuracao.Vagas.DistribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .Where(modalidade => exigencia.PodeAlcancarModalidade(modalidade.Codigo, fatosDeModalidade));

    /// <summary>Confere o alcance da modalidade pelas regras contidas no grupo.</summary>
    internal static IEnumerable<ModalidadeSelecionada> ModalidadesAlcancadasPor(ConfiguracaoDeclarada configuracao, RegraDeComprovacao regra, FatosDeModalidade fatosDeModalidade) =>
        configuracao.Vagas.DistribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .Where(modalidade => regra.PodeAlcancarModalidade(modalidade.Codigo, fatosDeModalidade));
}
